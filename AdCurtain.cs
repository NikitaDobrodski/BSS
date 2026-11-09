using Android.AccessibilityServices;
using Android.Content;
using Android.Graphics;
using Android.Views;

namespace ShortsGuard;

internal sealed class AdCurtain : IDisposable
{
    private readonly IWindowManager manager;
    private readonly View view;
    private readonly WindowManagerLayoutParams parameters;
    internal bool Visible { get; private set; }

    internal AdCurtain(AccessibilityService service)
    {
        manager = ((IWindowManager?)service.GetSystemService(Context.WindowService))!;
        view = new View(service);
        view.SetBackgroundColor(Color.Black);
        // Accessibility overlays are trusted windows: touches pass through to
        // VK so the user can keep scrolling. No drawing-over-apps permission.
        parameters = new WindowManagerLayoutParams(1, 1, WindowManagerTypes.AccessibilityOverlay,
            WindowManagerFlags.NotFocusable | WindowManagerFlags.NotTouchable | WindowManagerFlags.LayoutInScreen,
            Format.Opaque) { Gravity = GravityFlags.Top | GravityFlags.Left };
        if (OperatingSystem.IsAndroidVersionAtLeast(30)) parameters.FitInsetsTypes = 0;
    }

    internal void Show(Rect bounds)
    {
        parameters.X = bounds.Left;
        parameters.Y = bounds.Top;
        parameters.Width = bounds.Width();
        parameters.Height = bounds.Height();
        if (Visible) manager.UpdateViewLayout(view, parameters);
        else { manager.AddView(view, parameters); Visible = true; }
    }

    internal void Hide()
    {
        if (!Visible) return;
        manager.RemoveView(view);
        Visible = false;
    }

    public void Dispose() { Hide(); view.Dispose(); parameters.Dispose(); }
}
