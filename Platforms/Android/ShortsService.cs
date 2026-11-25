using Android.AccessibilityServices;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Views.Accessibility;
using Rect = Android.Graphics.Rect;

namespace ShortsGuard;

[Service(Name = "org.nikita.shortsguard.ShortsService", Label = "BlockShotsStop",
    Permission = "android.permission.BIND_ACCESSIBILITY_SERVICE", Exported = true)]
[IntentFilter(new[] { "android.accessibilityservice.AccessibilityService" })]
[MetaData("android.accessibilityservice", Resource = "@xml/accessibility_service")]
public sealed class ShortsService : AccessibilityService
{
    internal static bool IsConnected { get; private set; }
    private readonly Handler handler = new(Looper.MainLooper!);
    private bool scanPending;
    private string? pendingPackage;
    private readonly Dictionary<string, long> lastDiagnosticAt = new();
    private bool exiting;
    private bool connected;

    protected override void OnServiceConnected()
    {
        base.OnServiceConnected();
        connected = true;
        IsConnected = true;
    }

    public override void OnAccessibilityEvent(AccessibilityEvent? e)
    {
        string? package = e?.PackageName?.ToString();
        if (!connected || exiting || !ClipRules.IsSupported(package) ||
            !GuardState.EnabledFor(this, package!)) return;
        pendingPackage = package;
        if (scanPending) return;
        scanPending = true;
        // An event can precede the new active root. Coalesce rather than drop
        // the final event in a transition; inspect the settled window.
        handler.PostDelayed(() =>
        {
            scanPending = false;
            string? requested = pendingPackage;
            if (!connected || exiting || requested is null || !GuardState.EnabledFor(this, requested)) return;
            GuardState.Preferences(this).Edit()!.PutString("event:" + requested,
                DateTime.Now.ToString("dd.MM HH:mm:ss"))!.Apply();
            ScanCurrentScreen(requested);
        }, 180);
    }

    private void ScanCurrentScreen(string requested)
    {
        var snapshot = ReadScreen();
        string status = snapshot is null ? "Активное окно недоступно или уже сменилось" :
            snapshot.Package != requested ? "Активно другое поддерживаемое приложение" : "Структура получена";
        GuardState.Preferences(this).Edit()!.PutString("scan:" + requested, status)!.Apply();
        if (snapshot is null || snapshot.Package != requested || !GuardState.EnabledFor(this, snapshot.Package)) return;
        SaveDiagnostics(snapshot);
        if (!snapshot.ShouldBlock) return;
        exiting = true;
        if (PerformGlobalAction(GlobalAction.Back))
        {
            var prefs = GuardState.Preferences(this);
            prefs.Edit()!.PutInt("exits", prefs.GetInt("exits", 0) + 1)!.Apply();
        }
        handler.PostDelayed(() =>
        {
            // Only the same app may receive fallback Home. Re-read its screen.
            if (connected && GuardState.EnabledFor(this, snapshot.Package))
            {
                var current = ReadScreen();
                if (current?.Package == snapshot.Package && current.ShouldBlock)
                    PerformGlobalAction(GlobalAction.Home);
            }
            exiting = false;
        }, 900);
    }

    private void SaveDiagnostics(ClipScreen screen)
    {
        long now = SystemClock.ElapsedRealtime();
        if (!screen.ShouldBlock && lastDiagnosticAt.TryGetValue(screen.Package, out long previous) &&
            now - previous < 1000) return;
        lastDiagnosticAt[screen.Package] = now;
        string playerDetails = screen.Package switch
        {
            ClipRules.Instagram => $"Найден полноэкранный Reels: {screen.HasFullscreenVideoPager}\n",
            ClipRules.TikTok => $"Найден полноэкранный пейджер TikTok: {screen.HasFullscreenVideoPager}\n" +
                                $"Найден видеоплеер внутри пейджера: {screen.HasTikTokVideoPlayer}\n",
            _ => $"Выбрана вкладка Клипы: {screen.SelectedClipsTab}\n" +
                 $"Найден полноэкранный клип: {ClipRules.HasFullscreenClipPlayer(screen.Package, screen.Ids)}\n"
        };
        string report = $"BSS\nПакет: {screen.Package}\nВремя: {DateTime.Now:dd.MM HH:mm:ss}\n" +
            $"Правило совпало: {screen.ShouldBlock}\n" + playerDetails +
            "Структура (без текста экранов):\n" + string.Join("\n", screen.Structure);
        GuardState.Preferences(this).Edit()!.PutString("report:" + screen.Package, report)!.Apply();
    }

