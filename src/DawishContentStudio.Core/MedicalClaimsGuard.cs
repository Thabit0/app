using System.Text.RegularExpressions;

namespace DawishContentStudio.Core;

public sealed class MedicalClaimsGuard
{
    private static readonly string[] ArabicBlocked =
    [
        "يعالج", "يشفي", "علاج", "دواء", "جرعة", "يناسب مرضى",
        "ضغط", "سكري", "سكر", "مناعة", "يقوي المناعة", "هضم", "قولون", "التهاب", "كحة",
        "بلغم", "معدة", "ينحف", "تخسيس", "ألم", "الم", "مسكن", "للمفاصل", "للصدر",
        "للجسم", "صحي", "طبي", "بديل", "للربو", "للحساسية", "للجيوب", "للصداع",
        "اكتئاب", "قلق", "نوم", "يساعد على النوم", "يرخي", "يهدئ الاعصاب"
    ];

    private static readonly string[] EnglishBlocked =
    [
        "treat", "cure", "heal", "medicine", "dose", "dosage", "diabetes", "pressure",
        "immunity", "immune", "digestion", "inflammation", "pain", "weight loss", "slimming",
        "asthma", "allergy", "headache", "anxiety", "depression", "sleep", "medical"
    ];

    public IReadOnlyList<string> FindViolations(params string?[] texts)
    {
        var hits = new List<string>();
        foreach (var text in texts)
        {
            if (string.IsNullOrWhiteSpace(text)) continue;
            var normalized = Normalize(text);

            foreach (var word in ArabicBlocked.Concat(EnglishBlocked))
            {
                if (ContainsBlockedTerm(normalized, word))
                {
                    hits.Add(word);
                }
            }

            if (Regex.IsMatch(normalized, @"(مفيد|ينفع|يستخدم|مناسب)\s+(ل|لل|مع)", RegexOptions.IgnoreCase))
                hits.Add("صيغة فائدة/استخدام علاجي");

            if (Regex.IsMatch(normalized, @"(يساعد|يخفف|يقلل|يحسن)\s+", RegexOptions.IgnoreCase))
                hits.Add("صيغة تأثير صحي/علاجي");
        }

        return hits.Distinct().ToArray();
    }

    public bool IsSafe(params string?[] texts) => FindViolations(texts).Count == 0;

    public string RewriteToSafeMarketing(string? text, string storeName = "الدويش")
    {
        if (string.IsNullOrWhiteSpace(text))
            return $"اختيار مميز من {storeName}. جودة وتغليف يليق بذوقكم، متوفر الآن للطلب من الموقع.";

        var result = text;
        foreach (var word in ArabicBlocked.Concat(EnglishBlocked))
            result = RemoveBlockedTerm(result, word);

        result = Regex.Replace(result, @"(مفيد|ينفع|يستخدم|مناسب)\s+(ل|لل|مع)\s+\S+", "اختيار مميز", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"(يساعد|يخفف|يقلل|يحسن)\s+\S+", "يضيف لمسة مميزة", RegexOptions.IgnoreCase);
        result = Regex.Replace(result, @"\s+", " ").Trim();

        if (result.Length < 20)
            result = $"اختيار مميز من {storeName}. جودة وتغليف يليق بذوقكم، متوفر الآن للطلب من الموقع.";

        return result;
    }

    private static bool ContainsBlockedTerm(string normalizedText, string blockedTerm)
    {
        var normalizedTerm = Normalize(blockedTerm);

        if (normalizedTerm.Contains(' '))
            return normalizedText.Contains(normalizedTerm, StringComparison.OrdinalIgnoreCase);

        // Use word-like boundaries so short medical terms such as "ألم/الم" do not match
        // safe marketing words such as "الموقع" or "المتجر".
        var pattern = $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(normalizedTerm)}(?![\p{{L}}\p{{N}}])";
        return Regex.IsMatch(normalizedText, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static string RemoveBlockedTerm(string text, string blockedTerm)
    {
        var normalizedTerm = Normalize(blockedTerm);
        if (normalizedTerm.Contains(' '))
            return text.Replace(blockedTerm, "", StringComparison.OrdinalIgnoreCase);

        var pattern = $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(blockedTerm)}(?![\p{{L}}\p{{N}}])";
        return Regex.Replace(text, pattern, "", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static string Normalize(string input)
    {
        return input.Replace("أ", "ا").Replace("إ", "ا").Replace("آ", "ا").Replace("ى", "ي").Trim();
    }
}
