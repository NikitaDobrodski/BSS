namespace ShortsGuard;

internal readonly record struct ScreenBounds(int Left, int Top, int Right, int Bottom)
{
    internal int Width => Right - Left;
    internal int Height => Bottom - Top;
}

internal sealed record ScreenNode(string? Id, string? ClassName, ScreenBounds Bounds, bool Visible,
    bool Selected, bool Clickable, bool Editable, bool ClipsLabel, IReadOnlyList<ScreenNode> Children);

internal static class ScreenRecognizer
{
    internal static ClipScreen Recognize(string package, ScreenNode root)
    {
        var scan = new ScreenScan(package, root.Bounds);
        Visit(root, scan, 0, 0, 0);
        return new ClipScreen(package, scan.Ids, scan.SelectedClips, scan.Structure,
            scan.HasFullscreenPager, scan.HasTikTokVideoPlayer);
    }

    private sealed class ScreenScan(string package, ScreenBounds bounds)
    {
        internal string Package { get; } = package;
        internal ScreenBounds Bounds { get; } = bounds;
        internal HashSet<string> Ids { get; } = new(StringComparer.Ordinal);
        internal List<string> Structure { get; } = [];
        internal bool SelectedClips { get; set; }
        internal bool HasFullscreenPager { get; set; }
        internal bool HasTikTokVideoPlayer { get; set; }
    }

    private static void Visit(ScreenNode node, ScreenScan scan, int depth, int selectedSteps,
        int interactiveSteps, string? parentId = null, HashSet<string>? tikTokPagerIds = null)
    {
        selectedSteps = node.Selected ? 3 : Math.Max(0, selectedSteps - 1);
        interactiveSteps = node.Clickable ? 3 : Math.Max(0, interactiveSteps - 1);
        var bounds = node.Bounds;
        var screen = scan.Bounds;
        string pagerBounds = "";
        HashSet<string>? matchedTikTokPagerIds = null;
        if (node.Visible && ClipRules.IsVideoPagerId(scan.Package, node.Id, parentId))
        {
            int visibleWidth = Math.Max(0, Math.Min(bounds.Right, screen.Right) - Math.Max(bounds.Left, screen.Left));
            int visibleHeight = Math.Max(0, Math.Min(bounds.Bottom, screen.Bottom) - Math.Max(bounds.Top, screen.Top));
            bool fullscreen = ClipRules.HasFullscreenCoverage(visibleWidth, visibleHeight, screen.Width, screen.Height);
            scan.HasFullscreenPager |= fullscreen;
            if (fullscreen && scan.Package == ClipRules.TikTok)
                matchedTikTokPagerIds = new(StringComparer.Ordinal);
            pagerBounds = $" visibleBounds={visibleWidth}x{visibleHeight} windowBounds={screen.Width}x{screen.Height}";
        }
        tikTokPagerIds = matchedTikTokPagerIds ?? tikTokPagerIds;

        float centerY = (bounds.Top + bounds.Bottom) / 2f;
        bool nearEdge = screen.Height > 0 &&
            (centerY <= screen.Top + screen.Height * 0.25f || centerY >= screen.Top + screen.Height * 0.75f);
        if (ClipRules.IsSelectedClipsTab(node.ClipsLabel ? "Клипы" : null, selectedSteps > 0,
            interactiveSteps > 0, node.Visible, node.Editable, nearEdge)) scan.SelectedClips = true;

        if (node.Visible)
        {
            if (!string.IsNullOrEmpty(node.Id))
            {
                scan.Ids.Add(node.Id);
                tikTokPagerIds?.Add(node.Id);
            }
            scan.Structure.Add($"{depth}: {node.Id ?? "-"} {node.ClassName} " +
                $"selected={node.Selected} clickable={node.Clickable} edge={nearEdge}" +
                (node.ClipsLabel ? " label=CLIPS" : "") + pagerBounds);
        }
        foreach (var child in node.Children)
            Visit(child, scan, depth + 1, selectedSteps, interactiveSteps, node.Id, tikTokPagerIds);
        if (matchedTikTokPagerIds is not null)
            scan.HasTikTokVideoPlayer |= ClipRules.HasTikTokVideoPlayer(matchedTikTokPagerIds);
    }
}
