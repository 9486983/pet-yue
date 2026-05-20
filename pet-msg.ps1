param([string]$Message = "Hello from pet!")

$dir = "$env:USERPROFILE\.petdex\events"
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }

$id = "msg-$([System.IO.Path]::GetRandomFileName().Replace('.',''))"
$ts = (Get-Date).ToString('yyyy-MM-ddTHH:mm:ss.fffZ')

# JSON 转义：把内容里的引号和反斜线转义
$escaped = $Message -replace '\\', '\\' -replace '"', '\"'

$json = @"
{"id":"$id","timestamp":"$ts","assistant":"claude-code","type":"response","content":"$escaped"}
"@

$path = Join-Path $dir "$id.json"
[System.IO.File]::WriteAllText($path, $json)
Write-Host "✅ Sent to pet: $Message"
