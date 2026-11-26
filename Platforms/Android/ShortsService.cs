using Android.AccessibilityServices;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Views.Accessibility;

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
    private bool browserBackPending;
    private bool connected;

    protected override void OnServiceConnected()
    {
        base.OnServiceConnected();
        connected = true;
        IsConnected = true;
        GuardState.BrowserSubscriptionChanged -= UpdateBrowserSubscription;
        GuardState.BrowserSubscriptionChanged += UpdateBrowserSubscription;
        UpdateBrowserSubscription();
    }

    private void UpdateBrowserSubscription()
    {
        using var info = ServiceInfo;
        if (info?.PackageNames is not { Count: > 0 } packages) return;
        bool includeChrome = GuardState.EnabledFor(this, BrowserShortsDetector.Chrome);
        if (includeChrome == packages.Contains(BrowserShortsDetector.Chrome)) return;
        info.PackageNames = includeChrome
            ? [.. packages, BrowserShortsDetector.Chrome]
            : packages.Where(package => package != BrowserShortsDetector.Chrome).ToArray();
        SetServiceInfo(info);
    }

    public override void OnAccessibilityEvent(AccessibilityEvent? e)
    {
        string? package = e?.PackageName?.ToString();
        if (!connected || IsExitPending(package) || !ClipRules.IsSupported(package) ||
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
            if (!connected || requested is null || IsExitPending(requested) || !GuardState.EnabledFor(this, requested)) return;
            GuardState.Preferences(this).Edit()!.PutString("event:" + requested,
                DateTime.Now.ToString("dd.MM HH:mm:ss"))!.Apply();
            ScanCurrentScreen(requested);
        }, 180);
    }

    private bool IsExitPending(string? package) =>
        package == BrowserShortsDetector.Chrome ? browserBackPending : exiting;

    private void ScanCurrentScreen(string requested)
    {
        var snapshot = ReadScreen(requested);
        string status = snapshot is null ? "Активное окно недоступно или уже сменилось" :
            snapshot.Package != requested ? "Активно другое поддерживаемое приложение" : "Структура получена";
        GuardState.Preferences(this).Edit()!.PutString("scan:" + requested, status)!.Apply();
        if (snapshot is null || snapshot.Package != requested || !GuardState.EnabledFor(this, snapshot.Package)) return;
        SaveDiagnostics(snapshot);
        if (!snapshot.ShouldBlock) return;
        bool browser = snapshot.Package == BrowserShortsDetector.Chrome;
        if (browser)
            browserBackPending = true;
        else
            exiting = true;
        if (PerformGlobalAction(GlobalAction.Back))
        {
            var prefs = GuardState.Preferences(this);
            prefs.Edit()!.PutInt("exits", prefs.GetInt("exits", 0) + 1)!.Apply();
        }
        handler.PostDelayed(() =>
        {
            if (browser)
            {
                browserBackPending = false;
                return;
            }
            // Only the same app may receive fallback Home. Re-read its screen.
            if (connected && GuardState.EnabledFor(this, snapshot.Package))
            {
                var current = ReadScreen(snapshot.Package);
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
            BrowserShortsDetector.Chrome => screen.BrowserState switch
            {
                BrowserShortsState.YouTubeShorts => "Правило: YouTube Shorts в Chrome\n",
                BrowserShortsState.Editing => "Адресная строка в режиме ввода\n",
                BrowserShortsState.YouTubeWithoutPath => "Chrome передал домен YouTube без пути\n",
                BrowserShortsState.OtherPage => "Адрес не совпал с правилом Shorts\n",
                _ => "Адресная строка недоступна\n"
            },
            ClipRules.Instagram => $"Найден полноэкранный Reels: {screen.HasFullscreenVideoPager}\n",
            ClipRules.TikTok => $"Найден полноэкранный пейджер TikTok: {screen.HasFullscreenVideoPager}\n" +
                                $"Найден видеоплеер внутри пейджера: {screen.HasTikTokVideoPlayer}\n",
            _ => $"Выбрана вкладка Клипы: {screen.SelectedClipsTab}\n" +
                 $"Найден полноэкранный клип: {ClipRules.HasFullscreenClipPlayer(screen.Package, screen.Ids)}\n"
        };
        string report = $"BSS\nПакет: {screen.Package}\nВремя: {DateTime.Now:dd.MM HH:mm:ss}\n" +
            $"Правило совпало: {screen.ShouldBlock}\n" + playerDetails +
            (screen.Package == BrowserShortsDetector.Chrome
                ? ""
                : "Структура (без текста экранов):\n" + string.Join("\n", screen.Structure));
        GuardState.Preferences(this).Edit()!.PutString("report:" + screen.Package, report)!.Apply();
    }

    private ClipScreen? ReadScreen(string requested)
    {
        using var root = RootInActiveWindow;
        bool readChromeAddress = requested == BrowserShortsDetector.Chrome && GuardState.EnabledFor(this, requested);
        return AndroidScreenReader.Read(root, requested, readChromeAddress);
    }

    public override void OnInterrupt() { }
    public override void OnDestroy()
    {
        connected = false;
        IsConnected = false;
        GuardState.BrowserSubscriptionChanged -= UpdateBrowserSubscription;
        handler.RemoveCallbacksAndMessages(null);
        handler.Dispose();
        base.OnDestroy();
    }
}
