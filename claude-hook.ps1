param([string]$Message = "")

if ([string]::IsNullOrEmpty($Message)) { exit }

$dir = "$env:USERPROFILE\.petdex\events"
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }

$id = "claude-$([System.IO.Path]::GetRandomFileName().Replace('.',''))"
$ts = (Get-Date).ToString('yyyy-MM-ddTHH:mm:ss.fffZ')

# JSON 转义
$escaped = $Message -replace '\\', '\\' -replace '"', '\"'
if ($escaped.Length -gt 500) { $escaped = $escaped.Substring(0, 500) + "..." }

$json = @"
{"id":"$id","timestamp":"$ts","assistant":"claude-code","type":"response","content":"$escaped"}
"@

$path = Join-Path $dir "$id.json"
[System.IO.File]::WriteAllText($path, $json)
