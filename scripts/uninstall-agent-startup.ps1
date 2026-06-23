$taskName = "Dawish Publisher Agent"
if (Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue) {
  Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
  Write-Host "تم حذف مهمة تشغيل Dawish Publisher Agent." -ForegroundColor Green
} else {
  Write-Host "المهمة غير موجودة."
}
