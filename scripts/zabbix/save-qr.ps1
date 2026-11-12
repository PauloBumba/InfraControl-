param([string]$JsonPath, [string]$OutPng)
$j = Get-Content -Raw $JsonPath | ConvertFrom-Json
$b64 = $j.base64 -replace '^data:image/png;base64,', ''
[IO.File]::WriteAllBytes($OutPng, [Convert]::FromBase64String($b64))
Write-Output "saved $OutPng ($((Get-Item $OutPng).Length) bytes)"
