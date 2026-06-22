# v0.4 — Publisher Agent الحقيقي بدون API

هذه النسخة تكمل اتجاه المشروع الصحيح:

- Windows Native فقط.
- بدون API نهائيًا.
- بدون Electron / npm / Playwright.
- لابتوبك يجهز jobs في مجلد المزامنة.
- جهاز المحل يشغل Publisher Agent ويقرأ jobs وينفذها عند وقتها.

## أهم الجديد

1. **Publish Assistant Page**
   - لكل job يتم إنشاء `publish-assistant.html` داخل مجلد المنشور.
   - الصفحة تعرض الصورة، الكابشنات، روابط المنصات، وتنبيهات TikTok/Snapchat.

2. **فتح المنصات تلقائيًا**
   - يفتح Instagram.
   - يفتح TikTok Upload.
   - يفتح Snapchat Web.
   - بدون API وبدون ربط رسمي.

3. **TikTok image-first**
   - الوضع الافتراضي: صورة فقط.
   - لا يتم إضافة موسيقى ولا صوت.
   - fallback الفيديو الصامت اختياري فقط حسب إعداد job.

4. **Snapchat image-only**
   - سناب يبقى صورة فقط.

5. **Manual Done Confirmation**
   - بعد فتح المنصات وتجهيز النشر، يتم إنشاء `AWAITING_CONFIRMATION.flag`.
   - لا يتم اعتبار المنشور DONE إلا إذا علمته يدويًا بعد النشر.

6. **منع Sleep**
   - Agent يستخدم Windows execution state أثناء التشغيل حتى يقلل دخول الجهاز في Sleep.

7. **أوامر التحكم**

```powershell
DawishContentStudio.Agent.exe --sync "C:\Users\User\OneDrive\DawishSync" --loop
DawishContentStudio.Agent.exe --sync "C:\Users\User\OneDrive\DawishSync" --readiness
DawishContentStudio.Agent.exe --sync "C:\Users\User\OneDrive\DawishSync" --mark-done job_id
DawishContentStudio.Agent.exe --sync "C:\Users\User\OneDrive\DawishSync" --mark-failed job_id --reason "login required"
```

## ملاحظة مهمة

v0.4 لا تستخدم API ولا تدعي أن المنصات تسمح بالنشر النهائي دائمًا. إذا طلبت المنصة تحقق أو تغيرت الواجهة، يحولها النظام إلى مراجعة يدوية بدل ما ينشر شيء خطأ.
