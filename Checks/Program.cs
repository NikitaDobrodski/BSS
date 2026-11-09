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
Console.WriteLine("Rule checks and report replay passed. APK on-device behavior still needs verification.");

var adLabels = new (string Text, bool Expected)[]
{
    ("Реклама · 16+", true), ("Реклама • 18+", true), ("Реклама 12+", true),
    ("Реклама", false), ("Обсуждаем рекламу · 16+", false), ("Не реклама · 16+", false),
    ("Реклама · 19+", false), ("Реклама · 16+ подробнее", false), ("Перейти", false)
};
foreach (var sample in adLabels)
    if (AdRules.IsAdLabel(sample.Text) != sample.Expected) throw new Exception($"Ad label mismatch: {sample.Text}");
if (!AdRules.IsCandidate(ClipRules.Vk, true, true, true, true, false)) throw new Exception("Valid ad card rejected");
foreach (var package in new[] { ClipRules.VkVideo, ShortsDetector.YouTubePackage, "com.example.other" })
    if (AdRules.IsCandidate(package, true, true, true, true, false)) throw new Exception("Ad action outside main VK");
if (AdRules.IsCandidate(ClipRules.Vk, false, true, true, true, false) ||
    AdRules.IsCandidate(ClipRules.Vk, true, false, true, true, false) ||
    AdRules.IsCandidate(ClipRules.Vk, true, true, false, true, false) ||
    AdRules.IsCandidate(ClipRules.Vk, true, true, true, false, false) ||
    AdRules.IsCandidate(ClipRules.Vk, true, true, true, true, true)) throw new Exception("Incomplete or clip ad candidate accepted");
var gate = new AdSkipGate();
if (!gate.TryAcquire(1000) || gate.TryAcquire(1500)) throw new Exception("Ad cooldown failed");
gate.ObserveScroll(1600); // A prompt event from automatic scroll must not reset the burst.
if (!gate.TryAcquire(1900) || gate.TryAcquire(2800) || gate.TryAcquire(30000)) throw new Exception("Ad attempt limit failed");
gate.ObserveScroll(31000); // A later scroll starts a new burst.
if (!gate.TryAcquire(31100)) throw new Exception("Later scroll failed to reset budget");
Console.WriteLine("PASS: ad labels, package/structure exclusions, cooldown, two-attempt limit, later scroll reset");

// Direct RecyclerView children from the user's VK 8.201 report, 03.11 12:37.
AdRules.FeedPart[] flatAd =
[
    new("footer_container", false, false),
    new("post_header_profile_container", true, false),
    new("vk_feed_photo_view", false, false),
    new("container", false, true),
    new("description_container", false, false),
    new("post_header_profile_container", false, false)
];
if (!AdRules.HasFlatFeedAd(ClipRules.Vk, flatAd))
    throw new Exception("DEVICE REPORT REPLAY FAILED: ad header and CTA are sibling feed items");
Console.WriteLine("PASS: flat VK ad from 12:37 report");
AdRules.FeedPart[][] excludedFlat =
[
    [new("post_header_profile_container", true, false), new("post_header_profile_container", false, false), new("container", false, true)],
    [new("post_header_profile_container", true, false), new("footer_container", false, false), new("container", false, true)],
    [new("post_header_profile_container", false, false), new("container", false, true)],
    [new("container", false, true)],
    [new("post_header_profile_container", true, false)]
];
foreach (var excluded in excludedFlat)
    if (AdRules.HasFlatFeedAd(ClipRules.Vk, excluded)) throw new Exception("Flat ad crossed a post boundary or lacked evidence");
if (AdRules.HasFlatFeedAd(ClipRules.VkVideo, flatAd)) throw new Exception("Flat VK ad rule crossed package boundary");
var foundRange = AdRules.FindFlatFeedAd(ClipRules.Vk, flatAd);
if (foundRange != new AdRules.FeedRange(1, 4)) throw new Exception("Curtain range includes a neighboring post");
Console.WriteLine("PASS: flat ad boundaries and exact curtain range");
