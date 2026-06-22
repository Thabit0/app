param([Parameter(Mandatory=$true)][string]$SyncFolder)
$folders = @('jobs','status','heartbeat','logs','errors','screenshots','plans','archive','settings')
New-Item -ItemType Directory -Path $SyncFolder -Force | Out-Null
foreach ($f in $folders) { New-Item -ItemType Directory -Path (Join-Path $SyncFolder $f) -Force | Out-Null }
@{
  createdAt = (Get-Date).ToString('o')
  note = 'DawishSync folder initialized. No API. Drive/OneDrive sync is handled outside the app.'
} | ConvertTo-Json | Set-Content -Encoding UTF8 (Join-Path $SyncFolder 'settings\sync-info.json')
Write-Host "DawishSync جاهز: $SyncFolder" -ForegroundColor Green
