using Android.App;
using Android.Content.PM;

namespace ShortsGuard;

[Activity(Name = "org.nikita.shortsguard.MainActivity", Theme = "@style/Maui.MainTheme.NoActionBar",
    MainLauncher = true, Exported = true, LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
        ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public sealed class MainActivity : MauiAppCompatActivity
{
}
