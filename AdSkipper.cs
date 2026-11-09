using Android.Graphics;
using Android.Views.Accessibility;

namespace ShortsGuard;

internal sealed class AdTarget(AccessibilityNodeInfo scroller, Rect bounds) : IDisposable
{
    internal AccessibilityNodeInfo Scroller { get; } = scroller;
    internal Rect Bounds { get; } = bounds;
    public void Dispose() { Scroller.Dispose(); Bounds.Dispose(); }
}

internal static class AdSkipper
{
    internal static AdTarget? FindTarget(AccessibilityNodeInfo root)
    {
        if (root.PackageName?.ToString() != ClipRules.Vk) return null;
        var lists = root.FindAccessibilityNodeInfosByViewId(ClipRules.Vk + ":id/list");
        if (lists is null) return null;
        AdTarget? result = null;
        foreach (var list in lists)
        {
            bool keep = false;
            try
            {
                if (result is not null || !list.VisibleToUser ||
                    list.ClassName?.ToString()?.Contains("RecyclerView", StringComparison.Ordinal) != true) continue;
                var parts = new List<AdRules.FeedPart>();
                var rectangles = new List<(int Left, int Top, int Right, int Bottom)>();
                for (int i = 0; i < list.ChildCount && i < 160; i++)
                {
                    using var child = list.GetChild(i);
                    if (child is null) continue;
                    string id = child.ViewIdResourceName?.Split("/", 2).Last() ?? "";
                    var labels = new Labels();
                    int count = 0;
                    Inspect(child, labels, ref count, 0);
                    parts.Add(new(id, id == "post_header_profile_container" &&
                        (labels.FullAd || labels.BareAd && labels.Age), labels.Cta));
                    using var bounds = new Rect();
                    child.GetBoundsInScreen(bounds);
                    rectangles.Add((bounds.Left, bounds.Top, bounds.Right, bounds.Bottom));
                }
                using var viewport = new Rect();
                list.GetBoundsInScreen(viewport);
                for (int offset = 0; offset < parts.Count;)
                {
                    var range = AdRules.FindFlatFeedAd(ClipRules.Vk, parts.Skip(offset).ToArray());
                    if (range is null) break;
                    int start = offset + range.Value.Start;
                    int end = offset + range.Value.End;
                    int top = Math.Max(viewport.Top, rectangles[start].Top);
                    int bottom = Math.Min(viewport.Bottom, rectangles.Skip(start)
                        .Take(end - start + 1).Max(r => r.Bottom));
                    if (bottom > top && viewport.Width() > 0)
                    {
                        result = new AdTarget(list, new Rect(viewport.Left, top, viewport.Right, bottom));
                        keep = true;
                        break;
                    }
                    offset = end + 1;
                }
            }
            finally { if (!keep) list.Dispose(); }
        }
        return result;
    }

    private sealed class Labels { internal bool FullAd, BareAd, Age, Cta; }
    private static void Inspect(AccessibilityNodeInfo node, Labels labels, ref int count, int depth)
    {
        if (++count > 200 || depth > 12) return;
        // Off-screen header nodes can still describe a partially visible ad.
        // Final bounds must intersect the current feed viewport.
        if (!node.Editable)
            foreach (string? value in new[] { node.Text?.ToString(), node.ContentDescription?.ToString() })
            {
                labels.FullAd |= AdRules.IsAdLabel(value);
                labels.BareAd |= AdRules.IsBareAdLabel(value);
                labels.Age |= AdRules.IsAgeLabel(value);
                labels.Cta |= AdRules.IsCtaLabel(value);
            }
        for (int i = 0; i < node.ChildCount && count < 200; i++)
        {
            using var child = node.GetChild(i);
            if (child is not null) Inspect(child, labels, ref count, depth + 1);
        }
    }
}
