using System.Diagnostics;

namespace DawishContentStudio.Core;

public sealed class PublisherAgent
{
    private readonly SyncFolderService _sync;
    private readonly ClipboardService _clipboard = new();
    private readonly PublishAssistantPage _assistantPage = new();

    public PublisherAgent(SyncFolderService sync)
    {
        _sync = sync;
    }

    public async Task<int> ProcessDueJobsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _sync.ReadAgentSettingsAsync(cancellationToken);
        _sync.CleanupStaleLocks(TimeSpan.FromMinutes(Math.Max(5, settings.StaleLockMinutes)));
        var readiness = await _sync.CheckDeviceReadinessAsync(cancellationToken);
        await _sync.WriteHeartbeatAsync("publisher-agent", "يفحص المنشورات المستحقة — " + readiness.Summary, cancellationToken);
        await _sync.WriteLogAsync("agent", "scan: " + readiness.Summary, cancellationToken);

        var dueJobs = await _sync.ListDueJobsAsync(DateTimeOffset.Now, cancellationToken);
        var processed = 0;

        foreach (var job in dueJobs)
        {
            if (!_sync.TryLockJob(job, out _)) continue;

            try
            {
                var status = await ExecuteJobAsync(job, settings, cancellationToken);
                await _sync.WriteStatusAsync(status, cancellationToken);

                if (status.Overall == "awaiting_manual_confirmation" || settings.RequireManualDoneConfirmation)
                    _sync.MarkAwaitingConfirmation(job, status.LastMessage);
                else if (status.Overall.StartsWith("failed", StringComparison.OrdinalIgnoreCase))
                    _sync.MarkFailed(job, status.LastMessage);
                else
                    _sync.MarkDone(job);

                processed++;
            }
            catch (Exception ex)
            {
                await _sync.WriteStatusAsync(new PostStatus
                {
                    JobId = job.Id,
                    Overall = "failed",
                    LastMessage = ex.Message,
                    UpdatedAt = DateTimeOffset.Now
                }, cancellationToken);
                await _sync.WriteErrorAsync(job.Id, "agent", ex.Message, cancellationToken);
                _sync.MarkFailed(job, ex.Message);
            }
        }

