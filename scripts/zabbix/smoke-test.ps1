#Requires -Version 5.1
<#
  smoke-test.ps1 - Validacao rapida do Zabbix (Sprint 1, docs/65 secao 4.2)

  Checagens (todas via API JSON-RPC 8.0, Bearer token):
    1. Login (credencial de API)
    2. Web responde em /ping
    3. Acao 7 "InfraControl - triggers" existe e esta Enabled
    4. Media types 105 (Problem) e 106 (Recovery) existem e estao Enabled
    5. Queue zabbix[queue,10m] < 10
    6. Utilizacao do ICMP pinger < 75% (item zabbix[process,icmp pinger,avg,busy])

  Uso:
    .\smoke-test.ps1                    # localhost:8086
    .\smoke-test.ps1 -ZabbixUrl http://host:8086

  Saida: 0 = todos os checks OK, 1 = ao menos 1 falhou.
  Credenciais: env ZABBIX_API_USER / ZABBIX_API_PASSWORD (padrao Admin/zabbix).
#>
param(
    [string]$ZabbixUrl = "http://localhost:8086",
    [int]$MaxQueue = 10,
    [int]$MaxPingerBusyPct = 75
)

$ErrorActionPreference = "Stop"
$script:ZabbixUrl = $ZabbixUrl.TrimEnd("/")
$script:apiToken = $null
$script:failures = 0

function Check {
    param([string]$Name, [bool]$Ok, [string]$Detail)
    if ($Ok) {
        Write-Host ("[PASS] {0} - {1}" -f $Name, $Detail)
    }
    else {
        Write-Host ("[FAIL] {0} - {1}" -f $Name, $Detail)
        $script:failures++
    }
}

function Invoke-Zabbix {
    param([hashtable]$Payload, [int]$TimeoutSec = 60)
    $json = $Payload | ConvertTo-Json -Depth 20
    $params = @{ Uri = "$script:ZabbixUrl/api_jsonrpc.php"; Method = "Post"
                 ContentType = "application/json"; Body = $json; TimeoutSec = $TimeoutSec }
    if ($script:apiToken) { $params.Headers = @{ Authorization = "Bearer $($script:apiToken)" } }
    $resp = Invoke-RestMethod @params
    if ($resp.error) {
        throw ("Zabbix API error {0}: {1} {2}" -f $resp.error.code, $resp.error.message, $resp.error.data)
    }
    return $resp.result
}

# 1. Login
$apiUser = if ($env:ZABBIX_API_USER) { $env:ZABBIX_API_USER } else { "Admin" }
$apiPass = if ($env:ZABBIX_API_PASSWORD) { $env:ZABBIX_API_PASSWORD } else { "zabbix" }
try {
    $script:apiToken = Invoke-Zabbix @{ jsonrpc = "2.0"; method = "user.login"
                                       params = @{ username = $apiUser; password = $apiPass }; id = 1 }
    Check "Login API" $true "usuario $apiUser autenticado"
}
catch {
    Check "Login API" $false $_.Exception.Message
    exit 1
}

# 2. Web responde
try {
    $ping = Invoke-WebRequest -Uri "$script:ZabbixUrl/ping" -TimeoutSec 15 -UseBasicParsing
    Check "Web /ping" ($ping.StatusCode -eq 200) "HTTP $($ping.StatusCode)"
}
catch {
    Check "Web /ping" $false $_.Exception.Message
}

# 3. Acao 7 InfraControl
try {
    $actions = Invoke-Zabbix @{ jsonrpc = "2.0"; method = "action.get"
        params = @{ output = @("actionid", "name", "status")
                    filter = @{ actionid = "7" }; limit = 1 }; id = 2 }
    $a7 = @($actions)
    if ($a7.Count -eq 1) {
        Check "Acao 7 InfraControl" ($a7[0].status -eq "0") ("id=7 status=$($a7[0].status) (0=enabled)")
    }
    else {
        Check "Acao 7 InfraControl" $false "acao id=7 nao encontrada"
    }
}
catch {
    Check "Acao 7 InfraControl" $false $_.Exception.Message
}

# 4. Media types 105/106
try {
    $mts = Invoke-Zabbix @{ jsonrpc = "2.0"; method = "mediatype.get"
        params = @{ output = @("mediatypeid", "name", "status")
                    mediatypeids = @("105", "106") }; id = 3 }
    $found = @($mts)
    $enabled = @($found | Where-Object { $_.status -eq "0" })
    Check "Media types 105/106" ($found.Count -eq 2 -and $enabled.Count -eq 2) ("{0}/2 encontradas, {1}/2 enabled" -f $found.Count, $enabled.Count)
}
catch {
    Check "Media types 105/106" $false $_.Exception.Message
}

# 5. Queue < MaxQueue
try {
    $q = Invoke-Zabbix @{ jsonrpc = "2.0"; method = "item.get"
        params = @{ output = @("key_", "lastvalue")
                    filter = @{ key_ = "zabbix[queue,10m]" }; limit = 1 }; id = 4 }
    $qItems = @($q)
    if ($qItems.Count -eq 0) {
        Check "Queue < $MaxQueue" $false "item zabbix[queue,10m] nao encontrado"
    }
    else {
        $qv = [int]$qItems[0].lastvalue
        Check "Queue < $MaxQueue" ($qv -lt $MaxQueue) "queue=$qv"
    }
}
catch {
    Check "Queue < $MaxQueue" $false $_.Exception.Message
}

# 6. ICMP pinger busy < MaxPingerBusyPct
try {
    $p = Invoke-Zabbix @{ jsonrpc = "2.0"; method = "item.get"
        params = @{ output = @("key_", "lastvalue")
                    search = @{ key_ = "process,icmp pinger,avg,busy" }; limit = 20 }; id = 5 }
    $pItems = @($p)
    if ($pItems.Count -eq 0) {
        Check "ICMP pinger < $MaxPingerBusyPct%" $false "item de pinger nao encontrado"
    }
    else {
        $vals = @($pItems | ForEach-Object { [double]($_.lastvalue -replace ',', '.') })
        $max = ($vals | Measure-Object -Maximum).Maximum
        Check "ICMP pinger < $MaxPingerBusyPct%" ($max -lt $MaxPingerBusyPct) ("{0} amostras, max busy={1}%" -f $pItems.Count, $max)
    }
}
catch {
    Check "ICMP pinger < $MaxPingerBusyPct%" $false $_.Exception.Message
}

Write-Host ""
if ($script:failures -eq 0) {
    Write-Host "SMOKE TEST OK - 6/6 checks passaram."
    exit 0
}
Write-Host ("SMOKE TEST FALHOU - {0} check(s) com erro." -f $script:failures)
exit 1
