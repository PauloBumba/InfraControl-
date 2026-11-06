#Requires -Version 5.1
<#
  backup-zabbix.ps1 - Backup diario do Zabbix (Sprint 1, docs/65 secao 3.1)

  Gera em backups\zabbix (padrao):
    - db_<timestamp>.sql.gz      pg_dump do banco Zabbix (via container, gzip)
    - config_<timestamp>\        export de configuracao por categoria (JSON)
    - backup.log                 log de execucoes

  Modo versionado (para Git):
    .\backup-zabbix.ps1 -Versioned
    grava zabbix-config\export\{hosts,templates,media-types}.json + actions.json
    com nomes estaveis (diff limpo no Git).

  API Zabbix 8.0 (gotchas, confirmados ao vivo 2026-11-04):
    - autenticacao SOMENTE via header "Authorization: Bearer <token>";
      campo top-level "auth" e rejeitado (-32600 "unexpected parameter auth").
    - configuration.export exige arrays em options (true e rejeitado) e aceita
      somente hosts/templates/mediaTypes; itens/triggers/graphs sao exportados
      junto com hosts e templates. actions nao existe - vem de action.get.

  Credenciais da API: env ZABBIX_API_USER / ZABBIX_API_PASSWORD
  (padrao Admin/zabbix - credencial default do Zabbix).

  Saida: 0 = sucesso, 1 = falha.
#>
param(
    [string]$ZabbixUrl = "http://localhost:8086",
    [string]$BackupDir = "",
    [string]$VersionedDir = "",
    [string]$DbContainer = "infracontrol-zabbix-db",
    [string]$DbName = "zabbix",
    [string]$DbUser = "zabbix",
    [int]$RetentionDays = 7,
    [switch]$Versioned
)

$ErrorActionPreference = "Stop"

$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
if (-not $BackupDir) { $BackupDir = Join-Path $repoRoot "backups\zabbix" }
if (-not $VersionedDir) { $VersionedDir = Join-Path $repoRoot "zabbix-config" }

$apiUser = if ($env:ZABBIX_API_USER) { $env:ZABBIX_API_USER } else { "Admin" }
$apiPass = if ($env:ZABBIX_API_PASSWORD) { $env:ZABBIX_API_PASSWORD } else { "zabbix" }

function Log {
    param([string]$Message)
    $line = "[{0}] {1}" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss"), $Message
    Write-Host $line
    if ($script:logFile) { Add-Content -Path $script:logFile -Value $line -Encoding UTF8 }
}

# --------------------------------------------------------------- curl ------
# O Invoke-RestMethod do PS 5.1 trava/da 500 em exports grandes (confirmado
# ao vivo 2026-11-04: combinado = HTTP 500; curl = 1,6s). Todo request grosso
# passa por curl.exe com payload em arquivo (evita mangling de aspas).
function Invoke-ZabbixCurl {
    param([string]$PayloadJson, [string]$OutFile, [int]$TimeoutSec = 600)
    $reqFile = Join-Path $env:TEMP ("zbx-req-" + [Guid]::NewGuid().ToString("N") + ".json")
    [IO.File]::WriteAllText($reqFile, $PayloadJson, (New-Object Text.UTF8Encoding($false)))
    try {
        $headers = @()
        if ($script:apiToken) { $headers = @("-H", "Authorization: Bearer $($script:apiToken)") }
        & curl.exe -s --max-time $TimeoutSec -X POST "$script:ZabbixUrl/api_jsonrpc.php" `
            -H "Content-Type: application/json" @headers `
            --data-binary "@$reqFile" -o $OutFile
        if ($LASTEXITCODE -ne 0) { throw "curl exit $LASTEXITCODE (timeout/sem resposta)" }
        $resp = [Text.Encoding]::UTF8.GetString([IO.File]::ReadAllBytes($OutFile)) | ConvertFrom-Json
        if ($resp.error) {
            throw ("Zabbix API error {0}: {1} {2}" -f $resp.error.code, $resp.error.message, $resp.error.data)
        }
        return $resp.result
    }
    finally {
        Remove-Item $reqFile -Force -ErrorAction SilentlyContinue
    }
}

# ---------------------------------------------------------------- login ----
$script:ZabbixUrl = $ZabbixUrl.TrimEnd("/")
$script:apiToken = $null
$script:logFile = $null

try {
    $loginBody = '{"jsonrpc":"2.0","method":"user.login","params":{"username":"' + $apiUser + '","password":"' + $apiPass + '"},"id":1}'
    $loginFile = Join-Path $env:TEMP "zbx-login-tmp.json"
    $script:apiToken = Invoke-ZabbixCurl -PayloadJson $loginBody -OutFile $loginFile -TimeoutSec 30
    Log "Login OK em $ZabbixUrl (usuario $apiUser)"
}
catch {
    Write-Host "[ERRO] Login falhou: $($_.Exception.Message)"
    exit 1
}

