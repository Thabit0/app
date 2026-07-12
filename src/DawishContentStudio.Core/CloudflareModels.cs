using System.ComponentModel;
using System.Runtime.CompilerServices;
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
    public bool IsLinked { get; set; }
    public string PinHash { get; set; } = "";
}

public sealed class LocalImageItem : INotifyPropertyChanged
{
    private bool _isSelected;
    private DateTime? _scheduledAt;
    private string _status = "غير مجدولة";

    public string Path { get; set; } = "";
    public string FileName { get; set; } = "";
    public string? RemoteId { get; set; }

    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }

    public DateTime? ScheduledAt
    {
        get => _scheduledAt;
        set
        {
            _scheduledAt = value;
            Status = value.HasValue ? "مجدولة" : "غير مجدولة";
            OnPropertyChanged();
            OnPropertyChanged(nameof(ScheduleDateText));
            OnPropertyChanged(nameof(ScheduleTimeText));
        }
    }

    public string Status
    {
        get => _status;
        set { _status = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); }
    }

    public string StatusText => ScheduledAt.HasValue ? "● مجدولة" : "○ غير مجدولة";
    public string ScheduleDateText => ScheduledAt?.ToString("dd MMMM yyyy") ?? "";
    public string ScheduleTimeText => ScheduledAt?.ToString("hh:mm tt").Replace("AM", "ص").Replace("PM", "م") ?? "";

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class PostUploadRequest
{
    public string Caption { get; set; } = "";
    public bool InstagramEnabled { get; set; } = true;
    public bool TikTokEnabled { get; set; } = true;
    public bool SnapchatEnabled { get; set; } = true;
    public string InstagramLocation { get; set; } = "مدينة الزلفي";
    public string TikTokLocation { get; set; } = "عطارة الدويش";
    public string SnapchatLocation { get; set; } = "";
    public DateTime ScheduledAt { get; set; }
    public string TikTokMode { get; set; } = "image_only";
    public string SnapchatMode { get; set; } = "image_only";
}

public sealed class CloudflarePost
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("caption")] public string Caption { get; set; } = "";
    [JsonPropertyName("instagramEnabled")] public bool InstagramEnabled { get; set; }
    [JsonPropertyName("tiktokEnabled")] public bool TikTokEnabled { get; set; }
    [JsonPropertyName("snapchatEnabled")] public bool SnapchatEnabled { get; set; }
    [JsonPropertyName("instagramLocation")] public string? InstagramLocation { get; set; }
    [JsonPropertyName("tiktokLocation")] public string? TikTokLocation { get; set; }
    [JsonPropertyName("snapchatLocation")] public string? SnapchatLocation { get; set; }
    [JsonPropertyName("scheduledAt")] public DateTime ScheduledAt { get; set; }
    [JsonPropertyName("status")] public string Status { get; set; } = "scheduled";
    [JsonPropertyName("platformStates")] public Dictionary<string, string> PlatformStates { get; set; } = [];
    [JsonPropertyName("tiktokMode")] public string TikTokMode { get; set; } = "image_only";
    [JsonPropertyName("snapchatMode")] public string SnapchatMode { get; set; } = "image_only";
    [JsonPropertyName("mediaKey")] public string MediaKey { get; set; } = "";
}

public sealed class ClaimResponse
{
    [JsonPropertyName("ok")] public bool Ok { get; set; }
    [JsonPropertyName("postId")] public string PostId { get; set; } = "";
    [JsonPropertyName("deviceId")] public string DeviceId { get; set; } = "";
    [JsonPropertyName("claimedAt")] public DateTimeOffset ClaimedAt { get; set; }
    [JsonPropertyName("expiresAt")] public DateTimeOffset ExpiresAt { get; set; }
}

public sealed class PostEvent
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("post_id")] public string PostId { get; set; } = "";
    [JsonPropertyName("device_id")] public string DeviceId { get; set; } = "";
    [JsonPropertyName("event_type")] public string EventType { get; set; } = "";
    [JsonPropertyName("message")] public string Message { get; set; } = "";
    [JsonPropertyName("created_at")] public DateTimeOffset CreatedAt { get; set; }
}

public sealed class OperationResponse
{
    [JsonPropertyName("ok")] public bool Ok { get; set; }
    [JsonPropertyName("postId")] public string PostId { get; set; } = "";
    [JsonPropertyName("status")] public string Status { get; set; } = "";
}

public sealed class UploadPostResponse
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("mediaKey")] public string MediaKey { get; set; } = "";
    [JsonPropertyName("message")] public string Message { get; set; } = "";
}

public sealed class HealthResponse
{
    [JsonPropertyName("ok")] public bool Ok { get; set; }
    [JsonPropertyName("message")] public string Message { get; set; } = "Cloudflare متصل";
}
