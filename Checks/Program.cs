using ShortsGuard;
var cases = new (string Name, string Package, string[] Ids, bool Expected)[]
{
    ("Home with Shorts tab", ShortsDetector.YouTubePackage, ["pivot_bar", "shorts", "reel_shelf"], false),
    ("Regular player", ShortsDetector.YouTubePackage, ["player_view", "like_button", "comment_button"], false),
    ("Search with Shorts recommendations", ShortsDetector.YouTubePackage, ["search_results", "reel_shelf", "reel_like_button"], false),
    ("Shorts page", ShortsDetector.YouTubePackage, ["reel_player_page_container"], true),
    ("Alternative Shorts player", ShortsDetector.YouTubePackage, ["shorts_player_view"], true),
    ("Shorts recycler with controls", ShortsDetector.YouTubePackage, ["reel_recycler", "reel_comment_button"], true),
    ("Recycler alone", ShortsDetector.YouTubePackage, ["reel_recycler"], false),
    ("Other app", "com.example.other", ["reel_player_page_container"], false),
    ("Empty screen", ShortsDetector.YouTubePackage, [], false)
};
foreach (var test in cases)
{
    var ids = test.Ids.Select(id => $"{ShortsDetector.YouTubePackage}:id/{id}");
    if (ShortsDetector.IsShorts(test.Package, ids) != test.Expected)
        throw new Exception($"FAILED: {test.Name}");
    Console.WriteLine($"PASS: {test.Name}");
}
var clipsCases = new (string Name, string Label, bool Selected, bool Interactive, bool Visible, bool Editable, bool Edge, bool Expected)[]
{
    ("Selected clips navigation", "Клипы", true, true, true, false, true, true),
    ("Unselected clips tab on home", "Клипы", false, true, true, false, true, false),
    ("English selected navigation", "Clips", true, true, true, false, true, true),
    ("Ordinary video tab", "Видео", true, true, true, false, true, false),
    ("Message containing clips", "Посмотри Клипы", true, true, true, false, true, false),
    ("Text field named clips", "Клипы", true, true, true, true, true, false),
    ("Selected middle screen content", "Клипы", true, true, true, false, false, false),
    ("Invisible clips tab", "Клипы", true, true, false, false, true, false),
    ("Noninteractive heading", "Клипы", true, false, true, false, true, false)
};
foreach (var test in clipsCases)
{
    bool result = ClipRules.IsSelectedClipsTab(test.Label, test.Selected, test.Interactive,
        test.Visible, test.Editable, test.Edge);
    if (result != test.Expected) throw new Exception($"FAILED: {test.Name}");
    Console.WriteLine($"PASS: {test.Name}");
}
foreach (var package in new[] { ClipRules.Vk, ClipRules.VkVideo, "com.example.other", ShortsDetector.YouTubePackage })
{
    var screen = new ClipScreen(package, [], true, []);
    if (screen.ShouldBlock != ClipRules.IsVk(package)) throw new Exception($"Package routing failed: {package}");
}
Console.WriteLine("PASS: package routing (4 cases)");
foreach (var fixture in new[] { (ClipRules.Vk, true), (ClipRules.VkVideo, false) })
{
    var ids = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Fixtures", fixture.Item1 + ".txt"))
        .ToHashSet(StringComparer.Ordinal);
    var screen = new ClipScreen(fixture.Item1, ids, false, []);
    if (screen.ShouldBlock != fixture.Item2)
        throw new Exception($"DEVICE REPORT REPLAY FAILED: {fixture.Item1}, expected block={fixture.Item2}, got={screen.ShouldBlock}");
    Console.WriteLine($"PASS: device report replay {fixture.Item1}");
}
var actualVkIds = File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Fixtures", ClipRules.Vk + ".txt"))
    .ToHashSet(StringComparer.Ordinal);
foreach (var missing in new[] { "fullscreen_clip_owner_view", "fullscreen_clip_overlay_like_container", "fullscreen_clip_overlay_comment_container" })
{
    var partial = actualVkIds.Where(id => id != $"{ClipRules.Vk}:id/{missing}").ToHashSet(StringComparer.Ordinal);
    if (new ClipScreen(ClipRules.Vk, partial, false, []).ShouldBlock)
        throw new Exception($"Incomplete player must not block: missing {missing}");
}
var noTimeline = actualVkIds.Where(id => id != $"{ClipRules.Vk}:id/clip_seekbar" && id != $"{ClipRules.Vk}:id/clips_wrapper_top_bar")
    .ToHashSet(StringComparer.Ordinal);
if (new ClipScreen(ClipRules.Vk, noTimeline, false, []).ShouldBlock) throw new Exception("Missing clip timeline/header must not block");
if (new ClipScreen(ClipRules.VkVideo, actualVkIds, false, []).ShouldBlock) throw new Exception("Do not match IDs from another package");
Console.WriteLine("PASS: 5 incomplete/cross-package player checks");

const string instagram = ClipRules.Instagram;
const string viewer = ClipRules.InstagramReelsViewerId;
var reelsCases = new (string Name, string Id, bool Visible, int Width, int Height, int WindowWidth, bool Expected)[]
{
    ("Fullscreen Reels player", viewer, true, 1080, 2000, 1080, true),
    ("Hidden Reels player", viewer, false, 1080, 2000, 1080, false),
    ("Offscreen preloaded Reels player", viewer, true, 0, 2000, 1080, false),
    ("Reels preview in feed", viewer, true, 1080, 800, 1080, false),
    ("Fullscreen Story", instagram + ":id/reel_viewer", true, 1080, 2000, 1080, false),
    ("Reels navigation button", instagram + ":id/clips_tab", true, 1080, 2000, 1080, false),
    ("DM message list", instagram + ":id/direct_thread", true, 1080, 2000, 1080, false),
    ("Viewer ID from another package", "com.example.other:id/clips_viewer_view_pager", true, 1080, 2000, 1080, false),
    ("Missing active window bounds", viewer, true, 1080, 2000, 0, false)
};
foreach (var test in reelsCases)
{
    bool match = ClipRules.IsFullscreenInstagramReelsViewer(test.Id, test.Visible,
        test.Width, test.Height, test.WindowWidth, 2000);
    var screen = new ClipScreen(instagram, [test.Id], false, [], match);
    if (screen.ShouldBlock != test.Expected)
        throw new Exception($"FAILED: {test.Name}");
    Console.WriteLine($"PASS: {test.Name}");
}
if (!ClipRules.IsSupported(instagram) ||
    new ClipScreen(instagram, [viewer], true, []).ShouldBlock ||
    new ClipScreen("com.example.other", [viewer], false, [], true).ShouldBlock)
    throw new Exception("Instagram package routing failed");
Console.WriteLine("PASS: Instagram package routing");
Console.WriteLine("Rule checks and report replay passed. APK on-device behavior still needs verification.");
