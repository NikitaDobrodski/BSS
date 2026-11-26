using Android.Views.Accessibility;
using Rect = Android.Graphics.Rect;

namespace ShortsGuard;

internal static class AndroidScreenReader
{
    private const int MaxNodes = 1200;
    private const int MaxDepth = 45;

    // The caller owns the active root. This adapter releases any children it obtains.
    internal static ClipScreen? Read(AccessibilityNodeInfo? root, string requested, bool readBrowserAddress)
    {
        string? package = root?.PackageName?.ToString();
        if (root is null || package is null || !ClipRules.IsSupported(package)) return null;
        if (package != requested) return new ClipScreen(package, [], false, []);
        if (BrowserShortsDetector.IsSupported(package))
        {
            var state = readBrowserAddress ? ReadBrowserAddress(root, package) : BrowserShortsState.Unavailable;
            return new ClipScreen(package, [], false, [], BrowserState: state);
        }

        int visited = 0;
        bool readClipsLabels = package is not (ClipRules.Instagram or ClipRules.TikTok);
        var snapshot = Capture(root, readClipsLabels, 0, ref visited);
        return ScreenRecognizer.Recognize(package, snapshot!);
    }

    private static BrowserShortsState ReadBrowserAddress(AccessibilityNodeInfo root, string package)
    {
        bool yandex = package == BrowserShortsDetector.Yandex;
        if (yandex && IsEditingYandexAddress(root, package)) return BrowserShortsState.Editing;
        string addressId = yandex ? ":id/bro_omnibar_address_title_view" : ":id/url_bar";
        var bars = root.FindAccessibilityNodeInfosByViewId(package + addressId);
        if (bars is null) return BrowserShortsState.Unavailable;

        int visibleBars = 0;
        bool editing = false;
        var state = BrowserShortsState.Unavailable;
        foreach (var node in bars)
        {
            using var bar = node;
            if (!bar.VisibleToUser) continue;
            visibleBars++;
            editing |= bar.Focused;
            if (visibleBars == 1 && !editing)
                state = yandex
                    ? BrowserShortsDetector.ClassifyYandexAddress(bar.ContentDescription?.ToString() ?? bar.Text?.ToString())
                    : BrowserShortsDetector.ClassifyAddress(bar.Text?.ToString());
        }

        if (editing) return BrowserShortsState.Editing;
        return visibleBars == 1 ? state : BrowserShortsState.Unavailable;
    }

    private static bool IsEditingYandexAddress(AccessibilityNodeInfo root, string package)
    {
        var fields = root.FindAccessibilityNodeInfosByViewId(package + ":id/suggest_omnibox_query_edit");
        if (fields is null) return false;
        bool editing = false;
        foreach (var field in fields)
        {
            using var node = field;
            editing |= node.VisibleToUser && node.Focused;
        }
        return editing;
    }

    private static ScreenNode? Capture(AccessibilityNodeInfo node, bool readClipsLabels, int depth, ref int visited)
    {
        if (++visited > MaxNodes || depth > MaxDepth) return null;

        using var bounds = new Rect();
        node.GetBoundsInScreen(bounds);
        bool isChecked = node.Checkable && (OperatingSystem.IsAndroidVersionAtLeast(36)
            ? node.CheckedState == CheckedState.True : node.Checked);
        bool clipsLabel = readClipsLabels && (ClipRules.IsClipsLabel(node.Text?.ToString()) ||
            ClipRules.IsClipsLabel(node.ContentDescription?.ToString()));
        string? id = node.ViewIdResourceName;
        string? className = node.ClassName?.ToString();
        var screenBounds = new ScreenBounds(bounds.Left, bounds.Top, bounds.Right, bounds.Bottom);
        bool visible = node.VisibleToUser;
        bool selected = node.Selected || isChecked;
        bool clickable = node.Clickable;
        bool editable = node.Editable;

        List<ScreenNode>? children = null;
        for (int i = 0; i < node.ChildCount && visited < MaxNodes; i++)
        {
            using var child = node.GetChild(i);
            if (child is not null && Capture(child, readClipsLabels, depth + 1, ref visited) is { } captured)
                (children ??= []).Add(captured);
        }
        return new ScreenNode(id, className, screenBounds, visible, selected, clickable, editable, clipsLabel,
            children is null ? [] : children);
    }
}