    private ClipScreen? ReadScreen()
    {
        using var root = RootInActiveWindow;
        string? package = root?.PackageName?.ToString();
        if (root is null || package is null || !ClipRules.IsSupported(package)) return null;
        using var bounds = new Rect();
        root.GetBoundsInScreen(bounds);
        var scan = new ScreenScan(package, bounds);
        Walk(root, scan, 0, 0, 0);
        return new ClipScreen(package, scan.Ids, scan.SelectedClips, scan.Structure,
            scan.HasFullscreenPager, scan.HasTikTokVideoPlayer);
    }

    private sealed class ScreenScan(string package, Rect bounds)
    {
        internal string Package { get; } = package;
        internal Rect Bounds { get; } = bounds;
        internal HashSet<string> Ids { get; } = new(StringComparer.Ordinal);
        internal List<string> Structure { get; } = [];
        internal int Visited { get; set; }
        internal bool SelectedClips { get; set; }
        internal bool HasFullscreenPager { get; set; }
        internal bool HasTikTokVideoPlayer { get; set; }
    }

    private static void Walk(AccessibilityNodeInfo node, ScreenScan scan, int depth, int selectedSteps,
        int interactiveSteps, string? parentId = null, HashSet<string>? tikTokPagerIds = null)
    {
        if (++scan.Visited > 1200 || depth > 45) return;
        bool visible = node.VisibleToUser;
        bool isChecked = node.Checkable && (OperatingSystem.IsAndroidVersionAtLeast(36)
            ? node.CheckedState == CheckedState.True : node.Checked);
        bool ownSelected = node.Selected || isChecked;
        selectedSteps = ownSelected ? 3 : Math.Max(0, selectedSteps - 1);
        interactiveSteps = node.Clickable ? 3 : Math.Max(0, interactiveSteps - 1);
        string? id = node.ViewIdResourceName;
        bool clipsLabel = scan.Package is not (ClipRules.Instagram or ClipRules.TikTok) &&
            (ClipRules.IsClipsLabel(node.Text?.ToString()) || ClipRules.IsClipsLabel(node.ContentDescription?.ToString()));
        using var bounds = new Rect();
        node.GetBoundsInScreen(bounds);
        var screen = scan.Bounds;
        string pagerBounds = "";
        HashSet<string>? matchedTikTokPagerIds = null;
        if (visible && ClipRules.IsVideoPagerId(scan.Package, id, parentId))
        {
            int visibleWidth = Math.Max(0, Math.Min(bounds.Right, screen.Right) - Math.Max(bounds.Left, screen.Left));
            int visibleHeight = Math.Max(0, Math.Min(bounds.Bottom, screen.Bottom) - Math.Max(bounds.Top, screen.Top));
            int windowWidth = screen.Width();
            int windowHeight = screen.Height();
            bool fullscreen = ClipRules.HasFullscreenCoverage(visibleWidth, visibleHeight, windowWidth, windowHeight);
            scan.HasFullscreenPager |= fullscreen;
            if (fullscreen && scan.Package == ClipRules.TikTok)
                matchedTikTokPagerIds = new(StringComparer.Ordinal);
            pagerBounds = $" visibleBounds={visibleWidth}x{visibleHeight} windowBounds={windowWidth}x{windowHeight}";
        }
        tikTokPagerIds = matchedTikTokPagerIds ?? tikTokPagerIds;
        float centerY = (bounds.Top + bounds.Bottom) / 2f;
        bool nearEdge = screen.Height() > 0 &&
            (centerY <= screen.Top + screen.Height() * 0.25f || centerY >= screen.Top + screen.Height() * 0.75f);
        if (ClipRules.IsSelectedClipsTab(clipsLabel ? "Клипы" : null, selectedSteps > 0,
            interactiveSteps > 0, visible, node.Editable, nearEdge)) scan.SelectedClips = true;
        if (visible)
        {
            if (!string.IsNullOrEmpty(id))
            {
                scan.Ids.Add(id);
                tikTokPagerIds?.Add(id);
            }
            scan.Structure.Add($"{depth}: {id ?? "-"} {node.ClassName} " +
                $"selected={ownSelected} clickable={node.Clickable} edge={nearEdge}" +
                (clipsLabel ? " label=CLIPS" : "") + pagerBounds);
        }
        for (int i = 0; i < node.ChildCount && scan.Visited < 1200; i++)
        {
            using var child = node.GetChild(i);
            if (child is not null)
                Walk(child, scan, depth + 1, selectedSteps, interactiveSteps, id, tikTokPagerIds);
        }
        if (matchedTikTokPagerIds is not null)
            scan.HasTikTokVideoPlayer |= ClipRules.HasTikTokVideoPlayer(matchedTikTokPagerIds);
    }

    public override void OnInterrupt() { }
    public override void OnDestroy()
    {
        connected = false;
        IsConnected = false;
        handler.RemoveCallbacksAndMessages(null);
        handler.Dispose();
        base.OnDestroy();
    }
}
