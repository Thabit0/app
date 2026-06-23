using DawishContentStudio.Core;
using Xunit;

namespace DawishContentStudio.Tests;

public class PublisherAgentV04Tests
{
    [Fact]
    public async Task AwaitingConfirmationIsExcludedFromDueJobs()
    {
        var root = Path.Combine(Path.GetTempPath(), "dawish-agent-v04-" + Guid.NewGuid().ToString("N"));
        var sync = new SyncFolderService(root);
        var image = Path.Combine(root, "sample.jpg");
        await File.WriteAllBytesAsync(image, [1, 2, 3, 4]);

        var job = new PostJob
        {
            Id = JobId.Create("job"),
            Title = "اختبار",
            SourceImagePath = image,
            ScheduledAt = DateTimeOffset.Now.AddMinutes(-1),
            Captions = new PostCaptions
            {
                Instagram = "متوفر الآن في متجر الدويش.",
                TikTok = "متوفر الآن في متجر الدويش.",
                Snapchat = "متوفر الآن."
            },
            Publishing = new PublishingOptions { TikTokMode = TikTokPublishMode.ImageOnly }
        };
        await sync.CreateJobAsync(job, image);

        Assert.Single(await sync.ListDueJobsAsync(DateTimeOffset.Now));
        sync.MarkAwaitingConfirmation(job, "opened");
        Assert.Empty(await sync.ListDueJobsAsync(DateTimeOffset.Now));
        Assert.True(sync.IsAwaitingConfirmation(job.Id));
    }
}
