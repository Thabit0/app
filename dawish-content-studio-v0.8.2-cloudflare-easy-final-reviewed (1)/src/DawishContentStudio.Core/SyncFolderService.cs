using System.Text.Json;

namespace DawishContentStudio.Core;

public sealed class SyncFolderService
{
    private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public SyncFolderService(string rootFolder)
    {
        RootFolder = rootFolder;
        JobsFolder = Path.Combine(rootFolder, "jobs");
        StatusFolder = Path.Combine(rootFolder, "status");
        HeartbeatFolder = Path.Combine(rootFolder, "heartbeat");
        LogsFolder = Path.Combine(rootFolder, "logs");
        ErrorsFolder = Path.Combine(rootFolder, "errors");
        ScreenshotsFolder = Path.Combine(rootFolder, "screenshots");
        PlansFolder = Path.Combine(rootFolder, "plans");
        ArchiveFolder = Path.Combine(rootFolder, "archive");
        SettingsFolder = Path.Combine(rootFolder, "settings");
        EnsureStructure();
    }

    public string RootFolder { get; }
    public string JobsFolder { get; }
    public string StatusFolder { get; }
    public string HeartbeatFolder { get; }
    public string LogsFolder { get; }
    public string ErrorsFolder { get; }
    public string ScreenshotsFolder { get; }
    public string PlansFolder { get; }
    public string ArchiveFolder { get; }
    public string SettingsFolder { get; }

    public void EnsureStructure()
    {
        foreach (var folder in new[] { RootFolder, JobsFolder, StatusFolder, HeartbeatFolder, LogsFolder, ErrorsFolder, ScreenshotsFolder, PlansFolder, ArchiveFolder, SettingsFolder })
            Directory.CreateDirectory(folder);
    }

    public async Task<string> CreateJobAsync(PostJob job, string imagePath, CancellationToken cancellationToken = default)
    {
        var guard = new MedicalClaimsGuard();
        var violations = guard.FindViolations(job.Title, job.Captions.Instagram, job.Captions.TikTok, job.Captions.Snapchat);
        if (violations.Count > 0)
            throw new InvalidOperationException("لا يمكن اعتماد المنشور بسبب ادعاءات طبية: " + string.Join(", ", violations));

        if (!File.Exists(imagePath)) throw new FileNotFoundException("الصورة غير موجودة.", imagePath);

        job.Rules.NoMedicalClaims = true;
        job.Rules.TikTokNoMusic = true;
        job.Rules.SnapchatImageOnly = true;
        job.Status = "approved";
        job.Readiness = JobReadiness.Approved;
        job.UpdatedAt = DateTimeOffset.Now;

        var safeId = Sanitize(job.Id);
        var jobFolder = Path.Combine(JobsFolder, safeId);
        Directory.CreateDirectory(jobFolder);
        DeleteIfExists(Path.Combine(jobFolder, "READY.flag"));

        var extension = Path.GetExtension(imagePath);
        if (string.IsNullOrWhiteSpace(extension)) extension = ".jpg";
        job.MediaFileName = "image_original" + extension;

        await CopyFileAsync(imagePath, Path.Combine(jobFolder, job.MediaFileName), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(jobFolder, "instagram_caption.txt"), job.Captions.Instagram, cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(jobFolder, "tiktok_caption.txt"), job.Captions.TikTok, cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(jobFolder, "snapchat_caption.txt"), job.Captions.Snapchat, cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(jobFolder, "post.json"), JsonSerializer.Serialize(job, _jsonOptions), cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(jobFolder, "READY.flag"), DateTimeOffset.Now.ToString("O"), cancellationToken);

        return jobFolder;
    }

    public async Task<IReadOnlyList<string>> CreateJobsAsync(IEnumerable<PostJob> jobs, CancellationToken cancellationToken = default)
    {
        var created = new List<string>();
        foreach (var job in jobs)
        {
            if (string.IsNullOrWhiteSpace(job.SourceImagePath)) throw new InvalidOperationException($"المنشور {job.Title} بدون صورة مصدر.");
            created.Add(await CreateJobAsync(job, job.SourceImagePath, cancellationToken));
        }
        return created;
    }

