param(
  [string]$Sync = "$env:USERPROFILE\OneDrive\DawishSync",
  [int]$Interval = 30
)
$agent = Join-Path $PSScriptRoot "..\release\Agent\DawishContentStudio.Agent.exe"
if (!(Test-Path $agent)) { $agent = "DawishContentStudio.Agent.exe" }
& $agent --sync $Sync --loop --interval $Interval
