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
    public string ArchiveFolder { get; }
    public string SettingsFolder { get; }

    public void EnsureStructure()
    {
        foreach (var folder in new[] { RootFolder, JobsFolder, StatusFolder, HeartbeatFolder, LogsFolder, ErrorsFolder, ScreenshotsFolder, ArchiveFolder, SettingsFolder })
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
        return jobs.Where(j => j.Status == "approved" && j.ScheduledAt <= now && !IsDone(j.Id) && !IsFailed(j.Id) && !IsLocked(j.Id)).ToArray();
    }

    public bool TryLockJob(PostJob job, out string lockPath)
    {
        var folder = GetJobFolder(job.Id);
        lockPath = Path.Combine(folder, $"LOCKED_BY_{Sanitize(Environment.MachineName)}.lock");
        if (Directory.EnumerateFiles(folder, "LOCKED_BY_*.lock").Any()) return false;
        File.WriteAllText(lockPath, DateTimeOffset.Now.ToString("O"));
        return true;
    }

    public void MarkDone(PostJob job)
    {
        File.WriteAllText(Path.Combine(GetJobFolder(job.Id), "DONE.flag"), DateTimeOffset.Now.ToString("O"));
        DeleteLocks(job.Id);
    }

    public void MarkFailed(PostJob job, string reason)
    {
        File.WriteAllText(Path.Combine(GetJobFolder(job.Id), "FAILED.flag"), reason + Environment.NewLine + DateTimeOffset.Now.ToString("O"));
        DeleteLocks(job.Id);
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

    public string? FindJobMedia(PostJob job)
    {
        var jobFolder = GetJobFolder(job.Id);
        var file = Path.Combine(jobFolder, job.MediaFileName);
        return File.Exists(file) ? file : null;
    }

    public string GetJobFolder(string jobId) => Path.Combine(JobsFolder, Sanitize(jobId));
    public bool IsDone(string jobId) => File.Exists(Path.Combine(GetJobFolder(jobId), "DONE.flag"));
    public bool IsFailed(string jobId) => File.Exists(Path.Combine(GetJobFolder(jobId), "FAILED.flag"));
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