        await _sync.WriteHeartbeatAsync("publisher-agent", $"تمت معالجة {processed} منشور", cancellationToken);
        return processed;
    }

    private async Task<PostStatus> ExecuteJobAsync(PostJob job, AgentSettings settings, CancellationToken cancellationToken)
    {
        var media = _sync.FindJobMedia(job);
        var status = new PostStatus
        {
            JobId = job.Id,
            LastMessage = media is null ? "الصورة غير موجودة أو لم تكتمل مزامنتها." : "بدأت محطة النشر تنفيذ المنشور بدون API.",
            UpdatedAt = DateTimeOffset.Now
        };

        if (media is null)
        {
            status.Overall = "failed_missing_image";
            status.Instagram = job.Platforms.Instagram ? "failed_missing_image" : "skipped";
            status.TikTok = job.Platforms.TikTok ? "failed_missing_image" : "skipped";
            status.Snapchat = job.Platforms.Snapchat ? "failed_missing_image" : "skipped";
            await _sync.WriteErrorAsync(job.Id, "media", "الصورة غير موجودة أو لم تصل من Drive بعد.", cancellationToken);
            return status;
        }

        var lateBy = DateTimeOffset.Now - job.ScheduledAt;
        if (ShouldHoldLateJob(job, lateBy, settings, out var lateReason))
        {
            status.Overall = "needs_review_late";
            status.LastMessage = lateReason;
            status.Instagram = job.Platforms.Instagram ? "needs_review_late" : "skipped";
            status.TikTok = job.Platforms.TikTok ? "needs_review_late" : "skipped";
            status.Snapchat = job.Platforms.Snapchat ? "needs_review_late" : "skipped";
            _sync.MarkNeedsReview(job, lateReason);
            return status;
        }

        await _sync.WriteExecutionPlanAsync(job, media, cancellationToken);
        var assistantPath = await _assistantPage.CreateAsync(job, media, _sync.GetJobFolder(job.Id), cancellationToken);
        await _sync.WriteLogAsync("agent", $"job {job.Id}: media ready at {media}", cancellationToken);

        if (settings.CopyInstagramCaptionToClipboard && !string.IsNullOrWhiteSpace(job.Captions.Instagram))
        {
            status.LastMessage = await _clipboard.TrySetTextAsync(job.Captions.Instagram, cancellationToken)
                ? "تم نسخ كابشن Instagram للحافظة وفتح مساعد النشر."
                : "تعذر نسخ الكابشن للحافظة، لكنه محفوظ داخل ملفات المنشور.";
        }

        if (settings.OpenPublishAssistantPage)
            OpenPath(assistantPath);

        if (settings.OpenPlatformsAutomatically)
        {
            if (job.Platforms.Instagram)
            {
                OpenUrl(settings.InstagramUrl);
                status.Instagram = settings.StopBeforeFinalPublishClick ? "opened_needs_confirmation" : "opened_ready_to_publish";
            }
            else status.Instagram = "skipped";

            if (job.Platforms.TikTok)
            {
                OpenUrl(settings.TikTokUploadUrl);
                status.TikTok = job.Publishing.TikTokMode switch
                {
                    TikTokPublishMode.ImageOnly => "opened_image_only_no_music",
                    TikTokPublishMode.ImageFirstSilentVideoFallback => "opened_image_first_silent_fallback_allowed",
                    TikTokPublishMode.SilentVideoOnly => "opened_silent_video_no_audio",
                    _ => "opened_image_only_no_music"
                };
            }
            else status.TikTok = "skipped";

            if (job.Platforms.Snapchat)
            {
                OpenUrl(settings.SnapchatWebUrl);
                status.Snapchat = "opened_image_only";
            }
            else status.Snapchat = "skipped";

            status.Overall = settings.StopBeforeFinalPublishClick || settings.RequireManualDoneConfirmation
                ? "awaiting_manual_confirmation"
                : "opened";
            status.LastMessage = "تم فتح مساعد النشر والمنصات. TikTok صورة/بدون صوت حسب الإعداد، Snapchat صورة فقط. علّم المنشور تم بعد النشر.";
        }
        else
        {
            status.Instagram = job.Platforms.Instagram ? "ready_manual" : "skipped";
            status.TikTok = job.Platforms.TikTok ? "ready_image_only_no_music" : "skipped";
            status.Snapchat = job.Platforms.Snapchat ? "ready_image_only" : "skipped";
            status.Overall = "awaiting_manual_confirmation";
            status.LastMessage = "تم تجهيز مساعد النشر فقط بدون فتح المنصات. علّم المنشور تم بعد النشر.";
        }

        return status;
    }

    private static bool ShouldHoldLateJob(PostJob job, TimeSpan lateBy, AgentSettings settings, out string reason)
    {
        reason = "";
        if (lateBy <= TimeSpan.Zero) return false;

        if (job.Publishing.LatePostPolicy == LatePostPolicy.SkipIfLate)
        {
            reason = "تجاوز وقت النشر والسياسة تمنع نشر المتأخر.";
            return true;
        }

        if (job.Publishing.LatePostPolicy == LatePostPolicy.NeedsReviewIfLate)
        {
            reason = "تجاوز وقت النشر والسياسة تتطلب مراجعة قبل النشر.";
            return true;
        }

        var maxHours = Math.Max(1, settings.PublishLateIfLessThanHours);
        if (lateBy > TimeSpan.FromHours(maxHours))
        {
            reason = $"فات وقت المنشور أكثر من {maxHours} ساعات؛ يحتاج مراجعة قبل النشر.";
            return true;
        }

        return false;
    }

    private static void OpenUrl(string url) => OpenPath(url);

    private static void OpenPath(string pathOrUrl)
    {
        try
        {
            Process.Start(new ProcessStartInfo(pathOrUrl) { UseShellExecute = true });
        }
        catch
        {
            // Status is still written. No API mode should fail safely, not silently publish wrong content.
        }
    }
}
