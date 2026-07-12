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

    [Fact]
    public async Task ClaimUsesDeviceIdentityAndExpectedEndpoint()
    {
        var handler = new CaptureHandler
        {
            ResponseJson = "{\"ok\":true,\"postId\":\"post-1\",\"deviceId\":\"shop-pc\",\"claimedAt\":\"2026-07-12T00:00:00Z\",\"expiresAt\":\"2026-07-12T00:15:00Z\"}"
        };
        using var http = new HttpClient(handler);
        var client = new CloudflareClient("https://example.test", "token", http);

        var result = await client.ClaimPostAsync("post-1", "shop-pc");

        Assert.True(result.Ok);
        Assert.Equal("/v1/shop/claim", handler.RequestUri?.AbsolutePath);
        Assert.Contains("\"deviceId\":\"shop-pc\"", handler.RequestBody);
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public string? MultipartBody { get; private set; }
        public string? RequestBody { get; private set; }
        public Uri? RequestUri { get; private set; }
        public string ResponseJson { get; set; } = "{\"id\":\"post-1\",\"mediaKey\":\"posts/post-1/image.png\",\"message\":\"ok\"}";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            RequestBody = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            if (request.Content is MultipartFormDataContent) MultipartBody = RequestBody;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ResponseJson)
            };
        }
    }
}
