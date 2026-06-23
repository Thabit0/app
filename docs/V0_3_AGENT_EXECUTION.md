# v0.3 — محطة النشر بدون API

هذه النسخة تثبت معمارية جهازين بدون API:

- اللابتوب: Manager يجهز ويعتمد المنشورات.
- جهاز المحل: Publisher Agent يراقب مجلد Drive/OneDrive ويعالج المنشورات وقتها.
- الربط: ملفات فقط داخل `DawishSync`.

## ملفات التحكم

كل منشور داخل `jobs/<job-id>` يستخدم:

- `READY.flag`: لا يقرأه جهاز المحل إلا بعد اكتمال نسخ كل الملفات.
- `LOCKED_BY_<device>.lock`: يمنع تنفيذ المنشور مرتين.
- `DONE.flag`: مهمة محطة النشر اكتملت.
- `FAILED.flag`: فشل التنفيذ.
- `NEEDS_REVIEW.flag`: فات الوقت أو يحتاج تدخل.

## مجلدات جديدة

- `plans`: خطة تنفيذ لكل منشور، فيها تعليمات Instagram/TikTok/Snapchat.
- `logs`: سجل يومي لمحطة النشر.
- `errors`: أخطاء مفصلة لكل job.
- `heartbeat`: نبض جهاز المحل.
- `status`: حالة النشر التي يراها اللابتوب.

## TikTok

الإعداد الافتراضي:

- صورة فقط.
- بدون موسيقى.
- بدون صوت.

إذا اخترت fallback لاحقًا، يجب أن يكون فيديو صامت بلا audio track.

## Snapchat

سناب صورة فقط. إذا احتاج تدخل، يكتب Agent الحالة `needs_manual` أو `opened_image_only` ولا يحاول استخدام API.

## تشغيل Agent مرة واحدة

```powershell
DawishContentStudio.Agent.exe --sync "C:\Users\User\OneDrive\DawishSync"
```

## تشغيل Agent دائم

```powershell
DawishContentStudio.Agent.exe --sync "C:\Users\User\OneDrive\DawishSync" --loop --interval 30
```

## فحص جاهزية جهاز المحل

```powershell
DawishContentStudio.Agent.exe --sync "C:\Users\User\OneDrive\DawishSync" --readiness
```

## تثبيت التشغيل مع Windows

بعد إخراج نسخة Release، شغل من مجلد البرنامج:

```powershell
.\scripts\install-agent-startup.ps1 -SyncFolder "C:\Users\User\OneDrive\DawishSync" -AgentExe ".\DawishContentStudio.Agent.exe"
```
