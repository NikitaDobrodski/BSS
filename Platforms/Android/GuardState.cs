using Android.Content;

namespace ShortsGuard;

internal static class GuardState
{
    internal static ISharedPreferences Preferences(Context context) =>
        context.GetSharedPreferences("guard", FileCreationMode.Private)!;
    internal static bool Enabled(Context context) => Preferences(context).GetBoolean("enabled", false);
    internal static bool EnabledFor(Context context, string package) =>
        Enabled(context) && AppEnabled(context, package);
    internal static bool AppEnabled(Context context, string package) =>
        Preferences(context).GetBoolean("app:" + package, true);
    internal static void SetAppEnabled(Context context, string package, bool enabled) =>
        Preferences(context).Edit()!.PutBoolean("app:" + package, enabled)!.Apply();
    internal static void SetEnabled(Context context, bool enabled) =>
        Preferences(context).Edit()!.PutBoolean("enabled", enabled)!.Apply();
}