# ------------------------------------------------------- export config ----
# A API 8.0 rejeita o export combinado grande (HTTP 500) e exige arrays.
# Exportamos por categoria, uma chamada cada - tambem gera diffs menores no Git.
function Get-IdList {
    param([string]$Method, [string]$IdField)
    $tmp = Join-Path $env:TEMP ("zbx-ids-" + [Guid]::NewGuid().ToString("N") + ".json")
    $payload = '{"jsonrpc":"2.0","method":"' + $Method + '","params":{"output":["' + $IdField + '"]},"id":1}'
    $result = Invoke-ZabbixCurl -PayloadJson $payload -OutFile $tmp -TimeoutSec 120
    Remove-Item $tmp -Force -ErrorAction SilentlyContinue
    $ids = @($result | ForEach-Object { $_.$IdField })
    return ($ids | ForEach-Object { '"' + $_ + '"' }) -join ','
}

function Export-One {
    param([string]$Name, [string]$OptionsJson, [string]$OutFile)
    $payload = '{"jsonrpc":"2.0","method":"configuration.export","params":{"format":"json","options":' + $OptionsJson + '},"id":1}'
    $result = Invoke-ZabbixCurl -PayloadJson $payload -OutFile $OutFile -TimeoutSec 600
    if ($result -isnot [string] -or [string]::IsNullOrWhiteSpace($result)) {
        throw "configuration.export($Name) nao retornou string JSON."
    }
    $result
}

# Export grande estoura o memory_limit de 128MB do PHP (392 templates = 500 em
# gatherItems, confirmado 2026-11-04). Exporta em lotes de 50 IDs e mescla os
# objetos zabbix_export (arrays concatenados por chave).
function Export-Batched {
    param([string]$Name, [string]$IdListCsv, [string]$OutFile)
    $ids = @($IdListCsv -split ',' | Where-Object { $_ -replace '"', '' })
    if ($ids.Count -eq 0) { throw "Export-Batched($Name): lista de IDs vazia." }
    $batchSize = 50
    $merged = $null
    for ($i = 0; $i -lt $ids.Count; $i += $batchSize) {
        $end = [Math]::Min($i + $batchSize - 1, $ids.Count - 1)
        $batch = $ids[$i..$end] | ForEach-Object { '"' + ($_ -replace '"', '') + '"' }
        $options = '{"' + $Name + '":[' + ($batch -join ',') + ']}'
        $json = Export-One -Name $Name -OptionsJson $options -OutFile $OutFile
        Remove-Item $OutFile -Force -ErrorAction SilentlyContinue
        $obj = $json | ConvertFrom-Json
        if ($null -eq $merged) {
            $merged = $obj.zabbix_export.PSObject.Copy()
        }
        else {
            foreach ($prop in $obj.zabbix_export.PSObject.Properties) {
                $cur = $merged.PSObject.Properties[$prop.Name]
                if ($null -eq $cur) {
                    $merged | Add-Member -NotePropertyName $prop.Name -NotePropertyValue $prop.Value
                }
                elseif ($prop.Value -is [System.Array] -and $cur.Value -is [System.Array]) {
                    $merged.$($prop.Name) = @($cur.Value) + @($prop.Value)
                }
            }
        }
    }
    return ($merged | ConvertTo-Json -Depth 100)
}

# actions nao existe em configuration.export (8.0) - captura via action.get.
function Export-Actions {
    param([string]$OutFile)
    $payload = '{"jsonrpc":"2.0","method":"action.get","params":{"output":"extend","selectOperations":"extend","selectRecoveryOperations":"extend","selectConditions":"extend"},"id":1}'
    $result = Invoke-ZabbixCurl -PayloadJson $payload -OutFile $OutFile -TimeoutSec 300
    ($result | ConvertTo-Json -Depth 20)
}

function Write-Utf8 {
    param([string]$Path, [string]$Content)
    $dir = Split-Path -Parent $Path
    if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    [IO.File]::WriteAllText($Path, $Content, (New-Object System.Text.UTF8Encoding($false)))
}

