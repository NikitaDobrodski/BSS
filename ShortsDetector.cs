namespace ShortsGuard;

// Candidate IDs, to be verified against the installed YouTube version.
// Never match the word "Shorts", a tab, or a recommendation card alone.
public static class ShortsDetector
{
    public const string YouTubePackage = "com.google.android.youtube";
    public static bool IsShorts(string? packageName, IEnumerable<string> visibleIds)
    {
        if (packageName != YouTubePackage) return false;
        var ids = visibleIds.ToHashSet(StringComparer.Ordinal);
        return Has("reel_player_page_container") || Has("shorts_player_view") ||
            (Has("reel_recycler") && (Has("reel_like_button") || Has("reel_comment_button")));
        bool Has(string id) => ids.Contains($"{YouTubePackage}:id/{id}");
    }
}
