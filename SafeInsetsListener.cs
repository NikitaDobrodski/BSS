using Android.OS;
using Android.Views;

namespace ShortsGuard;

// Padding belongs to the viewport, outside the scrolling content, so every
// button remains above navigation controls even at the end of a long screen.
internal sealed class SafeInsetsListener : Java.Lang.Object, View.IOnApplyWindowInsetsListener
{
    public WindowInsets OnApplyWindowInsets(View? view, WindowInsets? insets)
    {
        if (view is null || insets is null) return insets!;
        if (OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            var safe = insets.GetInsets(WindowInsets.Type.SystemBars() | WindowInsets.Type.DisplayCutout());
            view.SetPadding(safe.Left, safe.Top, safe.Right, safe.Bottom);
        }
        else
        {
#pragma warning disable CA1422 // Legacy APIs are needed on Android 8–10 only.
            int left = insets.SystemWindowInsetLeft;
            int top = insets.SystemWindowInsetTop;
            int right = insets.SystemWindowInsetRight;
            int bottom = insets.SystemWindowInsetBottom;
#pragma warning restore CA1422
            if (OperatingSystem.IsAndroidVersionAtLeast(28) && insets.DisplayCutout is { } cutout)
            {
                left = Math.Max(left, cutout.SafeInsetLeft);
                top = Math.Max(top, cutout.SafeInsetTop);
                right = Math.Max(right, cutout.SafeInsetRight);
                bottom = Math.Max(bottom, cutout.SafeInsetBottom);
            }
            view.SetPadding(left, top, right, bottom);
        }
        return insets;
    }
}
