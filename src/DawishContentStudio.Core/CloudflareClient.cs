using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace DawishContentStudio.Core;

public sealed class CloudflareClient
{
    private readonly string _baseUrl;
    private readonly string _token;
    private static readonly HttpClient Http = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public CloudflareClient(string baseUrl, string token)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _token = token;
    }

    public async Task<HealthResponse> HealthAsync()
    {
        using var request = Create(HttpMethod.Get, "/health");
        using var response = await Http.SendAsync(request);
        return await ReadJsonAsync<HealthResponse>(response);
    }

    public async Task<UploadPostResponse> UploadPostAsync(PostUploadRequest post, string imagePath)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(post.Caption, Encoding.UTF8), "caption");
        content.Add(new StringContent(post.InstagramEnabled ? "1" : "0"), "instagram_enabled");
        content.Add(new StringContent(post.TikTokEnabled ? "1" : "0"), "tiktok_enabled");
        content.Add(new StringContent(post.SnapchatEnabled ? "1" : "0"), "snapchat_enabled");
        content.Add(new StringContent(post.InstagramLocation ?? "", Encoding.UTF8), "instagram_location");
        content.Add(new StringContent(post.TikTokLocation ?? "", Encoding.UTF8), "tiktok_location");
        content.Add(new StringContent(post.SnapchatLocation ?? "", Encoding.UTF8), "snapchat_location");
        content.Add(new StringContent(post.ScheduledAt.ToUniversalTime().ToString("O")), "scheduled_at");
        content.Add(new StringContent(post.TikTokMode), "tiktok_mode");
        content.Add(new StringContent(post.SnapchatMode), "snapchat_mode");

        var stream = File.OpenRead(imagePath);
        var image = new StreamContent(stream);
        image.Headers.ContentType = new MediaTypeHeaderValue(ContentTypeFor(imagePath));
        content.Add(image, "image", Path.GetFileName(imagePath));

        using var request = Create(HttpMethod.Post, "/v1/admin/posts");
        request.Content = content;
        using var response = await Http.SendAsync(request);
        return await ReadJsonAsync<UploadPostResponse>(response);
    }

    public async Task<List<CloudflarePost>> ListPostsAsync()
    {
        using var request = Create(HttpMethod.Get, "/v1/admin/posts");
        using var response = await Http.SendAsync(request);
        return await ReadJsonAsync<List<CloudflarePost>>(response);
    }

    public async Task<List<CloudflarePost>> GetDuePostsAsync()
    {
        using var request = Create(HttpMethod.Get, "/v1/shop/due");
        using var response = await Http.SendAsync(request);
        return await ReadJsonAsync<List<CloudflarePost>>(response);
    }

    public async Task HeartbeatAsync(string deviceName, string mode)
    {
        using var request = Create(HttpMethod.Post, "/v1/shop/heartbeat");
        request.Content = JsonContent.Create(new { device = deviceName, mode });
        using var response = await Http.SendAsync(request);
        await EnsureSuccessAsync(response);
    }

    public async Task DownloadMediaAsync(string mediaKey, string targetPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        using var request = Create(HttpMethod.Get, "/v1/media/" + Uri.EscapeDataString(mediaKey));
        using var response = await Http.SendAsync(request);
        await EnsureSuccessAsync(response);
        await using var input = await response.Content.ReadAsStreamAsync();
        await using var output = File.Create(targetPath);
        await input.CopyToAsync(output);
    }

    public async Task ReportResultAsync(string postId, string status, string message)
    {
        using var request = Create(HttpMethod.Post, $"/v1/shop/posts/{Uri.EscapeDataString(postId)}/result");
        request.Content = JsonContent.Create(new { platform = "all", status, message });
        using var response = await Http.SendAsync(request);
        await EnsureSuccessAsync(response);
    }

    private HttpRequestMessage Create(HttpMethod method, string path)
    {
        var req = new HttpRequestMessage(method, _baseUrl + path);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        return req;
    }

    private static async Task<T> ReadJsonAsync<T>(HttpResponseMessage response)
    {
        await EnsureSuccessAsync(response);
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(json, JsonOptions) ?? throw new InvalidOperationException("رد Cloudflare غير مفهوم.");
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        var text = await response.Content.ReadAsStringAsync();
        throw new InvalidOperationException($"Cloudflare رفض الطلب: {(int)response.StatusCode} {response.ReasonPhrase}\n{text}");
    }

    private static string ContentTypeFor(string file) => Path.GetExtension(file).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".webp" => "image/webp",
        _ => "image/jpeg"
    };
}
