#Requires -Version 5.1
<#
F27-T014 — cadastro dos telefones IP (HSDS) no Zabbix — provisionamento idempotente.

Inventário: scripts\zabbix\telefones-hsds.json (fonte: planilha do usuário,
2026-11-10 — ver docs\37-F27-MONITORAMENTO-ZABBIX.md §7.0.1/§7.0.2).

O que cada telefone recebe:

* grupo "Telefonia HSDS";
* host técnico tel-192-168-21-140 (IP normalizado), nome
  "TEL 1518 - NIR (1º andar)";
* interface (agent, useip=1) apontando para o IP do aparelho — os itens são
  simple checks, a porta não é usada;
* itens icmpping / icmppingloss / icmppingsec (espelham o template
  "ICMP Ping" 10564 — NÃO linkamos o template porque as triggers dele já nascem
  habilitadas e não podem ser desativadas por host; mudá-las afetaria os 59 hosts
  que já usam o template: docs/37 §7.0.2);
* UMA trigger por telefone: "TEL 1518 - NIR (1º andar) fora do ar",
  expressão max(/tel-.../icmpping,#3)=0, severidade High (4) — a action 7 é
  global e o filtro do InfraControl só repassa High/Critical ao WhatsApp;
* tags type=telefone-ip / managed-by=InfraControl / ramal / local / mac / setor
  + macros {$SETOR}, {$RAMAL}, {$MAC}, {$LOCAL}.

A trigger nasce DESABILITADA (status=1) para não gerar alerta em massa no
WhatsApp da empresa. Uso:

    .\provision-telefones.ps1            # cria (trigger nova nasce desabilitada)
    .\provision-telefones.ps1 -Enable    # habilita as triggers
    .\provision-telefones.ps1 -Disable   # desabilita as triggers

Sobrescritas por env: ZBX_API_URL, ZBX_USER, ZBX_PASSWORD (docs/27 §4).
#>
[CmdletBinding()]
param(
    [switch]$Enable,
    [switch]$Disable
)

$ErrorActionPreference = 'Stop'

if ($Enable -and $Disable) { throw '-Enable e -Disable são mutuamente exclusivos' }

$ApiUrl  = if ($env:ZBX_API_URL)    { $env:ZBX_API_URL }    else { 'http://localhost:8086/api_jsonrpc.php' }
$ApiUser = if ($env:ZBX_USER)       { $env:ZBX_USER }       else { 'Admin' }
$ApiPass = if ($env:ZBX_PASSWORD)   { $env:ZBX_PASSWORD }   else { 'zabbix' }
$DataFile = Join-Path $PSScriptRoot 'telefones-hsds.json'
$GroupName = 'Telefonia HSDS'
$InterfacePort = '10050'
$StatusFlag = if ($Enable) { 0 } elseif ($Disable) { 1 } else { $null }

$script:Token = $null
$Stats = @{ HostsNew = 0; HostsExisting = 0; Items = 0; TriggersNew = 0;
            TriggersUpdated = 0; TriggersStatus = 0; Skipped = 0 }

function Invoke-Zabbix {
    param([string]$Method, $Params, [switch]$NoAuth)
    $body = [ordered]@{ jsonrpc = '2.0'; method = $Method; params = $Params; id = 1 }
    $json = $body | ConvertTo-Json -Depth 12
    $bytes = [Text.Encoding]::UTF8.GetBytes($json)
    # PS 5.1 não deixa Content-Type em -Headers: usar -ContentType
    $call = @{ Uri = $ApiUrl; Method = 'Post'; Body = $bytes
               ContentType = 'application/json; charset=utf-8'; TimeoutSec = 60 }
    if ($script:Token -and -not $NoAuth) { $call.Headers = @{ Authorization = "Bearer $($script:Token)" } }
    try {
        $resp = Invoke-RestMethod @call
    } catch {
        throw "falha HTTP em ${Method}: $($_.Exception.Message)"
    }
    if ($resp.error) { throw "erro em ${Method}: $(ConvertTo-Json $resp.error -Depth 6 -Compress)" }
    return $resp.result
}

function Get-Group {
    $found = @(Invoke-Zabbix 'hostgroup.get' @{ filter = @{ name = $GroupName }; output = @('groupid') })
    if ($found.Count -gt 0) { return $found[0].groupid }
    $created = Invoke-Zabbix 'hostgroup.create' @{ name = $GroupName }
    Write-Host "  group '$GroupName' criada (id $($created.groupids[0]))"
    return $created.groupids[0]
}

function Get-ZbxHost {
    param([string]$HostName)
    $found = @(Invoke-Zabbix 'host.get' @{
        filter = @{ host = $HostName }
        output = @('hostid', 'name')
        selectInterfaces = @('interfaceid', 'type', 'ip', 'dns', 'port', 'useip', 'main')
        selectTags = @('tag', 'value')
        selectMacros = @('hostmacroid', 'macro', 'value')
    })
    if ($found.Count -gt 0) { return $found[0] }
    return $null
}

function Get-HostName {
    param($Phone)
    $ramal = "$($Phone.ramal)".Trim()
    if ($ramal) { "TEL $ramal - $($Phone.setor) ($($Phone.local))" }
    else { "TEL $($Phone.setor) ($($Phone.local))" }
}

function Get-TechnicalName {
    param($Phone)
    'tel-' + $Phone.ip.Replace('.', '-')
}

function Ensure-Interface {
    param([string]$HostId, [string]$Ip, $Interfaces)
    if (-not $Interfaces -or $Interfaces.Count -eq 0) {
        $created = Invoke-Zabbix 'hostinterface.create' @{
            hostid = $HostId; type = 1; main = 1; useip = 1
            ip = $Ip; dns = ''; port = $InterfacePort
        }
        Write-Host "    interface criada -> ${Ip}:$InterfacePort"
        return $created.interfaceids[0]
    }
    $iface = @($Interfaces)[0]
    if ($iface.ip -ne $Ip -or $iface.port -ne $InterfacePort -or $iface.useip -ne '1') {
        Invoke-Zabbix 'hostinterface.update' @{
            interfaceid = $iface.interfaceid; type = 1; main = 1; useip = 1
            ip = $Ip; dns = ''; port = $InterfacePort
        } | Out-Null
        Write-Host "    interface ajustada -> ${Ip}:$InterfacePort"
    }
    return $iface.interfaceid
}

function Ensure-HostTags {
    param([string]$HostId, $Desired, $Existing)
    $merged = @{}
    foreach ($t in @($Existing)) { if ($t.tag) { $merged[$t.tag] = "$($t.value)" } }
    $changed = $false
    foreach ($key in $Desired.Keys) {
        if ($merged[$key] -ne $Desired[$key]) { $merged[$key] = $Desired[$key]; $changed = $true }
    }
    if ($changed) {
        $tags = foreach ($key in ($merged.Keys | Sort-Object)) {
            @{ tag = $key; value = $merged[$key] }
        }
        Invoke-Zabbix 'host.update' @{ hostid = $HostId; tags = $tags } | Out-Null
    }
}

function Ensure-HostMacros {
    param([string]$HostId, $Desired, $Existing)
    $current = @{}
    foreach ($m in @($Existing)) { if ($m.macro) { $current[$m.macro] = $m } }
    foreach ($key in $Desired.Keys) {
        $row = $current[$key]
        if (-not $row) {
            Invoke-Zabbix 'usermacro.create' @{ hostid = $HostId; macro = $key; value = $Desired[$key] } | Out-Null
            Write-Host "    macro $key criada = '$($Desired[$key])'"
        } elseif ($row.value -ne $Desired[$key]) {
            Invoke-Zabbix 'usermacro.update' @{ hostmacroid = $row.hostmacroid; value = $Desired[$key] } | Out-Null
            Write-Host "    macro $key -> '$($Desired[$key])'"
        }
    }
}

function Ensure-Items {
    param([string]$HostId, [string]$InterfaceId)
    $existing = @{}
    foreach ($i in @(Invoke-Zabbix 'item.get' @{ hostids = @($HostId); output = @('key_') })) {
        $existing[$i.key_] = $true
    }
    $toCreate = @()
    foreach ($spec in @(
        @{ key = 'icmpping';      name = 'ICMP ping';            valueType = 3 },
        @{ key = 'icmppingloss';  name = 'ICMP loss';            valueType = 0 },
        @{ key = 'icmppingsec';   name = 'ICMP response time';   valueType = 0 }
    )) {
        if ($existing[$spec.key]) { continue }
        $toCreate += @{
            hostid = $HostId; interfaceid = $InterfaceId; name = $spec.name
            key_ = $spec.key; type = 3; value_type = $spec.valueType
            delay = '1m'; history = '7d'; trends = '30d'
        }
    }
    if ($toCreate.Count -gt 0) {
        Invoke-Zabbix 'item.create' $toCreate | Out-Null
        $Stats.Items += $toCreate.Count
    }
}

function Ensure-Trigger {
    param([string]$HostId, [string]$TechName, [string]$Name)
    $description = "$Name fora do ar"
    $expression = "max(/$TechName/icmpping,#3)=0"
    # expandExpression=true: a API devolve a expressão real; sem isso ela vem
    # normalizada no formato legado ({itemid}=0) e a comparação nunca casa
    $found = @(Invoke-Zabbix 'trigger.get' @{
        hostids = @($HostId); filter = @{ description = $description }
        output = @('triggerid', 'expression', 'priority', 'status')
        expandExpression = $true
    })
    if ($found.Count -eq 0) {
        # por padrão nasce DESABILITADA (status=1): sem -Enable não há alerta
        $status = if ($null -eq $StatusFlag) { 1 } else { $StatusFlag }
        $created = Invoke-Zabbix 'trigger.create' @{
            description = $description; expression = $expression
            priority = 4; status = $status
            tags = @(@{ tag = 'component'; value = 'telefonia' })
        }
        $Stats.TriggersNew++
        $estado = if ($status -eq 1) { 'DESABILITADA' } else { 'habilitada' }
        Write-Host "    trigger criada ($($created.triggerids[0]), ${estado}): $description"
        return
    }
    $row = $found[0]
    $updates = @{ triggerid = $row.triggerid }
    if ($row.expression -ne $expression) { $updates.expression = $expression }
    if ("$($row.priority)" -ne '4') { $updates.priority = 4 }
    if ($updates.Keys.Count -gt 1) {
        Invoke-Zabbix 'trigger.update' $updates | Out-Null
        $Stats.TriggersUpdated++
    }
    if (($null -ne $StatusFlag) -and ("$($row.status)" -ne "$StatusFlag")) {
        Invoke-Zabbix 'trigger.update' @{ triggerid = $row.triggerid; status = $StatusFlag } | Out-Null
        $Stats.TriggersStatus++
    }
}

# ---------------------------------------------------------------- execução ----
Write-Host "Zabbix: $ApiUrl (user $ApiUser)"
$script:Token = Invoke-Zabbix 'user.login' @{ username = $ApiUser; password = $ApiPass } -NoAuth
Write-Host "versão: $(Invoke-Zabbix 'apiinfo.version' @{} -NoAuth)"

if (-not (Test-Path $DataFile)) { throw "inventário não encontrado: $DataFile" }
$data = Get-Content -Raw -Encoding UTF8 $DataFile | ConvertFrom-Json
$phones = @($data.telefones)
Write-Host "inventário: $(Split-Path $DataFile -Leaf) — $($phones.Count) telefones"

$ips = @($phones | ForEach-Object { $_.ip })
$duplicados = @($ips | Group-Object | Where-Object { $_.Count -gt 1 } | ForEach-Object { $_.Name })
if ($duplicados.Count -gt 0) { throw "IPs duplicados no inventário: $($duplicados -join ', ')" }
$tecnicos = @($phones | ForEach-Object { Get-TechnicalName $_ })
$repetidos = @($tecnicos | Group-Object | Where-Object { $_.Count -gt 1 } | ForEach-Object { $_.Name })
if ($repetidos.Count -gt 0) { throw "hosts técnicos duplicados: $($repetidos -join ', ')" }

$groupid = Get-Group

# colisão de IP com hosts já existentes (câmeras/APs/switches/telefones)
$ipParaHost = @{}
foreach ($h in @(Invoke-Zabbix 'host.get' @{ output = @('hostid', 'host'); selectInterfaces = @('ip') })) {
    foreach ($iface in @($h.interfaces)) {
        if ($iface.ip -and -not $ipParaHost.ContainsKey($iface.ip)) { $ipParaHost[$iface.ip] = $h.host }
    }
}

foreach ($phone in $phones) {
    $tech = Get-TechnicalName $phone
    $name = Get-HostName $phone
    $collision = $ipParaHost[$phone.ip]
    if ($collision -and $collision -ne $tech) {
        Write-Warning "$($phone.ip) já é interface do host '$collision' — $tech NÃO criado"
        $Stats.Skipped++
        continue
    }

    $ramal = "$($phone.ramal)".Trim(); if (-not $ramal) { $ramal = 'n/d' }
    $mac = "$($phone.mac)".Trim();     if (-not $mac) { $mac = 'n/d' }
    $desiredTags = [ordered]@{
        'type' = 'telefone-ip'; 'managed-by' = 'InfraControl'
        'ramal' = $ramal; 'local' = "$($phone.local)"; 'mac' = $mac; 'setor' = "$($phone.setor)"
    }
    $desiredMacros = [ordered]@{
        '{$SETOR}' = "$($phone.setor)"; '{$RAMAL}' = $ramal
        '{$MAC}' = $mac; '{$LOCAL}' = "$($phone.local)"
    }

    $record = Get-ZbxHost $tech
    if (-not $record) {
        $tags = foreach ($k in $desiredTags.Keys) { @{ tag = $k; value = $desiredTags[$k] } }
        $macros = foreach ($k in $desiredMacros.Keys) { @{ macro = $k; value = $desiredMacros[$k] } }
        Invoke-Zabbix 'host.create' @{
            host = $tech; name = $name; status = 0
            groups = @(@{ groupid = $groupid })
            interfaces = @(@{ type = 1; main = 1; useip = 1; ip = $phone.ip; dns = ''; port = $InterfacePort })
            tags = $tags; macros = $macros
        } | Out-Null
        $Stats.HostsNew++
        Write-Host "  host criado: $tech -> $name"
        $record = Get-ZbxHost $tech
    } else {
        $Stats.HostsExisting++
        if ($record.name -ne $name) {
            Invoke-Zabbix 'host.update' @{ hostid = $record.hostid; name = $name } | Out-Null
        }
        Ensure-Interface -HostId $record.hostid -Ip $phone.ip -Interfaces $record.interfaces | Out-Null
        Ensure-HostTags -HostId $record.hostid -Desired $desiredTags -Existing $record.tags
        Ensure-HostMacros -HostId $record.hostid -Desired $desiredMacros -Existing $record.macros
        $record = Get-ZbxHost $tech
    }

    $interfaceid = @($record.interfaces)[0].interfaceid
    if (-not $interfaceid) { throw "$tech sem interface após garantir interface" }
    Ensure-Items -HostId $record.hostid -InterfaceId $interfaceid
    Ensure-Trigger -HostId $record.hostid -TechName $tech -Name $name
}

# ------------------------------------------------------------------ resumo ----
$phoneHosts = @(Invoke-Zabbix 'host.get' @{ groupids = @($groupid); output = @('hostid') })
$nossas = @()
$ativas = 0
if ($phoneHosts.Count -gt 0) {
    $trig = @(Invoke-Zabbix 'trigger.get' @{
        hostids = @($phoneHosts | ForEach-Object { $_.hostid })
        output = @('triggerid', 'description', 'status')
    })
    $nossas = @($trig | Where-Object { $_.description -like '* fora do ar' })
    $ativas = @($nossas | Where-Object { "$($_.status)" -eq '0' }).Count
}

Write-Host ''
Write-Host 'resumo:'
Write-Host "  hosts: novos=$($Stats.HostsNew) existentes=$($Stats.HostsExisting) ignorados(IP em uso)=$($Stats.Skipped) · total no grupo=$($phoneHosts.Count)"
Write-Host "  itens criados: $($Stats.Items)"
Write-Host "  triggers: novas=$($Stats.TriggersNew) atualizadas=$($Stats.TriggersUpdated) status alterado=$($Stats.TriggersStatus) · total=$($nossas.Count) (habilitadas=$ativas desabilitadas=$($nossas.Count - $ativas))"
if ($ativas -gt 0) {
    Write-Warning 'HÁ TRIGGERS HABILITADAS → action 7 (global) → InfraControl → WhatsApp'
}
