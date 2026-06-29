using System.Text.Json.Serialization;

namespace DawishContentStudio.Core;

public enum DeviceRole
{
    Admin,
    Shop
}

public sealed class AppSettings
{
    public string WorkerUrl { get; set; } = "https://dawish-content-api.thabit1919.workers.dev";
    public string Token { get; set; } = "";
    public DeviceRole Role { get; set; } = DeviceRole.Admin;
}

public sealed class LocalImageItem
{
    public string Path { get; set; } = "";
    public string FileName { get; set; } = "";
    public string Status { get; set; } = "جديد";
    public string? RemoteId { get; set; }
    public override string ToString() => $"{FileName} — {Status}";
}

public sealed class PostUploadRequest
{
    public string Caption { get; set; } = "";
    public bool InstagramEnabled { get; set; } = true;
    public bool TikTokEnabled { get; set; } = true;
    public bool SnapchatEnabled { get; set; }
    public string InstagramLocation { get; set; } = "";
    public string TikTokLocation { get; set; } = "";
    public string SnapchatLocation { get; set; } = "";
    public DateTime ScheduledAt { get; set; }
    public string TikTokMode { get; set; } = "image_only";
    public string SnapchatMode { get; set; } = "image_only";
}

public sealed class CloudflarePost
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("caption")]
    public string Caption { get; set; } = "";

    [JsonPropertyName("instagramEnabled")]
    public bool InstagramEnabled { get; set; }

    [JsonPropertyName("tiktokEnabled")]
    public bool TikTokEnabled { get; set; }

    [JsonPropertyName("snapchatEnabled")]
    public bool SnapchatEnabled { get; set; }

    [JsonPropertyName("instagramLocation")]
    public string? InstagramLocation { get; set; }

    [JsonPropertyName("tiktokLocation")]
    public string? TikTokLocation { get; set; }

    [JsonPropertyName("snapchatLocation")]
    public string? SnapchatLocation { get; set; }

    [JsonPropertyName("scheduledAt")]
    public DateTime ScheduledAt { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "scheduled";

    [JsonPropertyName("tiktokMode")]
    public string TikTokMode { get; set; } = "image_only";

    [JsonPropertyName("snapchatMode")]
    public string SnapchatMode { get; set; } = "image_only";

    [JsonPropertyName("mediaKey")]
    public string MediaKey { get; set; } = "";
}

public sealed class UploadPostResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("mediaKey")]
    public string MediaKey { get; set; } = "";

    [JsonPropertyName("message")]
    public string Message { get; set; } = "";
}

public sealed class HealthResponse
{
    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = "Cloudflare متصل";
}
