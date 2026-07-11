using DawishContentStudio.Core;
using System.Net;
using System.Text;
using Xunit;

namespace DawishContentStudio.Tests;

public sealed class CloudflareClientTests
{
    [Fact]
    public async Task UploadUsesCloudflareCompatibleMultipartNames()
    {
        var handler = new CaptureHandler();
        using var http = new HttpClient(handler);
        var client = new CloudflareClient("https://example.test", "token", http);
        var imagePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".png");
        await File.WriteAllBytesAsync(imagePath, [1, 2, 3, 4]);

        try
        {
            var result = await client.UploadPostAsync(new PostUploadRequest
            {
                Caption = "اختبار",
                ScheduledAt = DateTime.Now.AddHours(1),
                InstagramEnabled = true,
                TikTokEnabled = true,
                SnapchatEnabled = true,
                InstagramLocation = "مدينة الزلفي",
                TikTokLocation = "عطارة الدويش",
                TikTokMode = "image_only",
                SnapchatMode = "image_only"
            }, imagePath);

            Assert.Equal("post-1", result.Id);
            Assert.NotNull(handler.MultipartBody);
            Assert.Contains("Content-Disposition: form-data; name=\"caption\"", handler.MultipartBody);
            Assert.Contains("Content-Disposition: form-data; name=\"image\"; filename=\"upload.png\"", handler.MultipartBody);
            Assert.DoesNotContain("filename*=", handler.MultipartBody);
        }
        finally
        {
            File.Delete(imagePath);
        }
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public string? MultipartBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            MultipartBody = Encoding.UTF8.GetString(await request.Content!.ReadAsByteArrayAsync(cancellationToken));
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"id\":\"post-1\",\"mediaKey\":\"posts/post-1/image.png\",\"message\":\"ok\"}")
            };
        }
    }
}
