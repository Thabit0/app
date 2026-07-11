namespace DawishContentStudio.Core;

public sealed class AgentSettings
{
    public string DeviceName { get; set; } = Environment.MachineName;
    public string SyncFolder { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "DawishSync");
    public int ScanIntervalSeconds { get; set; } = 30;
    public bool RunAtWindowsStartup { get; set; } = true;
    public bool StopBeforeFinalPublishClick { get; set; } = true;
    public bool OpenPlatformsAutomatically { get; set; } = true;
    public bool KeepTikTokImageOnlyByDefault { get; set; } = true;
    public bool SnapchatImageOnly { get; set; } = true;
    public bool PreventSleepWhileRunning { get; set; } = true;
    public bool OpenPublishAssistantPage { get; set; } = true;
    public bool CopyInstagramCaptionToClipboard { get; set; } = true;
    public bool RequireManualDoneConfirmation { get; set; } = true;
    public int PublishLateIfLessThanHours { get; set; } = 6;
    public int MarkNeedsReviewIfLateMoreThanHours { get; set; } = 6;
    public int StaleLockMinutes { get; set; } = 30;
    public string PreferredBrowser { get; set; } = "default";
    public string InstagramUrl { get; set; } = "https://www.instagram.com/";
    public string TikTokUploadUrl { get; set; } = "https://www.tiktok.com/upload";
    public string SnapchatWebUrl { get; set; } = "https://web.snapchat.com/";
}

public sealed class DeviceReadiness
{
    public string Device { get; set; } = Environment.MachineName;
    public string SyncFolder { get; set; } = "";
    public bool SyncFolderExists { get; set; }
    public bool JobsFolderExists { get; set; }
    public bool StatusFolderExists { get; set; }
    public bool BrowserAvailable { get; set; }
    public bool HasDueJobs { get; set; }
    public int ReadyJobsCount { get; set; }
    public int DueJobsCount { get; set; }
    public string BrowserHint { get; set; } = "";
    public string Summary { get; set; } = "";
    public DateTimeOffset CheckedAt { get; set; } = DateTimeOffset.Now;
}

public sealed class JobExecutionPlan
{
    public string JobId { get; set; } = "";
    public string Title { get; set; } = "";
    public DateTimeOffset ScheduledAt { get; set; }
    public string MediaPath { get; set; } = "";
    public string InstagramInstruction { get; set; } = "";
    public string TikTokInstruction { get; set; } = "";
    public string SnapchatInstruction { get; set; } = "";
    public bool TikTokImageOnly { get; set; } = true;
    public bool TikTokNoMusic { get; set; } = true;
    public bool SnapchatImageOnly { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
}
