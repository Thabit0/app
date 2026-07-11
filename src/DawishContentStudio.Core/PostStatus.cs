namespace DawishContentStudio.Core;

public sealed class PostStatus
{
    public string JobId { get; set; } = "";
    public string DeviceName { get; set; } = Environment.MachineName;
    public string Instagram { get; set; } = "pending";
    public string TikTok { get; set; } = "pending_image";
    public string Snapchat { get; set; } = "pending_image";
    public string Overall { get; set; } = "pending";
    public string LastMessage { get; set; } = "";
    public string? ScreenshotPath { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
}

public sealed class HeartbeatStatus
{
    public string Device { get; set; } = Environment.MachineName;
    public string Mode { get; set; } = "publisher-agent";
    public string SyncFolder { get; set; } = "";
    public string LastMessage { get; set; } = "";
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
}
