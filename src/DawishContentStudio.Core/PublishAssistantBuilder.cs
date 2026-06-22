using System.Text;
using System.Text.Encodings.Web;

namespace DawishContentStudio.Core;

public static class PublishAssistantBuilder
{
    public static string WritePage(string folder, CloudflarePost post, string imagePath)
    {
        Directory.CreateDirectory(folder);
        var htmlPath = Path.Combine(folder, "publish-assistant.html");
        var imageName = Path.GetFileName(imagePath);
        string E(string? value) => HtmlEncoder.Default.Encode(value ?? "");
        var sb = new StringBuilder();
        sb.AppendLine("<!doctype html><html lang='ar' dir='rtl'><head><meta charset='utf-8'><title>Dawish Publish Assistant</title>");
        sb.AppendLine("<style>body{font-family:Tahoma,Arial;background:#f7f1e6;color:#17281f;padding:24px} .card{background:#fff;border:1px solid #dfd0b6;border-radius:20px;padding:18px;margin:12px 0;max-width:820px} img{max-width:420px;border-radius:18px;display:block;margin:auto}.btn{display:inline-block;background:#477d68;color:white;padding:12px 18px;border-radius:12px;margin:6px;text-decoration:none} textarea{width:100%;height:120px;font-size:16px}</style></head><body>");
        sb.AppendLine("<h1>مساعد نشر الدويش</h1>");
        sb.AppendLine("<div class='card'><img src='" + E(imageName) + "'></div>");
        sb.AppendLine("<div class='card'><h2>الكابشن</h2><textarea id='cap'>" + E(post.Caption) + "</textarea><br><button onclick='navigator.clipboard.writeText(document.getElementById(\"cap\").value)'>نسخ الكابشن</button></div>");
        sb.AppendLine("<div class='card'><h2>اللوكيشن</h2><p>Instagram: " + E(post.InstagramLocation) + "</p><p>TikTok: " + E(post.TikTokLocation) + "</p><p>Snapchat: " + E(post.SnapchatLocation) + "</p></div>");
        sb.AppendLine("<div class='card'><h2>فتح المنصات</h2>");
        if (post.InstagramEnabled) sb.AppendLine("<a class='btn' href='https://www.instagram.com/' target='_blank'>فتح Instagram</a>");
        if (post.TikTokEnabled) sb.AppendLine("<a class='btn' href='https://www.tiktok.com/upload' target='_blank'>فتح TikTok Upload</a><p>TikTok صورة فقط وبدون صوت.</p>");
        if (post.SnapchatEnabled) sb.AppendLine("<a class='btn' href='https://web.snapchat.com/' target='_blank'>فتح Snapchat</a><p>Snapchat صورة فقط.</p>");
        sb.AppendLine("</div></body></html>");
        File.WriteAllText(htmlPath, sb.ToString(), Encoding.UTF8);
        return htmlPath;
    }
}
