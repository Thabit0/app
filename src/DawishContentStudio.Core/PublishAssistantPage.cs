using System.Net;
using System.Text;

namespace DawishContentStudio.Core;

public sealed class PublishAssistantPage
{
    public async Task<string> CreateAsync(PostJob job, string mediaPath, string jobFolder, CancellationToken cancellationToken = default)
    {
        var file = Path.Combine(jobFolder, "publish-assistant.html");
        var mediaUri = new Uri(mediaPath).AbsoluteUri;
        var html = BuildHtml(job, mediaUri);
        await File.WriteAllTextAsync(file, html, Encoding.UTF8, cancellationToken);
        return file;
    }

    private static string BuildHtml(PostJob job, string mediaUri)
    {
        static string E(string value) => WebUtility.HtmlEncode(value ?? string.Empty);
        var tikTokMode = job.Publishing.TikTokMode switch
        {
            TikTokPublishMode.ImageOnly => "صورة فقط — بدون تحويل لمقطع",
            TikTokPublishMode.ImageFirstSilentVideoFallback => "صورة أولًا — فيديو صامت كاحتياط فقط",
            TikTokPublishMode.SilentVideoOnly => "فيديو صامت فقط — بدون صوت",
            _ => "صورة فقط"
        };

        return $$"""
<!doctype html>
<html lang="ar" dir="rtl">
<head>
<meta charset="utf-8">
<title>Dawish Publish Assistant - {{E(job.Title)}}</title>
<style>
body{font-family:Segoe UI,Tahoma,Arial,sans-serif;margin:0;background:#f5efe6;color:#2d1d13}.wrap{max-width:1180px;margin:24px auto;padding:0 18px}.top{background:#3d2617;color:white;border-radius:20px;padding:18px 22px;margin-bottom:18px}.grid{display:grid;grid-template-columns:390px 1fr;gap:18px}.card{background:white;border-radius:20px;padding:18px;box-shadow:0 8px 24px rgba(0,0,0,.08)}img{max-width:100%;border-radius:16px;background:#eee}.pill{display:inline-block;padding:7px 12px;border-radius:999px;background:#f4d8a8;margin:4px;color:#3d2617;font-weight:700}.platform{border:1px solid #ead8c3;border-radius:16px;padding:14px;margin:12px 0}.caption{white-space:pre-wrap;background:#faf7f2;border-radius:12px;padding:12px;line-height:1.7}.danger{color:#8a1f1f;font-weight:700}.ok{color:#126236;font-weight:700}.button{display:inline-block;background:#6b3f24;color:white;text-decoration:none;border-radius:12px;padding:11px 14px;margin:5px}.muted{color:#765f50}.phone{background:#111;border-radius:34px;padding:18px;max-width:320px;margin:auto}.screen{background:#fff;border-radius:24px;min-height:560px;padding:14px}.screen .image{height:330px;display:flex;align-items:center;justify-content:center;overflow:hidden;background:#eee;border-radius:18px}.screen img{width:100%;height:100%;object-fit:contain}.copybox{font-family:Consolas,monospace;font-size:13px;direction:rtl}</style>
</head>
<body>
<div class="wrap">
 <div class="top">
  <h1>مساعد النشر — {{E(job.Title)}}</h1>
  <div class="muted" style="color:#f4d8a8">بدون API — تيك توك بدون صوت — سناب صورة فقط — لا تضغط نشر إلا بعد المراجعة</div>
 </div>
 <div class="grid">
  <div class="card">
   <h2>معاينة الجوال</h2>
   <div class="phone"><div class="screen">
    <div class="muted">Instagram / TikTok / Snapchat</div>
    <div class="image"><img src="{{mediaUri}}" alt="post image"></div>
    <p>{{E(job.Captions.Instagram)}}</p>
    <div class="pill">TikTok: {{E(tikTokMode)}}</div>
    <div class="pill">Snapchat: صورة فقط</div>
   </div></div>
  </div>
  <div class="card">
   <h2>خطوات التنفيذ</h2>
   <p><span class="ok">الصورة جاهزة:</span> {{E(job.MediaFileName)}}</p>
   <p><span class="ok">وقت النشر:</span> {{job.ScheduledAt:yyyy-MM-dd HH:mm}}</p>
   <p><span class="ok">القواعد:</span> TikTok بدون صوت، Snapchat صورة فقط، منع الكلام الطبي.</p>
   <a class="button" href="https://www.instagram.com/">فتح Instagram</a>
   <a class="button" href="https://www.tiktok.com/upload">فتح TikTok Upload</a>
   <a class="button" href="https://web.snapchat.com/">فتح Snapchat Web</a>
   <div class="platform">
    <h3>Instagram caption</h3>
    <div class="caption copybox">{{E(job.Captions.Instagram)}}</div>
   </div>
   <div class="platform">
    <h3>TikTok caption — بدون أغاني</h3>
    <div class="caption copybox">{{E(job.Captions.TikTok)}}</div>
   </div>
   <div class="platform">
    <h3>Snapchat caption — صورة فقط</h3>
    <div class="caption copybox">{{E(job.Captions.Snapchat)}}</div>
   </div>
   <p class="danger">بعد النشر من المتصفح، ارجع للبرنامج/Agent وعلّم المنشور تم. لا يوجد API ولا نشر خلف ظهرك.</p>
  </div>
 </div>
</div>
</body>
</html>
""";
    }
}