# ------------------------------------------------------------- versioned ---
if ($Versioned) {
    try {
        $hostIds = Get-IdList "host.get" "hostid"
        $tplIds = Get-IdList "template.get" "templateid"
        $mtIds = Get-IdList "mediatype.get" "mediatypeid"
        Log ("Export: hosts=[{0}] templates=[{1}] mediaTypes=[{2}] (contagens via listagem)" -f `
            ($hostIds -split ',' | Where-Object { $_ }).Count, `
            ($tplIds -split ',' | Where-Object { $_ }).Count, `
            ($mtIds -split ',' | Where-Object { $_ }).Count)

        $expDir = Join-Path $VersionedDir "export"
        New-Item -ItemType Directory -Path $expDir -Force | Out-Null

        $parts = @(
            @{ n = "hosts.json"; k = "hosts"; ids = $hostIds },
            @{ n = "templates.json"; k = "templates"; ids = $tplIds },
            @{ n = "media-types.json"; k = "mediaTypes"; ids = $mtIds }
        )
        foreach ($p in $parts) {
            $json = Export-Batched -Name $p.k -IdListCsv $p.ids -OutFile (Join-Path $expDir "tmp-$($p.n)")
            Write-Utf8 (Join-Path $expDir $p.n) $json
            $sz = (Get-Item (Join-Path $expDir $p.n)).Length
            Log ("Versionado: export/{0} ({1:N0} bytes)" -f $p.n, $sz)
        }

        $actionsJson = Export-Actions -OutFile (Join-Path $env:TEMP "zbx-tmp-actions.json")
        Remove-Item (Join-Path $env:TEMP "zbx-tmp-actions.json") -Force -ErrorAction SilentlyContinue
        Write-Utf8 (Join-Path $VersionedDir "actions.json") $actionsJson
        Log ("Versionado: actions.json ({0:N0} bytes)" -f (Get-Item (Join-Path $VersionedDir "actions.json")).Length)
        Log "Varredura de segredos obrigatoria antes do commit (zabbix-config/README.md)."
        exit 0
    }
    catch {
        Write-Host "[ERRO] Export versionado falhou: $($_.Exception.Message)"
        exit 1
    }
}

# --------------------------------------------------------------- backup ----
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
New-Item -ItemType Directory -Path $BackupDir -Force | Out-Null
$script:logFile = Join-Path $BackupDir "backup.log"

try {
    # 1. Export de configuracao por categoria (rollback leve)
    $cfgDir = Join-Path $BackupDir "config_$timestamp"
    New-Item -ItemType Directory -Path $cfgDir -Force | Out-Null
    $hostIds = Get-IdList "host.get" "hostid"
    $tplIds = Get-IdList "template.get" "templateid"
    $mtIds = Get-IdList "mediatype.get" "mediatypeid"
    $parts = @(
        @{ n = "hosts.json"; k = "hosts"; ids = $hostIds },
        @{ n = "templates.json"; k = "templates"; ids = $tplIds },
        @{ n = "media-types.json"; k = "mediaTypes"; ids = $mtIds }
    )
    foreach ($p in $parts) {
        $json = Export-Batched -Name $p.k -IdListCsv $p.ids -OutFile (Join-Path $cfgDir "tmp-$($p.n)")
        Write-Utf8 (Join-Path $cfgDir $p.n) $json
    }
    $actionsJson = Export-Actions -OutFile (Join-Path $env:TEMP "zbx-tmp-actions.json")
    Remove-Item (Join-Path $env:TEMP "zbx-tmp-actions.json") -Force -ErrorAction SilentlyContinue
    Write-Utf8 (Join-Path $cfgDir "actions.json") $actionsJson
    $cfgSize = (Get-Item (Join-Path $cfgDir "hosts.json")).Length
    Log ("Config: {0} (hosts.json {1:N0} bytes + templates/media-types/actions)" -f $cfgDir, $cfgSize)

    # 2. Dump do banco via container (gzip la dentro; docker cp evita corromper
    #    encoding no redirecionamento PowerShell de texto grande)
    #    ATENCAO: nao usar $dbName - PowerShell e case-insensitive e sobrescreve
    #    o param $DbName (bug real encontrado em 2026-11-04).
    $tmpDumpName = "zabbix_backup.sql.gz"
    $containerTmp = "/tmp/$tmpDumpName"
    Log "pg_dump iniciado (container $DbContainer)..."
    docker exec $DbContainer sh -c "pg_dump -U $DbUser $DbName | gzip -c > $containerTmp && gzip -t $containerTmp"
    if ($LASTEXITCODE -ne 0) { throw "pg_dump/gzip falhou (exit $LASTEXITCODE)." }

    $dumpPath = Join-Path $BackupDir "db_$timestamp.sql.gz"
    docker cp "${DbContainer}:$containerTmp" $dumpPath
    if ($LASTEXITCODE -ne 0) { throw "docker cp falhou (exit $LASTEXITCODE)." }
    docker exec $DbContainer rm -f $containerTmp | Out-Null

    $dumpSize = (Get-Item $dumpPath).Length
    if ($dumpSize -lt 1MB) { throw "Dump suspeito ($dumpSize bytes) - abortando." }
    $dumpGB = $dumpSize / 1GB
    Log ("Dump: {0} ({1:N2} GB)" -f $dumpPath, $dumpGB)

    # 3. Retencao (7 dias) - cobre dumps (.sql/.sql.gz) e diretorios config_*
    $cutoff = (Get-Date).AddDays(-$RetentionDays)
    Get-ChildItem -Path $BackupDir -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match "^(db_|config_)" -and $_.LastWriteTime -lt $cutoff } |
        ForEach-Object { Remove-Item $_.FullName -Recurse -Force; Log "Retencao: removido $($_.Name)" }

    Log "Backup concluido com sucesso."
    exit 0
}
catch {
    Write-Host "[ERRO] Backup falhou: $($_.Exception.Message)"
    Log "[ERRO] Backup falhou."
    exit 1
}
