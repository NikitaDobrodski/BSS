using Android.AccessibilityServices;
using Android.App;
using Android.Content;
using Android.Graphics;
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
    private readonly AdSkipGate adSkipGate = new();
    private bool exiting;
    private bool connected;
    private AdCurtain? curtain;
    private bool curtainCheckPending;

    protected override void OnServiceConnected()
    {
        base.OnServiceConnected();
        connected = true;
        IsConnected = true;
        curtain = new AdCurtain(this);
    }

    public override void OnAccessibilityEvent(AccessibilityEvent? e)
    {
        string? package = e?.PackageName?.ToString();
        if (!connected || exiting || !ClipRules.IsSupported(package) ||
            !GuardState.ShouldObserve(this, package!)) return;
        if (package == ClipRules.Vk && e?.EventType == EventTypes.ViewScrolled)
            adSkipGate.ObserveScroll(SystemClock.ElapsedRealtime());
        pendingPackage = package;
        if (scanPending) return;
        scanPending = true;
        // An event can precede the new active root. Coalesce rather than drop
        // the final event in a transition; inspect the settled window.
        handler.PostDelayed(() =>
        {
            scanPending = false;
            string? requested = pendingPackage;
            if (!connected || exiting || requested is null || !GuardState.ShouldObserve(this, requested)) return;
            GuardState.Preferences(this).Edit()!.PutString("event:" + requested,
                DateTime.Now.ToString("dd.MM HH:mm:ss"))!.Apply();
            ScanCurrentScreen(requested);
        }, 180);
    }

    private void ScanCurrentScreen(string requested)
    {
        if (requested != ClipRules.Vk) curtain?.Hide();
        var snapshot = ReadScreen();
        string status = snapshot is null ? "Активное окно недоступно или уже сменилось" :
            snapshot.Package != requested ? "Активно другое поддерживаемое приложение" : "Структура получена";
        GuardState.Preferences(this).Edit()!.PutString("scan:" + requested, status)!.Apply();
        if (snapshot is null || snapshot.Package != requested || !GuardState.ShouldObserve(this, snapshot.Package)) return;
        SaveDiagnostics(snapshot);
        if (snapshot.ShouldBlock) curtain?.Hide();
        if (!snapshot.ShouldBlock || !GuardState.EnabledFor(this, snapshot.Package))
        {
            TrySkipAd(snapshot);
            return;
        }
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

    private void TrySkipAd(ClipScreen snapshot)
    {
        if (snapshot.Package != ClipRules.Vk || snapshot.ShouldBlock || !GuardState.AdsEnabled(this))
        { curtain?.Hide(); return; }
        using var root = RootInActiveWindow;
        if (root?.PackageName?.ToString() != ClipRules.Vk) { curtain?.Hide(); return; }
        using var target = AdSkipper.FindTarget(root);
        if (target is null)
        {
            curtain?.Hide();
            GuardState.Preferences(this).Edit()!.PutString("ad_status", "Карточка с рекламной отметкой и кнопкой не найдена")!.Apply();
            return;
        }
        if (GuardState.BlackoutAds(this))
        {
            curtain?.Show(target.Bounds);
            GuardState.Preferences(this).Edit()!.PutString("ad_status", "Реклама закрыта чёрной плашкой")!.Apply();
            ScheduleCurtainCheck();
            return;
        }
        curtain?.Hide();
        if (!adSkipGate.TryAcquire(SystemClock.ElapsedRealtime()))
        {
            GuardState.Preferences(this).Edit()!.PutString("ad_status", "Реклама найдена; пауза автоматической прокрутки")!.Apply();
            return;
        }
        bool accepted = target.Scroller.PerformAction(Android.Views.Accessibility.Action.ScrollForward);
        var prefs = GuardState.Preferences(this);
        prefs.Edit()!.PutString("ad_status", accepted ? "Команда прокрутки принята" : "VK отклонил команду прокрутки")!
            .PutInt("ad_scrolls", prefs.GetInt("ad_scrolls", 0) + (accepted ? 1 : 0))!.Apply();
        if (accepted)
        {
            // A tall card can need a second page. The gate prevents more than
            // two attempts; screen and switches are re-read for every attempt.
            handler.PostDelayed(() =>
            {
                if (connected && !exiting && GuardState.AdsEnabled(this)) ScanCurrentScreen(ClipRules.Vk);
            }, 900);
        }
    }

    private void ScheduleCurtainCheck()
    {
        if (curtainCheckPending) return;
        curtainCheckPending = true;
        handler.PostDelayed(() =>
        {
            curtainCheckPending = false;
            if (!connected || !GuardState.AdsEnabled(this) || !GuardState.BlackoutAds(this))
            { curtain?.Hide(); return; }
            // Check only while a curtain is shown: remove it on app switching,
            // dismissal, or changed feed layout, even without a VK event.
            using var root = RootInActiveWindow;
            if (root?.PackageName?.ToString() != ClipRules.Vk) { curtain?.Hide(); return; }
            using var target = AdSkipper.FindTarget(root);
            if (target is null) { curtain?.Hide(); return; }
            curtain?.Show(target.Bounds);
            ScheduleCurtainCheck();
        }, 120);
    }

    private void SaveDiagnostics(ClipScreen screen)
    {
        long now = SystemClock.ElapsedRealtime();
        if (!screen.ShouldBlock && lastDiagnosticAt.TryGetValue(screen.Package, out long previous) &&
            now - previous < 1000) return;
        lastDiagnosticAt[screen.Package] = now;
        string report = $"BSS\nПакет: {screen.Package}\nВремя: {DateTime.Now:dd.MM HH:mm:ss}\n" +
            $"Правило совпало: {screen.ShouldBlock}\nВыбрана вкладка Клипы: {screen.SelectedClipsTab}\n" +
            $"Найден полноэкранный клип: {ClipRules.HasFullscreenClipPlayer(screen.Package, screen.Ids)}\n" +
            "Структура (без текста экранов):\n" + string.Join("\n", screen.Structure);
        GuardState.Preferences(this).Edit()!.PutString("report:" + screen.Package, report)!.Apply();
    }

    private ClipScreen? ReadScreen()
    {
        using var root = RootInActiveWindow;
        string? package = root?.PackageName?.ToString();
        if (root is null || !ClipRules.IsSupported(package)) return null;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var structure = new List<string>();
        using var bounds = new Rect();
        root.GetBoundsInScreen(bounds);
        bool selectedClips = false;
        int visited = 0;
        Walk(root, ids, structure, ref selectedClips, ref visited, 0, 0, 0, bounds);
        return new ClipScreen(package!, ids, selectedClips, structure);
    }

    private static void Walk(AccessibilityNodeInfo node, HashSet<string> ids, List<string> structure,
        ref bool selectedClips, ref int visited, int depth, int selectedSteps, int interactiveSteps, Rect screen)
    {
        if (++visited > 1200 || depth > 45) return;
        bool visible = node.VisibleToUser;
        bool isChecked = node.Checkable && (OperatingSystem.IsAndroidVersionAtLeast(36)
            ? node.CheckedState == CheckedState.True : node.Checked);
        bool ownSelected = node.Selected || isChecked;
        selectedSteps = ownSelected ? 3 : Math.Max(0, selectedSteps - 1);
        interactiveSteps = node.Clickable ? 3 : Math.Max(0, interactiveSteps - 1);
        string? id = node.ViewIdResourceName;
        bool clipsLabel = ClipRules.IsClipsLabel(node.Text?.ToString()) ||
            ClipRules.IsClipsLabel(node.ContentDescription?.ToString());
        bool adLabel = AdRules.IsAdLabel(node.Text?.ToString()) || AdRules.IsAdLabel(node.ContentDescription?.ToString());
        bool adBare = AdRules.IsBareAdLabel(node.Text?.ToString()) || AdRules.IsBareAdLabel(node.ContentDescription?.ToString());
        bool adAge = AdRules.IsAgeLabel(node.Text?.ToString()) || AdRules.IsAgeLabel(node.ContentDescription?.ToString());
        bool adCta = AdRules.IsCtaLabel(node.Text?.ToString()) || AdRules.IsCtaLabel(node.ContentDescription?.ToString());
        using var bounds = new Rect();
        node.GetBoundsInScreen(bounds);
        float centerY = (bounds.Top + bounds.Bottom) / 2f;
        bool nearEdge = screen.Height() > 0 &&
            (centerY <= screen.Top + screen.Height() * 0.25f || centerY >= screen.Top + screen.Height() * 0.75f);
        if (ClipRules.IsSelectedClipsTab(clipsLabel ? "Клипы" : null, selectedSteps > 0,
            interactiveSteps > 0, visible, node.Editable, nearEdge)) selectedClips = true;
        if (visible)
        {
            if (!string.IsNullOrEmpty(id)) ids.Add(id);
            structure.Add($"{depth}: {id ?? "-"} {node.ClassName} " +
                $"selected={ownSelected} clickable={node.Clickable} edge={nearEdge}" +
                (clipsLabel ? " label=CLIPS" : "") + (adLabel ? " label=AD_WITH_AGE" : "") +
                (adBare ? " label=AD" : "") + (adAge ? " label=AGE" : "") + (adCta ? " label=AD_CTA" : ""));
        }
        for (int i = 0; i < node.ChildCount && visited < 1200; i++)
        {
            using var child = node.GetChild(i);
            if (child is not null) Walk(child, ids, structure, ref selectedClips, ref visited,
                depth + 1, selectedSteps, interactiveSteps, screen);
        }
    }

    public override void OnInterrupt() { curtain?.Hide(); }
    public override void OnDestroy()
    {
        connected = false;
        IsConnected = false;
        handler.RemoveCallbacksAndMessages(null);
        curtain?.Dispose();
        handler.Dispose();
        base.OnDestroy();
    }
}