    public async Task<IReadOnlyList<PostJob>> ListJobsAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(JobsFolder)) return [];
        var result = new List<PostJob>();
        foreach (var file in Directory.EnumerateFiles(JobsFolder, "post.json", SearchOption.AllDirectories))
        {
            var jobFolder = Path.GetDirectoryName(file)!;
            if (!File.Exists(Path.Combine(jobFolder, "READY.flag"))) continue;
            try
            {
                var json = await File.ReadAllTextAsync(file, cancellationToken);
                var job = JsonSerializer.Deserialize<PostJob>(json, _jsonOptions);
                if (job is not null) result.Add(job);
            }
            catch
            {
                // Ignore half-synced or corrupted jobs. The next scan will retry.
            }
        }
        return result.OrderBy(j => j.ScheduledAt).ToArray();
    }

    public async Task<IReadOnlyList<PostJob>> ListDueJobsAsync(DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var jobs = await ListJobsAsync(cancellationToken);
        return jobs.Where(j => j.Status == "approved" && j.ScheduledAt <= now && !IsDone(j.Id) && !IsFailed(j.Id) && !IsAwaitingConfirmation(j.Id) && !IsLocked(j.Id)).ToArray();
    }

    public bool TryLockJob(PostJob job, out string lockPath)
    {
        var folder = GetJobFolder(job.Id);
        lockPath = Path.Combine(folder, $"LOCKED_BY_{Sanitize(Environment.MachineName)}.lock");
        if (Directory.EnumerateFiles(folder, "LOCKED_BY_*.lock").Any()) return false;
        File.WriteAllText(lockPath, DateTimeOffset.Now.ToString("O"));
        return true;
    }

    public void MarkDone(PostJob job) => MarkDone(job.Id);

    public void MarkDone(string jobId)
    {
        File.WriteAllText(Path.Combine(GetJobFolder(jobId), "DONE.flag"), DateTimeOffset.Now.ToString("O"));
        DeleteIfExists(Path.Combine(GetJobFolder(jobId), "AWAITING_CONFIRMATION.flag"));
        DeleteLocks(jobId);
    }

    public void MarkAwaitingConfirmation(PostJob job, string message)
    {
        File.WriteAllText(Path.Combine(GetJobFolder(job.Id), "AWAITING_CONFIRMATION.flag"), message + Environment.NewLine + DateTimeOffset.Now.ToString("O"));
        DeleteLocks(job.Id);
    }

    public void MarkFailed(PostJob job, string reason) => MarkFailed(job.Id, reason);

    public void MarkFailed(string jobId, string reason)
    {
        File.WriteAllText(Path.Combine(GetJobFolder(jobId), "FAILED.flag"), reason + Environment.NewLine + DateTimeOffset.Now.ToString("O"));
        DeleteIfExists(Path.Combine(GetJobFolder(jobId), "AWAITING_CONFIRMATION.flag"));
        DeleteLocks(jobId);
    }

    public async Task WriteStatusAsync(PostStatus status, CancellationToken cancellationToken = default)
    {
        var file = Path.Combine(StatusFolder, Sanitize(status.JobId) + ".status.json");
        await File.WriteAllTextAsync(file, JsonSerializer.Serialize(status, _jsonOptions), cancellationToken);
    }

    public async Task<IReadOnlyList<PostStatus>> ListStatusesAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(StatusFolder)) return [];
        var result = new List<PostStatus>();
        foreach (var file in Directory.EnumerateFiles(StatusFolder, "*.status.json"))
        {
            try
            {
                var json = await File.ReadAllTextAsync(file, cancellationToken);
                var status = JsonSerializer.Deserialize<PostStatus>(json, _jsonOptions);
                if (status is not null) result.Add(status);
            }
            catch { }
        }
        return result.OrderByDescending(s => s.UpdatedAt).ToArray();
    }

    public async Task WriteHeartbeatAsync(string mode, string message = "", CancellationToken cancellationToken = default)
    {
        var file = Path.Combine(HeartbeatFolder, Environment.MachineName + ".json");
        var payload = new HeartbeatStatus
        {
            Device = Environment.MachineName,
            Mode = mode,
            SyncFolder = RootFolder,
            LastMessage = message,
            UpdatedAt = DateTimeOffset.Now
        };
        await File.WriteAllTextAsync(file, JsonSerializer.Serialize(payload, _jsonOptions), cancellationToken);
    }

    public async Task<IReadOnlyList<HeartbeatStatus>> ListHeartbeatsAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(HeartbeatFolder)) return [];
        var result = new List<HeartbeatStatus>();
        foreach (var file in Directory.EnumerateFiles(HeartbeatFolder, "*.json"))
        {
            try
            {
                var json = await File.ReadAllTextAsync(file, cancellationToken);
                var heartbeat = JsonSerializer.Deserialize<HeartbeatStatus>(json, _jsonOptions);
                if (heartbeat is not null) result.Add(heartbeat);
            }
            catch { }
        }
        return result.OrderByDescending(h => h.UpdatedAt).ToArray();
    }


    public async Task<AgentSettings> ReadAgentSettingsAsync(CancellationToken cancellationToken = default)
    {
        var file = Path.Combine(SettingsFolder, "agent-settings.json");
        if (!File.Exists(file))
        {
            var defaults = new AgentSettings { SyncFolder = RootFolder };
            await WriteAgentSettingsAsync(defaults, cancellationToken);
            return defaults;
        }

        try
        {
            var json = await File.ReadAllTextAsync(file, cancellationToken);
            return JsonSerializer.Deserialize<AgentSettings>(json, _jsonOptions) ?? new AgentSettings { SyncFolder = RootFolder };
        }
        catch
        {
            return new AgentSettings { SyncFolder = RootFolder };
        }
    }

    public async Task WriteAgentSettingsAsync(AgentSettings settings, CancellationToken cancellationToken = default)
    {
        settings.SyncFolder = RootFolder;
        var file = Path.Combine(SettingsFolder, "agent-settings.json");
        await File.WriteAllTextAsync(file, JsonSerializer.Serialize(settings, _jsonOptions), cancellationToken);
    }

    public async Task WriteLogAsync(string source, string message, CancellationToken cancellationToken = default)
    {
        var file = Path.Combine(LogsFolder, DateTime.Now.ToString("yyyy-MM-dd") + ".log");
        var line = $"{DateTimeOffset.Now:O}\t{source}\t{message}{Environment.NewLine}";
        await File.AppendAllTextAsync(file, line, cancellationToken);
    }

    public async Task WriteErrorAsync(string jobId, string platform, string message, CancellationToken cancellationToken = default)
    {
        var folder = Path.Combine(ErrorsFolder, Sanitize(jobId));
        Directory.CreateDirectory(folder);
        var file = Path.Combine(folder, platform + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json");
        var payload = new
        {
            jobId,
            platform,
            message,
            device = Environment.MachineName,
            at = DateTimeOffset.Now
        };
        await File.WriteAllTextAsync(file, JsonSerializer.Serialize(payload, _jsonOptions), cancellationToken);
    }

    public async Task WriteExecutionPlanAsync(PostJob job, string mediaPath, CancellationToken cancellationToken = default)
    {
        var plan = new JobExecutionPlan
        {
            JobId = job.Id,
            Title = job.Title,
            ScheduledAt = job.ScheduledAt,
            MediaPath = mediaPath,
            TikTokImageOnly = job.Publishing.TikTokMode == TikTokPublishMode.ImageOnly,
            TikTokNoMusic = true,
            SnapchatImageOnly = true,
            InstagramInstruction = job.Platforms.Instagram ? "افتح Instagram، ارفع الصورة، الصق كابشن Instagram، ثم اعتمد النشر حسب إعداد الأمان." : "متوقف",
            TikTokInstruction = job.Platforms.TikTok ? "افتح TikTok Upload، ارفع الصورة كصورة فقط، لا تضف أغاني ولا صوت." : "متوقف",
            SnapchatInstruction = job.Platforms.Snapchat ? "افتح Snapchat Web أو نافذة سناب، ارفع الصورة كما هي كصورة فقط." : "متوقف"
        };
        var file = Path.Combine(PlansFolder, Sanitize(job.Id) + ".plan.json");
        await File.WriteAllTextAsync(file, JsonSerializer.Serialize(plan, _jsonOptions), cancellationToken);
    }

    public async Task<DeviceReadiness> CheckDeviceReadinessAsync(CancellationToken cancellationToken = default)
    {
        var jobs = await ListJobsAsync(cancellationToken);
        var dueJobs = await ListDueJobsAsync(DateTimeOffset.Now, cancellationToken);
        var browserAvailable = IsBrowserAvailable();
        var readiness = new DeviceReadiness
        {
            Device = Environment.MachineName,
            SyncFolder = RootFolder,
            SyncFolderExists = Directory.Exists(RootFolder),
            JobsFolderExists = Directory.Exists(JobsFolder),
            StatusFolderExists = Directory.Exists(StatusFolder),
            BrowserAvailable = browserAvailable,
            BrowserHint = browserAvailable ? "متصفح متوفر" : "لم يتم العثور على Edge/Chrome من المسارات الشائعة، سيحاول Windows فتح المتصفح الافتراضي.",
            HasDueJobs = dueJobs.Count > 0,
            ReadyJobsCount = jobs.Count,
            DueJobsCount = dueJobs.Count,
            Summary = $"جاهز: {jobs.Count}، مستحق الآن: {dueJobs.Count}، المتصفح: {(browserAvailable ? "موجود" : "غير مؤكد")}",
            CheckedAt = DateTimeOffset.Now
        };
        var file = Path.Combine(StatusFolder, Environment.MachineName + ".readiness.json");
        await File.WriteAllTextAsync(file, JsonSerializer.Serialize(readiness, _jsonOptions), cancellationToken);
        return readiness;
    }

    public void CleanupStaleLocks(TimeSpan maxAge)
    {
        if (!Directory.Exists(JobsFolder)) return;
        foreach (var lockFile in Directory.EnumerateFiles(JobsFolder, "LOCKED_BY_*.lock", SearchOption.AllDirectories))
        {
            try
            {
                var age = DateTimeOffset.UtcNow - new DateTimeOffset(File.GetLastWriteTimeUtc(lockFile));
                if (age > maxAge) File.Delete(lockFile);
            }
            catch { }
        }
    }

    public void MarkNeedsReview(PostJob job, string reason)
    {
        File.WriteAllText(Path.Combine(GetJobFolder(job.Id), "NEEDS_REVIEW.flag"), reason + Environment.NewLine + DateTimeOffset.Now.ToString("O"));
        DeleteLocks(job.Id);
    }

    public void ArchiveJob(PostJob job)
    {
        var source = GetJobFolder(job.Id);
        if (!Directory.Exists(source)) return;
        var target = Path.Combine(ArchiveFolder, Sanitize(job.Id));
        if (Directory.Exists(target)) target += "-" + DateTime.Now.ToString("yyyyMMddHHmmss");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        try { Directory.Move(source, target); } catch { }
    }

    private static bool IsBrowserAvailable()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var candidates = new[]
        {
            Path.Combine(programFiles, "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(programFilesX86, "Microsoft", "Edge", "Application", "msedge.exe"),
            Path.Combine(programFiles, "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(programFilesX86, "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(local, "Google", "Chrome", "Application", "chrome.exe")
        };
        return candidates.Any(File.Exists);
    }

    public async Task<PostJob?> FindJobAsync(string jobId, CancellationToken cancellationToken = default)
    {
        var file = Path.Combine(GetJobFolder(jobId), "post.json");
        if (!File.Exists(file)) return null;
        try
        {
            var json = await File.ReadAllTextAsync(file, cancellationToken);
            return JsonSerializer.Deserialize<PostJob>(json, _jsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public string GetCaptionPath(PostJob job, string platform)
    {
        var fileName = platform.ToLowerInvariant() switch
        {
            "instagram" => "instagram_caption.txt",
            "tiktok" => "tiktok_caption.txt",
            "snapchat" => "snapchat_caption.txt",
            _ => "instagram_caption.txt"
        };
        return Path.Combine(GetJobFolder(job.Id), fileName);
    }

    public string GetPublishAssistantPath(PostJob job) => Path.Combine(GetJobFolder(job.Id), "publish-assistant.html");

    public string? FindJobMedia(PostJob job)
    {
        var jobFolder = GetJobFolder(job.Id);
        var file = Path.Combine(jobFolder, job.MediaFileName);
        return File.Exists(file) ? file : null;
    }

    public string GetJobFolder(string jobId) => Path.Combine(JobsFolder, Sanitize(jobId));
    public bool IsDone(string jobId) => File.Exists(Path.Combine(GetJobFolder(jobId), "DONE.flag"));
    public bool IsFailed(string jobId) => File.Exists(Path.Combine(GetJobFolder(jobId), "FAILED.flag"));
    public bool IsAwaitingConfirmation(string jobId) => File.Exists(Path.Combine(GetJobFolder(jobId), "AWAITING_CONFIRMATION.flag"));
    public bool IsLocked(string jobId) => Directory.Exists(GetJobFolder(jobId)) && Directory.EnumerateFiles(GetJobFolder(jobId), "LOCKED_BY_*.lock").Any();

    private void DeleteLocks(string jobId)
    {
        var folder = GetJobFolder(jobId);
        if (!Directory.Exists(folder)) return;
        foreach (var lockFile in Directory.EnumerateFiles(folder, "LOCKED_BY_*.lock")) DeleteIfExists(lockFile);
    }

    private static async Task CopyFileAsync(string source, string target, CancellationToken cancellationToken)
    {
        await using var sourceStream = File.Open(source, FileMode.Open, FileAccess.Read, FileShare.Read);
        await using var targetStream = File.Create(target);
        await sourceStream.CopyToAsync(targetStream, cancellationToken);
    }

    private static void DeleteIfExists(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    public static string Sanitize(string input)
    {
        foreach (var ch in Path.GetInvalidFileNameChars()) input = input.Replace(ch, '-');
        return input;
    }
}
