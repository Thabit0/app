namespace DawishContentStudio.Core;

public sealed class PublishExecutionResult
{
    public string JobId { get; set; } = "";
    public string Overall { get; set; } = "pending";
    public string Instagram { get; set; } = "skipped";
    public string TikTok { get; set; } = "skipped";
    public string Snapchat { get; set; } = "skipped";
    public string AssistantPagePath { get; set; } = "";
    public string MediaPath { get; set; } = "";
    public bool ClipboardUpdated { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
    public string Message { get; set; } = "";
}
