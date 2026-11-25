using Android.Content;
using Android.Content.PM;
using Android.Provider;
using Application = Android.App.Application;

namespace ShortsGuard;

internal static class GuardDevice
{
    private static Context Context => Application.Context;

    internal static bool ProtectionEnabled
    {
        get => GuardState.Enabled(Context);
        set => GuardState.SetEnabled(Context, value);
    }

    internal static bool IsAppEnabled(string package) => GuardState.AppEnabled(Context, package);

    internal static void SetAppEnabled(string package, bool enabled) =>
        GuardState.SetAppEnabled(Context, package, enabled);

    internal static (bool Configured, bool Connected, int Exits) ReadStatus()
    {
        using var service = new ComponentName(Context.PackageName!, "org.nikita.shortsguard.ShortsService");
        string enabledServices = Settings.Secure.GetString(Context.ContentResolver,
            Settings.Secure.EnabledAccessibilityServices) ?? "";
        bool configured = enabledServices.Split(':').Any(name =>
        {
            using var configuredService = ComponentName.UnflattenFromString(name);
            return service.Equals(configuredService);
        });
        return (configured, ShortsService.IsConnected, GuardState.Preferences(Context).GetInt("exits", 0));
    }

    internal static bool OpenAccessibilitySettings()
    {
        using var intent = new Intent(Settings.ActionAccessibilitySettings);
        return StartActivity(intent);
    }

    internal static bool OpenApp(string package)
    {
        using var intent = Context.PackageManager!.GetLaunchIntentForPackage(package);
        return intent is not null && StartActivity(intent);
    }

    internal static string ReadDiagnostics(string package)
    {
        string version;
        try
        {
            using var info = Context.PackageManager!.GetPackageInfo(package, PackageInfoFlags.MetaData);
            version = info?.VersionName ?? "неизвестна";
        }
        catch (PackageManager.NameNotFoundException)
        {
            version = "приложение не найдено";
        }

        var preferences = GuardState.Preferences(Context);
        string lastEvent = preferences.GetString("event:" + package, "ещё нет")!;
        string scan = preferences.GetString("scan:" + package, "ещё нет")!;
        string report = preferences.GetString("report:" + package,
            "Проверок ещё нет. Включите службу и защиту, откройте короткую ленту, подождите 5 секунд и вернитесь через список недавних приложений.")!;
        return $"Версия приложения: {version}\nПоследнее событие: {lastEvent}\nСостояние чтения: {scan}\n{report}";
    }

    private static bool StartActivity(Intent intent)
    {
        intent.AddFlags(ActivityFlags.NewTask);
        try
        {
            Context.StartActivity(intent);
            return true;
        }
        catch (ActivityNotFoundException)
        {
            return false;
        }
    }
}
