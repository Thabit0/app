# DawishSync Schema

```text
DawishSync/
  jobs/
    job_YYYYMMDD_HHMMSS_slug/
      post.json
      image_original.jpg
      instagram_caption.txt
      tiktok_caption.txt
      snapchat_caption.txt
      READY.flag
      LOCKED_BY_<device>.lock
      DONE.flag
      FAILED.flag
  status/
    job_...status.json
  heartbeat/
    SHOP-PC.json
  logs/
  errors/
  screenshots/
  archive/
  settings/
```

## قواعد السلامة

- لا يقرأ Agent أي منشور بدون `READY.flag`.
- عند التنفيذ يكتب Lock لمنع التكرار.
- بعد النجاح يكتب DONE.
- بعد الفشل يكتب FAILED مع status مفصل.
