namespace DawishContentStudio.Core;

public sealed class PostJob
{
    public string Id { get; set; } = JobId.Create("post");
    public string Title { get; set; } = "منشور جديد";
    public DateTimeOffset ScheduledAt { get; set; } = DateTimeOffset.Now.AddHours(1);
    public string MediaFileName { get; set; } = "image_original.jpg";
    public string SourceImagePath { get; set; } = "";
    public string WebsiteUrl { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string CampaignName { get; set; } = "";
    public PlatformSelection Platforms { get; set; } = new();
    public PostCaptions Captions { get; set; } = new();
    public SafetyRules Rules { get; set; } = new();
    public PublishingOptions Publishing { get; set; } = new();
    public JobReadiness Readiness { get; set; } = JobReadiness.Approved;
    public string Status { get; set; } = "approved";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;
}

public sealed class PlatformSelection
{
    public bool Instagram { get; set; } = true;
    public bool TikTok { get; set; } = true;
    public bool Snapchat { get; set; } = true;
}

public sealed class PostCaptions
{
    public string Instagram { get; set; } = "";
    public string TikTok { get; set; } = "";
    public string Snapchat { get; set; } = "";
}

public sealed class SafetyRules
{
    public bool NoMedicalClaims { get; set; } = true;
    public bool TikTokNoMusic { get; set; } = true;
    public bool SnapchatImageOnly { get; set; } = true;
}

public sealed class PublishingOptions
{
    public TikTokPublishMode TikTokMode { get; set; } = TikTokPublishMode.ImageOnly;
    public LatePostPolicy LatePostPolicy { get; set; } = LatePostPolicy.PublishIfLessThanSixHoursLate;
    public bool StopBeforeFinalPublishClick { get; set; } = true;
}

public static class JobId
{
    public static string Create(string prefix)
    {
        return $"{prefix}_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}"[..(prefix.Length + 1 + 15 + 1 + 8)];
    }
}
