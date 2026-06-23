using DawishContentStudio.Core;
using Xunit;

namespace DawishContentStudio.Tests;

public sealed class BulkJobPlannerTests
{
    [Fact]
    public void CreatesSeparateJobsForEveryImage()
    {
        var planner = new BulkJobPlanner();
        var jobs = planner.CreateJobs(new BulkPostRequest
        {
            ImagePaths = ["a.jpg", "b.jpg", "c.jpg"],
            SharedCaption = "متوفر الآن في متجر الدويش. اطلبه من الموقع.",
            TikTokMode = TikTokPublishMode.ImageOnly,
            StartAt = new DateTimeOffset(2026, 6, 22, 19, 0, 0, TimeSpan.Zero),
            PostsPerDay = 1
        });

        Assert.Equal(3, jobs.Count);
        Assert.All(jobs, j => Assert.Equal(TikTokPublishMode.ImageOnly, j.Publishing.TikTokMode));
    }
}
