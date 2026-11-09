using System.Text.RegularExpressions;

namespace ShortsGuard;

public static partial class AdRules
{
    public readonly record struct FeedPart(string ResourceId, bool HeaderAdMark, bool Cta);

    public readonly record struct FeedRange(int Start, int End);
    public static FeedRange? FindFlatFeedAd(string package, IReadOnlyList<FeedPart> parts)
    {
        if (package != ClipRules.Vk) return null;
        int start = -1;
        bool cta = false;
        for (int i = 0; i <= parts.Count; i++)
        {
            bool boundary = i == parts.Count || parts[i].ResourceId is "post_header_profile_container" or "footer_container";
            if (boundary && start >= 0)
            {
                if (cta) return new FeedRange(start, i - 1);
                start = -1;
                cta = false;
            }
            if (i == parts.Count) break;
            if (parts[i].ResourceId == "post_header_profile_container" && parts[i].HeaderAdMark) start = i;
            if (start >= 0) cta |= parts[i].Cta;
        }
        return null;
    }
    public static bool HasFlatFeedAd(string package, IReadOnlyList<FeedPart> parts) => FindFlatFeedAd(package, parts) is not null;
    // Whole short labels only: a normal sentence mentioning ads must not match.
    public static bool IsAdLabel(string? text) => text is not null &&
        AdLabelPattern().IsMatch(text.Trim());
    public static bool IsBareAdLabel(string? text) => text?.Trim() == "Реклама";
    public static bool IsAgeLabel(string? text) => text?.Trim() is "0+" or "6+" or "12+" or "16+" or "18+";
    public static bool IsCtaLabel(string? text) => text?.Trim() == "Перейти";

    public static bool IsCandidate(string package, bool adWithAge, bool hasCta,
        bool inScrollableCard, bool markerVisible, bool clips) =>
        package == ClipRules.Vk && adWithAge && hasCta && inScrollableCard && markerVisible && !clips;

    [GeneratedRegex(@"^Реклама\s*[·•|]?\s*(?:0|6|12|16|18)\+$", RegexOptions.CultureInvariant)]
    private static partial Regex AdLabelPattern();
}

// No timers reset this budget: at most two attempts until a later user scroll.
public sealed class AdSkipGate
{
    private int attempts;
    private long lastAttempt = -1;
    public void ObserveScroll(long now)
    {
        if (lastAttempt >= 0 && now - lastAttempt >= 1500) attempts = 0;
    }
    public bool TryAcquire(long now)
    {
        if (attempts >= 2 || (lastAttempt >= 0 && now - lastAttempt < 750)) return false;
        attempts++;
        lastAttempt = now;
        return true;
    }
}
