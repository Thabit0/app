# المعمارية بدون API

## التدفق

```text
لابتوبك / Manager
  يجهز الصور والكابشنات والجدولة
  يكتب Job كامل داخل DawishSync
  يكتب READY.flag بعد اكتمال كل الملفات

Drive/OneDrive/Syncthing
  يزامن الملفات فقط

جهاز المحل / Publisher Agent
  يراقب DawishSync/jobs
  لا يقرأ إلا Jobs فيها READY.flag
  يعمل LOCK
  يفتح المنصات أو يجهز النشر
  يكتب status و DONE/FAILED

لابتوبك
  يقرأ status و heartbeat
```

لا يوجد API مع المنصات أو الدرايف.
