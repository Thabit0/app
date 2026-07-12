using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace DawishContentStudio.Core;

public sealed class CloudflareClient
{
    private readonly string _baseUrl;
    private readonly string _token;
    private readonly HttpClient _http;
    private static readonly HttpClient SharedHttp = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public CloudflareClient(string baseUrl, string token, HttpClient? httpClient = null)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _token = token;
        _http = httpClient ?? SharedHttp;
    }

    public async Task<HealthResponse> HealthAsync()
    {
        using var request = Create(HttpMethod.Get, "/health");
        using var response = await _http.SendAsync(request);
        return await ReadJsonAsync<HealthResponse>(response);
    }

    public async Task<UploadPostResponse> UploadPostAsync(PostUploadRequest post, string imagePath)
    {
        using var content = new MultipartFormDataContent();
        AddTextPart(content, "caption", post.Caption);
        AddTextPart(content, "instagram_enabled", post.InstagramEnabled ? "1" : "0");
        AddTextPart(content, "tiktok_enabled", post.TikTokEnabled ? "1" : "0");
        AddTextPart(content, "snapchat_enabled", post.SnapchatEnabled ? "1" : "0");
        AddTextPart(content, "instagram_location", post.InstagramLocation ?? "");
        AddTextPart(content, "tiktok_location", post.TikTokLocation ?? "");
        AddTextPart(content, "snapchat_location", post.SnapchatLocation ?? "");
        AddTextPart(content, "scheduled_at", post.ScheduledAt.ToUniversalTime().ToString("O"));
        AddTextPart(content, "tiktok_mode", post.TikTokMode);
        AddTextPart(content, "snapchat_mode", post.SnapchatMode);

        var stream = File.OpenRead(imagePath);
        var image = new StreamContent(stream);
        image.Headers.ContentType = new MediaTypeHeaderValue(ContentTypeFor(imagePath));
        image.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
        {
            Name = Quote("image"),
            FileName = Quote("upload" + SafeExtension(imagePath))
        };
        content.Add(image);

        using var request = Create(HttpMethod.Post, "/v1/admin/posts");
        request.Content = content;
        using var response = await _http.SendAsync(request);
        return await ReadJsonAsync<UploadPostResponse>(response);
    }

    public async Task<List<CloudflarePost>> ListPostsAsync()
    {
        using var request = Create(HttpMethod.Get, "/v1/admin/posts");
        using var response = await _http.SendAsync(request);
        return await ReadJsonAsync<List<CloudflarePost>>(response);
    }

    public async Task<List<CloudflarePost>> GetDuePostsAsync()
    {
        using var request = Create(HttpMethod.Get, "/v1/shop/due");
        using var response = await _http.SendAsync(request);
        return await ReadJsonAsync<List<CloudflarePost>>(response);
    }

    public async Task<List<CloudflarePost>> GetQueuePostsAsync()
    {
        using var request = Create(HttpMethod.Get, "/v1/shop/queue");
        using var response = await _http.SendAsync(request);
        return await ReadJsonAsync<List<CloudflarePost>>(response);
    }

    public async Task<ClaimResponse> ClaimPostAsync(string postId, string deviceId)
    {
        using var request = Create(HttpMethod.Post, "/v1/shop/claim");
        request.Content = JsonContent.Create(new { postId, deviceId });
        using var response = await _http.SendAsync(request);
        return await ReadJsonAsync<ClaimResponse>(response);
    }

    public async Task<OperationResponse> CancelPostAsync(string postId)
    {
        using var request = Create(HttpMethod.Post, $"/v1/admin/posts/{Uri.EscapeDataString(postId)}/cancel");
        request.Content = JsonContent.Create(new { });
        using var response = await _http.SendAsync(request);
        return await ReadJsonAsync<OperationResponse>(response);
    }

    public async Task<OperationResponse> ReschedulePostAsync(string postId, DateTimeOffset scheduledAt)
    {
        using var request = Create(HttpMethod.Post, $"/v1/admin/posts/{Uri.EscapeDataString(postId)}/reschedule");
        request.Content = JsonContent.Create(new { scheduledAt = scheduledAt.ToUniversalTime().ToString("O") });
        using var response = await _http.SendAsync(request);
        return await ReadJsonAsync<OperationResponse>(response);
    }

    public async Task<List<PostEvent>> GetPostEventsAsync(string postId)
    {
        using var request = Create(HttpMethod.Get, $"/v1/admin/posts/{Uri.EscapeDataString(postId)}/events");
        using var response = await _http.SendAsync(request);
        return await ReadJsonAsync<List<PostEvent>>(response);
    }

    public async Task HeartbeatAsync(string deviceName, string mode)
    {
        using var request = Create(HttpMethod.Post, "/v1/shop/heartbeat");
        request.Content = JsonContent.Create(new { device = deviceName, mode });
        using var response = await _http.SendAsync(request);
        await EnsureSuccessAsync(response);
    }

    public async Task DownloadMediaAsync(string mediaKey, string targetPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        using var request = Create(HttpMethod.Get, "/v1/media/" + Uri.EscapeDataString(mediaKey));
        using var response = await _http.SendAsync(request);
        await EnsureSuccessAsync(response);
        await using var input = await response.Content.ReadAsStreamAsync();
        await using var output = File.Create(targetPath);
        await input.CopyToAsync(output);
    }

    public async Task ReportResultAsync(string postId, string platform, string status, string message, string deviceId)
    {
        using var request = Create(HttpMethod.Post, $"/v1/shop/posts/{Uri.EscapeDataString(postId)}/platform-result");
        request.Content = JsonContent.Create(new { platform, status, message, deviceId });
        using var response = await _http.SendAsync(request);
        await EnsureSuccessAsync(response);
    }

    private HttpRequestMessage Create(HttpMethod method, string path)
    {
        var req = new HttpRequestMessage(method, _baseUrl + path);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        return req;
    }

    private static void AddTextPart(MultipartFormDataContent content, string name, string value)
    {
        var part = new StringContent(value, Encoding.UTF8);
        part.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
        {
            Name = Quote(name)
        };
        content.Add(part);
    }

    private static string Quote(string value) => $"\"{value.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";

    private static string SafeExtension(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".png" => ".png",
        ".webp" => ".webp",
        ".jpeg" => ".jpeg",
        _ => ".jpg"
    };

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
