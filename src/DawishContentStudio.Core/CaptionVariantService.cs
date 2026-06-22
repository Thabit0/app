namespace DawishContentStudio.Core;

public sealed class CaptionVariantService
{
    private readonly MedicalClaimsGuard _guard = new();

    public CaptionVariants CreateSafeVariants(string productName, string websiteUrl, string storeName = "الدويش")
    {
        productName = string.IsNullOrWhiteSpace(productName) ? "اختيار مميز" : productName.Trim();
        var link = string.IsNullOrWhiteSpace(websiteUrl) ? "اطلبه من الموقع" : websiteUrl.Trim();

        var formal = $"{productName} متوفر الآن في متجر {storeName}. جودة وتغليف يليق بذوقكم. للطلب: {link}";
        var shortText = $"{productName} متوفر الآن لدى {storeName}. للطلب: {link}";
        var marketing = $"اختيار مميز لمحبي الذوق الفاخر من {storeName}. متوفر الآن للطلب من الموقع.";

        return new CaptionVariants(
            _guard.RewriteToSafeMarketing(formal, storeName),
            _guard.RewriteToSafeMarketing(shortText, storeName),
            _guard.RewriteToSafeMarketing(marketing, storeName)
        );
    }
}

public sealed record CaptionVariants(string Formal, string Short, string Marketing);
