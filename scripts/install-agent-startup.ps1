param(
  [Parameter(Mandatory=$true)][string]$SyncFolder,
  [string]$AgentExe = ".\\DawishContentStudio.Agent.exe",
  [int]$IntervalSeconds = 30
)

$taskName = "Dawish Publisher Agent"
$fullAgent = Resolve-Path $AgentExe
$action = New-ScheduledTaskAction -Execute $fullAgent -Argument "--sync `"$SyncFolder`" --loop --interval $IntervalSeconds"
$trigger = New-ScheduledTaskTrigger -AtLogOn
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable
Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Settings $settings -Force | Out-Null
Write-Host "تم تثبيت تشغيل Dawish Publisher Agent مع Windows." -ForegroundColor Green
Write-Host "SyncFolder: $SyncFolder"
