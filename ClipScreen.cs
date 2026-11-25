namespace ShortsGuard;

public sealed record ClipScreen(string Package, HashSet<string> Ids, bool SelectedClipsTab,
    IReadOnlyList<string> Structure, bool HasInstagramReelsViewer = false)
{
    public bool ShouldBlock => Package switch
    {
        ShortsDetector.YouTubePackage => ShortsDetector.IsShorts(Package, Ids),
        ClipRules.Instagram => HasInstagramReelsViewer,
        ClipRules.Vk or ClipRules.VkVideo => SelectedClipsTab || ClipRules.HasFullscreenClipPlayer(Package, Ids),
        _ => false
    };
}

public static class ClipRules
{
    public const string Vk = "com.vkontakte.android";
    public const string VkVideo = "com.vk.vkvideo";
    public const string Instagram = "com.instagram.android";
    public const string InstagramReelsViewerId = Instagram + ":id/clips_viewer_view_pager";
    public static bool IsVk(string? package) => package is Vk or VkVideo;
    public static bool IsSupported(string? package) => package is ShortsDetector.YouTubePackage or Instagram || IsVk(package);

    // Instagram can retain a preloaded Reels pager offscreen while showing a Story.
    public static bool IsFullscreenInstagramReelsViewer(string? id, bool visible, int visibleWidth,
        int visibleHeight, int windowWidth, int windowHeight) =>
        id == InstagramReelsViewerId && visible && visibleWidth > 0 && visibleHeight > 0 &&
        windowWidth > 0 && windowHeight > 0 &&
        (double)visibleWidth * visibleHeight >= 0.6 * windowWidth * windowHeight;

    public static bool HasFullscreenClipPlayer(string package, ISet<string> ids)
    {
        if (!IsVk(package)) return false;
        bool Has(string name) => ids.Contains($"{package}:id/{name}");
        // Observed together in the user's VK 8.201 fullscreen clip report.
        // VK Video may share these resources; its fullscreen report is still needed.
        return Has("fullscreen_clip_owner_view") &&
            Has("fullscreen_clip_overlay_like_container") &&
            Has("fullscreen_clip_overlay_comment_container") &&
            (Has("clip_seekbar") || Has("clips_wrapper_top_bar"));
    }

    // Exact navigation labels only. Never retain arbitrary text or descriptions.
    public static bool IsClipsLabel(string? value) =>
        value?.Trim() is "Клипы" or "Clips" or "VK Клипы" or "VK Clips";

    public static bool IsSelectedClipsTab(string? label, bool selected, bool interactive,
        bool visible, bool editable, bool nearScreenEdge) =>
        IsClipsLabel(label) && selected && interactive && visible && !editable && nearScreenEdge;
}
