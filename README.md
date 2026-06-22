# Dawish Content Studio Windows

برنامج ويندوز لإدارة محتوى حسابات المحل **بدون API نهائيًا**.

## المعمارية المعتمدة

- **Manager** على لابتوبك: يجهز الصور، الكابشنات، الجدولة، المعاينة، والاعتماد.
- **Publisher Agent** على جهاز المحل: يراقب مجلد المزامنة وينفذ المنشورات عند وقتها.
- الربط بين الجهازين يتم بملفات داخل مجلد مزامنة مثل OneDrive / Google Drive / Syncthing / مجلد شبكة.
- البرنامج لا يستخدم Instagram API ولا TikTok API ولا Snapchat API ولا Google Drive API.

## أهم الميزات في هذه النسخة

- مجلد مزامنة منظم: jobs / status / heartbeat / logs / errors / screenshots / archive.
- نظام READY / LOCK / DONE / FAILED لمنع قراءة ملفات ناقصة أو تنفيذ مكرر.
- Heartbeat لجهاز المحل عشان تعرف هل هو متصل.
- إنشاء منشور واحد أو مجموعة صور.
- تحديد عدة صور وتعديلها مع بعض.
- جدولة جماعية: كل صورة تنزل كمنشور مستقل.
- TikTok mode:
  - صورة فقط.
  - صورة أولًا ثم فيديو صامت احتياطي.
  - فيديو صامت فقط.
- الافتراضي: TikTok صورة فقط.
- Snapchat صورة فقط.
- منع الادعاءات الطبية والتلميحات العلاجية.
- مركز اعتماد قبل إرسال المنشورات لمجلد المزامنة.
- Publisher Agent Console كبداية لجهاز المحل.
- GitHub Actions لبناء Manager وAgent كملفات Windows.

## تشغيل المطور

```powershell
dotnet restore DawishContentStudio.sln
dotnet run --project src/DawishContentStudio.Manager/DawishContentStudio.Manager.csproj
```

تشغيل جهاز المحل كـ Agent:

```powershell
dotnet run --project src/DawishContentStudio.Agent/DawishContentStudio.Agent.csproj -- --sync "C:\Users\YOUR_USER\OneDrive\DawishSync" --loop
```

## إنشاء نسخة Windows من GitHub

1. ارفع المشروع إلى GitHub.
2. افتح Actions.
3. شغل Workflow باسم `Windows Release`.
4. سيظهر Artifact يحتوي:
   - DawishContentStudio.Manager
   - DawishContentStudio.Agent

## ملاحظات مهمة

- النشر يتم بدون API عبر Browser Automation/فتح المنصات من جهاز المحل، مع تسجيل الحالة.
- أول مرحلة تجعل جهاز المحل يفتح المنصة ويجهز المنشور ويكتب الحالة. الضغط التلقائي الكامل على زر النشر يضاف لاحقًا بحذر بعد اختبار الحسابات.
- لو المنصة طلبت تحقق، Agent يسجل `needs_manual` بدل محاولة تجاوز التحقق.
