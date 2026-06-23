namespace DawishContentStudio.Core;

public enum TikTokPublishMode
{
    ImageOnly,
    ImageFirstSilentVideoFallback,
    SilentVideoOnly
}

public enum JobReadiness
{
    Draft,
    NeedsReview,
    Approved,
    Ready,
    Locked,
    Done,
    Failed
}

public enum LatePostPolicy
{
    PublishIfLessThanSixHoursLate,
    NeedsReviewIfLate,
    SkipIfLate
}
