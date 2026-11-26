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
    var pager = Node(test.Id, new(0, 0, test.Width, test.Height)) with { Visible = test.Visible };
    var root = Node(null, new(0, 0, test.WindowWidth, 2000), pager);
    var screen = ScreenRecognizer.Recognize(instagram, root);
    if (screen.ShouldBlock != test.Expected)
        throw new Exception($"FAILED: {test.Name}");
    Console.WriteLine($"PASS: {test.Name}");
}
if (!ClipRules.IsSupported(instagram) ||
    new ClipScreen(instagram, [viewer], true, []).ShouldBlock ||
    new ClipScreen("com.example.other", [viewer], false, [], true).ShouldBlock)
    throw new Exception("Instagram package routing failed");
Console.WriteLine("PASS: Instagram package routing");

const string tikTok = ClipRules.TikTok;
const string feedPager = ClipRules.TikTokFeedPagerId;
const string feedWrapper = ClipRules.TikTokFeedWrapperId;
const string videoContainer = ClipRules.TikTokVideoContainerId;
const string videoPlayer = ClipRules.TikTokPlayerId;
var tikTokCases = new (string Name, string Id, string Parent, bool Visible, int Height, string[] VisibleIds, bool Expected)[]
{
    ("TikTok 47.9.3 For You feed", feedPager, feedWrapper, true, 2135, [videoContainer, videoPlayer], true),
    ("TikTok app-wide pager", feedPager, tikTok + ":id/jqi", true, 2400, [videoContainer, videoPlayer], false),
    ("TikTok hidden feed pager", feedPager, feedWrapper, false, 2135, [videoContainer, videoPlayer], false),
    ("TikTok offscreen feed pager", feedPager, feedWrapper, true, 0, [videoContainer, videoPlayer], false),
    ("TikTok search thumbnail", feedPager, feedWrapper, true, 600, [videoContainer, videoPlayer], false),
    ("TikTok profile without a video player", feedPager, feedWrapper, true, 2135, [videoContainer], false),
    ("TikTok inbox without a video container", feedPager, feedWrapper, true, 2135, [videoPlayer], false),
    ("Foreign pager ID in TikTok", viewer, feedWrapper, true, 2135, [videoContainer, videoPlayer], false)
};
foreach (var test in tikTokCases)
{
    var pagerBounds = new ScreenBounds(0, 0, 1080, test.Height);
    var playerNodes = test.VisibleIds.Select(id => Node(id, pagerBounds)).ToArray();
    var pager = Node(test.Id, pagerBounds, playerNodes) with { Visible = test.Visible };
    var wrapper = Node(test.Parent, pagerBounds, pager);
    var screen = ScreenRecognizer.Recognize(tikTok, Node(null, new(0, 0, 1080, 2400), wrapper));
    if (screen.ShouldBlock != test.Expected) throw new Exception($"FAILED: {test.Name}");
    Console.WriteLine($"PASS: {test.Name}");
}
if (!ClipRules.IsSupported(tikTok) || ClipRules.IsVideoPagerId(instagram, feedPager, feedWrapper))
    throw new Exception("TikTok package routing failed");
var window = new ScreenBounds(0, 0, 1080, 2400);
var feedBounds = new ScreenBounds(0, 0, 1080, 2135);
var player = Node(videoContainer, feedBounds, Node(videoPlayer, feedBounds));
var emptyFeed = Node(feedWrapper, feedBounds, Node(feedPager, feedBounds));
var siblingPlayer = ScreenRecognizer.Recognize(tikTok, Node(null, window, emptyFeed, player));
if (!siblingPlayer.HasFullscreenVideoPager || !siblingPlayer.Ids.Contains(videoPlayer) || siblingPlayer.ShouldBlock)
    throw new Exception("TikTok player outside the fullscreen pager must not block");
var insideFeed = Node(feedWrapper, feedBounds, Node(feedPager, feedBounds, player));
if (!ScreenRecognizer.Recognize(tikTok, Node(null, window, insideFeed)).ShouldBlock)
    throw new Exception("TikTok player nested inside the fullscreen pager must block");
