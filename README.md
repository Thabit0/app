# Dawish Content Studio Windows No API v0.4.2

إصلاح بناء GitHub Actions: تم تحديد أحداث WPF بشكل صريح مثل `WpfControls.SelectionChangedEventArgs` لتفادي تعارض Windows Forms/WPF.

# Dawish Content Studio Windows — No API v0.4.2

برنامج ويندوز Native لإدارة محتوى الدويش بين جهازين بدون API.

## الفكرة

- لابتوبك: **Manager** لتجهيز الصور والكابشن والجدولة.
- جهاز المحل: **Publisher Agent** يراقب مجلد المزامنة وينفذ المنشورات عند وقتها.
- الربط بين الجهازين: مجلد عادي داخل OneDrive / Google Drive / Syncthing / شبكة محلية.
- لا يوجد Instagram API ولا TikTok API ولا Snapchat API.

## أهم ما في v0.4.2

- Publisher Agent فعلي أكثر.
- صفحة Publish Assistant لكل منشور.
- فتح المنصات تلقائيًا من جهاز المحل.
- نسخ كابشن Instagram للحافظة.
- TikTok صورة فقط افتراضيًا وبدون صوت.
- Snapchat صورة فقط.
- منع Sleep أثناء تشغيل Agent.
- `AWAITING_CONFIRMATION.flag` بدل تعليم المنشور Done مباشرة.
- أوامر `--mark-done` و `--mark-failed`.
- تعديل جماعي موجود: كل صورة تنزل كمنشور مستقل.
- مراقبة جهاز المحل عبر heartbeat/status.

## تشغيل Agent على جهاز المحل

```powershell
DawishContentStudio.Agent.exe --sync "C:\Users\User\OneDrive\DawishSync" --init-settings
DawishContentStudio.Agent.exe --sync "C:\Users\User\OneDrive\DawishSync" --readiness
DawishContentStudio.Agent.exe --sync "C:\Users\User\OneDrive\DawishSync" --loop --interval 30
```

بعد ما تنشر فعليًا من المتصفح، علم المنشور تم:

```powershell
DawishContentStudio.Agent.exe --sync "C:\Users\User\OneDrive\DawishSync" --mark-done job_id
```

أو من شاشة Manager: اختر حالة المنشور ثم اضغط **تعليم المحدد تم**.

## بناء GitHub Actions

ارفع المشروع إلى GitHub ثم شغل Workflow:

- Validate
- Windows Release

سيطلع لك Manager وAgent كملفات Windows.


## v0.4.2 Build Fix

تم إصلاح تعارض أسماء WPF/Windows Forms في ملفات Manager:
- `App.xaml.cs`: استخدام `System.Windows.Application` صراحة.
- `MainWindow.xaml.cs`: استخدام alias لعناصر WPF Controls.

هذا يعالج أخطاء GitHub Actions:
- `Application is an ambiguous reference`
- `ComboBox is an ambiguous reference`
