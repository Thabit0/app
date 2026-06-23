# v0.8 Cloudflare Easy

هذه النسخة تلغي مجلد المزامنة المحلي/الدرايف وتستخدم Cloudflare كمركز تبادل بيانات.

## تغييرات مهمة

- واجهة مبسطة جدًا.
- شاشة أول تشغيل: أنا المدير / هذا جهاز المحل.
- حذف العنوان.
- الكابشن هو الأساس.
- لوكيشن منفصل لكل منصة.
- الوقت بنظام 12 ساعة.
- رفع صور متعددة.
- كل صورة منشور مستقل.
- جهاز المحل يسحب من Cloudflare.
- TikTok صورة فقط وبدون صوت.
- Snapchat صورة فقط.

## Cloudflare Endpoints

- `GET /health`
- `POST /v1/admin/posts`
- `GET /v1/admin/posts`
- `GET /v1/shop/due`
- `POST /v1/shop/heartbeat`
- `GET /v1/media/:key`
- `POST /v1/shop/posts/:id/result`

## الأمان

- جهاز المدير يستخدم `ADMIN_TOKEN`.
- جهاز المحل يستخدم `SHOP_TOKEN`.
- R2 لا يكون Public.
- الصور تُقرأ عبر Worker فقط.


## v0.8.1 Review Fix

- Worker لا يعيد نفس المنشور كـ due بعد أن يسجل جهاز المحل `assistant_opened`.
- Worker يفحص الكابشن ضد الادعاءات الطبية أيضًا.
- Worker يفرض TikTok صورة فقط وSnapchat صورة فقط من جهة السيرفر.