Console.WriteLine("PASS: TikTok package routing");
Console.WriteLine("PASS: TikTok player outside the fullscreen pager remains open");
Console.WriteLine("PASS: TikTok nested player blocks");

var tabBounds = new ScreenBounds(0, 2160, 320, 2400);
var tabLabel = Node(null, tabBounds) with { ClipsLabel = true };
var tab = Node(null, tabBounds, Node(null, tabBounds, tabLabel)) with { Selected = true, Clickable = true };
var vkTabScreen = ScreenRecognizer.Recognize(ClipRules.Vk, Node(null, window, tab));
if (!vkTabScreen.ShouldBlock || !vkTabScreen.Structure.Any(line => line.EndsWith("label=CLIPS", StringComparison.Ordinal)))
    throw new Exception("VK Clips label must inherit selected and interactive states from its navigation tab");
Console.WriteLine("PASS: VK selected tab recognition from its accessibility tree");

var chromeCases = new (string Name, string? Address, BrowserShortsState Expected)[]
{
    ("Mobile Shorts link", "https://m.youtube.com/shorts/abcdefghijk", BrowserShortsState.YouTubeShorts),
    ("Shorts address without scheme", "youtube.com/shorts/abcdefghijk", BrowserShortsState.YouTubeShorts),
    ("Shorts feed landing page", "https://www.youtube.com/shorts?feature=share", BrowserShortsState.YouTubeShorts),
    ("Shorts address with a URL in the query", "m.youtube.com/shorts/abcdefghijk?next=https://example.com", BrowserShortsState.YouTubeShorts),
    ("Regular YouTube video", "https://m.youtube.com/watch?v=abcdefghijk", BrowserShortsState.OtherPage),
    ("Only the YouTube host is visible", "m.youtube.com", BrowserShortsState.YouTubeWithoutPath),
    ("YouTube search containing a Shorts link", "https://www.youtube.com/results?search_query=youtube.com%2Fshorts%2F", BrowserShortsState.OtherPage),
    ("Lookalike YouTube host", "https://youtube.com.example.org/shorts/abcdefghijk", BrowserShortsState.OtherPage),
    ("Another domain with a Shorts path", "https://example.org/shorts/abcdefghijk", BrowserShortsState.OtherPage),
    ("User info spoofing the host", "https://youtube.com@example.org/shorts/abcdefghijk", BrowserShortsState.OtherPage),
    ("URL with embedded user info", "https://example.org@youtube.com/shorts/abcdefghijk", BrowserShortsState.OtherPage),
    ("Unrelated route prefixed with Shorts", "youtube.com/shortstuff", BrowserShortsState.OtherPage),
    ("Wrong scheme", "ftp://youtube.com/shorts/abcdefghijk", BrowserShortsState.OtherPage),
    ("Search input containing spaces", "youtube.com/shorts/abcdefghijk search phrase", BrowserShortsState.OtherPage),
    ("Hidden address bar", null, BrowserShortsState.Unavailable)
};
foreach (var test in chromeCases)
{
    var state = BrowserShortsDetector.ClassifyAddress(test.Address);
    var screen = new ClipScreen(BrowserShortsDetector.Chrome, [], false, [], BrowserState: state);
    if (state != test.Expected || screen.ShouldBlock != (test.Expected == BrowserShortsState.YouTubeShorts))
        throw new Exception($"FAILED: {test.Name}");
    Console.WriteLine($"PASS: {test.Name}");
}
if (!ClipRules.IsSupported(BrowserShortsDetector.Chrome) ||
    new ClipScreen(BrowserShortsDetector.Chrome, [], false, [], BrowserState: BrowserShortsState.Editing).ShouldBlock ||
    new ClipScreen("com.example.other", [], false, [], BrowserState: BrowserShortsState.YouTubeShorts).ShouldBlock)
    throw new Exception("Chrome package routing or address editing guard failed");
Console.WriteLine("PASS: Chrome package routing and address editing guard");
Console.WriteLine("Rule checks and report replay passed. APK on-device behavior still needs verification.");

static ScreenNode Node(string? id, ScreenBounds bounds, params ScreenNode[] children) =>
    new(id, "android.view.View", bounds, Visible: true, Selected: false, Clickable: false, Editable: false,
        ClipsLabel: false, Children: children);
