using Android.Content;

namespace ShortsGuard;

internal static class GuardState
{
    internal static ISharedPreferences Preferences(Context context) =>
        context.GetSharedPreferences("guard", FileCreationMode.Private)!;
    internal static bool Enabled(Context context) => Preferences(context).GetBoolean("enabled", false);
    internal static bool EnabledFor(Context context, string package) =>
        Enabled(context) && Preferences(context).GetBoolean("app:" + package, true);
    internal static bool AdsEnabled(Context context) => Enabled(context) &&
        Preferences(context).GetBoolean("skip_vk_ads", false);
    internal static bool BlackoutAds(Context context) => Preferences(context).GetBoolean("blackout_ads", true);
    internal static bool ShouldObserve(Context context, string package) =>
        EnabledFor(context, package) || (package == ClipRules.Vk && AdsEnabled(context));
    internal static void SetEnabled(Context context, bool enabled) =>
        Preferences(context).Edit()!.PutBoolean("enabled", enabled)!.Apply();
}
