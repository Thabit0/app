# إعداد مرحلة Cloudflare

نفّذ Migration على قاعدة D1 الحالية قبل نشر `worker.js`:

```powershell
npx wrangler d1 execute dawish_content_db --remote --file cloudflare/migrations/0002_platform_claims_retention.sql
```

بعدها انشر Worker الحالي مع الحفاظ على Bindings التالية:

- D1 binding: `DB` إلى `dawish_content_db`
- R2 binding: `MEDIA` إلى `dawish-content-media`
- Secrets: `ADMIN_TOKEN` و`SHOP_TOKEN`

لتنظيف R2 تلقائيًا، أضف Cron Trigger يوميًا إلى Worker، مثل:

```text
0 3 * * *
```

يمكن اختبار التنظيف يدويًا بواسطة `POST /v1/admin/cleanup` باستخدام `ADMIN_TOKEN`.

لا يَحذف التنظيف بيانات المنشور أو سجل الأحداث من D1؛ يحذف ملف الصورة من R2 فقط بعد 30 يومًا.
