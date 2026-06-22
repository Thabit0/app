using System.Diagnostics;

namespace DawishContentStudio.Core;

public sealed class PublisherAgent
{
    private readonly SyncFolderService _sync;

    public PublisherAgent(SyncFolderService sync)
    {
        _sync = sync;
    }

    public async Task<int> ProcessDueJobsAsync(CancellationToken cancellationToken = default)
    {
        await _sync.WriteHeartbeatAsync("publisher-agent", "يفحص المنشورات المستحقة", cancellationToken);
        var dueJobs = await _sync.ListDueJobsAsync(DateTimeOffset.Now, cancellationToken);
        var processed = 0;

        foreach (var job in dueJobs)
        {
            if (!_sync.TryLockJob(job, out _)) continue;

            try
            {
                var media = _sync.FindJobMedia(job);
                var status = new PostStatus
                {
                    JobId = job.Id,
                    LastMessage = media is null ? "الصورة غير موجودة أو لم تكتمل مزامنتها." : "تم فتح المنصات للمراجعة/النشر بدون API.",
                    UpdatedAt = DateTimeOffset.Now
                };

                if (media is null)
                {
                    status.Overall = "failed";
                    status.Instagram = job.Platforms.Instagram ? "failed_missing_image" : "skipped";
                    status.TikTok = job.Platforms.TikTok ? "failed_missing_image" : "skipped";
                    status.Snapchat = job.Platforms.Snapchat ? "failed_missing_image" : "skipped";
                    await _sync.WriteStatusAsync(status, cancellationToken);
                    _sync.MarkFailed(job, "missing image");
                    continue;
                }

                var lateBy = DateTimeOffset.Now - job.ScheduledAt;
                if (lateBy > TimeSpan.FromHours(6) && job.Publishing.LatePostPolicy == LatePostPolicy.PublishIfLessThanSixHoursLate)
                {
                    status.Overall = "needs_review_late";
                    status.LastMessage = "فات وقت المنشور أكثر من 6 ساعات؛ يحتاج مراجعة قبل النشر.";
                    await _sync.WriteStatusAsync(status, cancellationToken);
                    _sync.MarkFailed(job, "late needs review");
                    continue;
                }

                if (job.Platforms.Instagram)
                {
                    OpenUrl("https://www.instagram.com/");
                    status.Instagram = job.Publishing.StopBeforeFinalPublishClick ? "opened_needs_confirmation" : "opened";
                }
                else status.Instagram = "skipped";

                if (job.Platforms.TikTok)
                {
                    OpenUrl("https://www.tiktok.com/upload");
                    status.TikTok = job.Publishing.TikTokMode switch
                    {
                        TikTokPublishMode.ImageOnly => "opened_image_post_required",
                        TikTokPublishMode.ImageFirstSilentVideoFallback => "opened_image_first_silent_fallback_allowed",
                        TikTokPublishMode.SilentVideoOnly => "opened_silent_video_required",
                        _ => "opened_image_post_required"
                    };
                }
                else status.TikTok = "skipped";

                if (job.Platforms.Snapchat)
                {
                    OpenUrl("https://web.snapchat.com/");
                    status.Snapchat = "opened_image_only";
                }
                else status.Snapchat = "skipped";

                status.Overall = "opened_needs_confirmation";
                await _sync.WriteStatusAsync(status, cancellationToken);
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
                _sync.MarkFailed(job, ex.Message);
            }
        }

        await _sync.WriteHeartbeatAsync("publisher-agent", $"تمت معالجة {processed} منشور", cancellationToken);
        return processed;
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // Status will still be written. Future versions can capture screenshots from UI automation.
        }
    }
}
