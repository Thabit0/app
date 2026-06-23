namespace DawishContentStudio.Core;

public sealed class BulkPostRequest
{
    public IReadOnlyList<string> ImagePaths { get; init; } = [];
    public string TitlePrefix { get; init; } = "منشور";
    public string SharedCaption { get; init; } = "متوفر الآن في متجر الدويش. اطلبه من الموقع.";
    public string WebsiteUrl { get; init; } = "";
    public string CampaignName { get; init; } = "";
    public PlatformSelection Platforms { get; init; } = new();
    public TikTokPublishMode TikTokMode { get; init; } = TikTokPublishMode.ImageOnly;
    public DateTimeOffset StartAt { get; init; } = DateTimeOffset.Now.AddHours(1);
    public int PostsPerDay { get; init; } = 1;
    public int MinutesBetweenPosts { get; init; } = 120;
    public bool SkipFriday { get; init; } = false;
    public bool EachImageAsSeparatePost { get; init; } = true;
}

public sealed class BulkJobPlanner
{
    private readonly MedicalClaimsGuard _guard = new();

    public IReadOnlyList<PostJob> CreateJobs(BulkPostRequest request)
    {
        if (request.ImagePaths.Count == 0) return [];
        if (!_guard.IsSafe(request.SharedCaption, request.TitlePrefix, request.CampaignName))
            throw new InvalidOperationException("النص الجماعي يحتوي على ادعاء طبي/علاجي ممنوع.");

        var result = new List<PostJob>();
        var current = request.StartAt;
        var countInDay = 0;

        for (var i = 0; i < request.ImagePaths.Count; i++)
        {
            while (request.SkipFriday && current.DayOfWeek == DayOfWeek.Friday)
                current = current.Date.AddDays(1).Add(request.StartAt.TimeOfDay);

            if (countInDay >= Math.Max(1, request.PostsPerDay))
            {
                current = current.Date.AddDays(1).Add(request.StartAt.TimeOfDay);
                countInDay = 0;
                while (request.SkipFriday && current.DayOfWeek == DayOfWeek.Friday)
                    current = current.Date.AddDays(1).Add(request.StartAt.TimeOfDay);
            }

            var image = request.ImagePaths[i];
            var title = $"{request.TitlePrefix} {i + 1}".Trim();
            var snapchatCaption = request.SharedCaption.Length > 80 ? request.SharedCaption[..80] : request.SharedCaption;

            result.Add(new PostJob
            {
                Id = JobId.Create("job"),
                Title = title,
                SourceImagePath = image,
                ScheduledAt = current,
                WebsiteUrl = request.WebsiteUrl,
                CampaignName = request.CampaignName,
                Platforms = new PlatformSelection
                {
                    Instagram = request.Platforms.Instagram,
                    TikTok = request.Platforms.TikTok,
                    Snapchat = request.Platforms.Snapchat
                },
                Captions = new PostCaptions
                {
                    Instagram = request.SharedCaption,
                    TikTok = request.SharedCaption,
                    Snapchat = snapchatCaption
                },
                Rules = new SafetyRules
                {
                    NoMedicalClaims = true,
                    TikTokNoMusic = true,
                    SnapchatImageOnly = true
                },
                Publishing = new PublishingOptions
                {
                    TikTokMode = request.TikTokMode,
                    StopBeforeFinalPublishClick = true
                },
                Status = "approved",
                Readiness = JobReadiness.Approved
            });

            countInDay++;
            if (countInDay < request.PostsPerDay)
                current = current.AddMinutes(Math.Max(15, request.MinutesBetweenPosts));
        }

        return result;
    }
}
