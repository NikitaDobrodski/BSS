namespace ShortsGuard;

public sealed record ClipScreen(string Package, HashSet<string> Ids, bool SelectedClipsTab,
    IReadOnlyList<string> Structure, bool HasFullscreenVideoPager = false, bool HasTikTokVideoPlayer = false)
{
    public bool ShouldBlock => Package switch
    {
        ShortsDetector.YouTubePackage => ShortsDetector.IsShorts(Package, Ids),
        ClipRules.Instagram => HasFullscreenVideoPager,
        ClipRules.TikTok => HasFullscreenVideoPager && HasTikTokVideoPlayer,
        ClipRules.Vk or ClipRules.VkVideo => SelectedClipsTab || ClipRules.HasFullscreenClipPlayer(Package, Ids),
        _ => false
    };
}

public static class ClipRules
{
    public const string Vk = "com.vkontakte.android";
    public const string VkVideo = "com.vk.vkvideo";
    public const string Instagram = "com.instagram.android";
    public const string TikTok = "com.zhiliaoapp.musically";
    public const string InstagramReelsViewerId = Instagram + ":id/clips_viewer_view_pager";
    public const string TikTokFeedPagerId = TikTok + ":id/viewpager";
    public const string TikTokFeedWrapperId = TikTok + ":id/view_pager_layout_wrapper";
    public const string TikTokVideoContainerId = TikTok + ":id/video_container_area";
    public const string TikTokPlayerId = TikTok + ":id/player_view";
    public static bool IsVk(string? package) => package is Vk or VkVideo;
    public static bool IsSupported(string? package) => package is ShortsDetector.YouTubePackage or Instagram or TikTok || IsVk(package);

    public static bool IsVideoPagerId(string package, string? id, string? parentId = null) => package switch
    {
        Instagram => id == InstagramReelsViewerId,
        // TikTok 47.9.3 reuses the same pager ID for its application tabs.
        TikTok => id == TikTokFeedPagerId && parentId == TikTokFeedWrapperId,
        _ => false
    };

    public static bool HasTikTokVideoPlayer(ISet<string> ids) =>
        ids.Contains(TikTokVideoContainerId) && ids.Contains(TikTokPlayerId);

    // A pager can remain in the tree when an app preloads it outside the active screen.
    public static bool HasFullscreenCoverage(int visibleWidth, int visibleHeight, int windowWidth, int windowHeight) =>
        visibleWidth > 0 && visibleHeight > 0 && windowWidth > 0 && windowHeight > 0 &&
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
