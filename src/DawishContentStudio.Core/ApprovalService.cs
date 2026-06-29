namespace DawishContentStudio.Core;

public sealed class ApprovalService
{
    private readonly MedicalClaimsGuard _guard = new();

    public ApprovalResult Review(PostJob job, string? mediaPath = null)
    {
        var issues = new List<string>();
        var warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(job.Title)) issues.Add("العنوان ناقص.");
        if (job.ScheduledAt < DateTimeOffset.Now.AddMinutes(-1)) warnings.Add("وقت النشر في الماضي.");
        if (!job.Platforms.Instagram && !job.Platforms.TikTok && !job.Platforms.Snapchat) issues.Add("لم يتم اختيار أي منصة.");
        if (job.Platforms.TikTok && !job.Rules.TikTokNoMusic) issues.Add("تيك توك يجب أن يكون بدون صوت.");
        if (job.Platforms.Snapchat && !job.Rules.SnapchatImageOnly) issues.Add("سناب يجب أن يكون صورة فقط.");
        if (job.Platforms.TikTok && job.Publishing.TikTokMode == TikTokPublishMode.SilentVideoOnly)
            warnings.Add("تيك توك مضبوط على فيديو صامت فقط، مع أن الخيار الأفضل لك هو صورة فقط.");

        var violations = _guard.FindViolations(job.Title, job.Captions.Instagram, job.Captions.TikTok, job.Captions.Snapchat);
        if (violations.Count > 0)
            issues.Add("يوجد كلام طبي/علاجي ممنوع: " + string.Join(", ", violations));

        if (string.IsNullOrWhiteSpace(job.Captions.Instagram) && job.Platforms.Instagram) warnings.Add("كابشن إنستغرام ناقص.");
        if (string.IsNullOrWhiteSpace(job.Captions.TikTok) && job.Platforms.TikTok) warnings.Add("كابشن تيك توك ناقص.");
        if (string.IsNullOrWhiteSpace(job.Captions.Snapchat) && job.Platforms.Snapchat) warnings.Add("كابشن سناب ناقص.");
        if (!string.IsNullOrWhiteSpace(mediaPath) && !File.Exists(mediaPath)) issues.Add("الصورة غير موجودة.");

        return new ApprovalResult(issues.Count == 0, issues, warnings);
    }
}

public sealed record ApprovalResult(bool IsApproved, IReadOnlyList<string> Issues, IReadOnlyList<string> Warnings)
{
    public string Summary => IsApproved
        ? (Warnings.Count == 0 ? "جاهز للاعتماد" : "جاهز مع ملاحظات")
        : "يحتاج تعديل";
}
