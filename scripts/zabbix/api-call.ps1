param(
    [Parameter(Mandatory=$true)][string]$Method,
    [Parameter(Mandatory=$true)][string]$ParamsJsonFile,
    [string]$OutFile
)
$ErrorActionPreference = 'Stop'
$api = 'http://localhost:8086/api_jsonrpc.php'
$tmp = Join-Path $env:TEMP ('zbx-api-' + [guid]::NewGuid().ToString('N') + '.json')

function Send-Api([hashtable]$req) {
    [IO.File]::WriteAllText($tmp, ($req | ConvertTo-Json -Depth 10), [Text.UTF8Encoding]::new($false))
    $resp = curl.exe -s -X POST $api -H 'Content-Type: application/json' -H "Authorization: Bearer $script:token" --data-binary "@$tmp"
    return ($resp | ConvertFrom-Json)
}

$login = @{ jsonrpc='2.0'; method='user.login'; params=@{ username='Admin'; password='zabbix' }; id=1 }
[IO.File]::WriteAllText($tmp, ($login | ConvertTo-Json -Depth 5), [Text.UTF8Encoding]::new($false))
$resp = curl.exe -s -X POST $api -H 'Content-Type: application/json' --data-binary "@$tmp" | ConvertFrom-Json
if (-not $resp.result) { throw "login falhou: $($resp | ConvertTo-Json -Compress)" }
$script:token = $resp.result
Write-Output "login OK"

$params = Get-Content -Raw -Encoding UTF8 $ParamsJsonFile | ConvertFrom-Json
$req = @{ jsonrpc='2.0'; method=$Method; params=$params; id=2 }
$res = Send-Api $req
$json = $res | ConvertTo-Json -Depth 10
if ($OutFile) { [IO.File]::WriteAllText($OutFile, $json, [Text.UTF8Encoding]::new($false)) }
Write-Output $json
Remove-Item $tmp -ErrorAction SilentlyContinue
