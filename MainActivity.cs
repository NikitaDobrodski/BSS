using Android.AccessibilityServices;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Provider;
using Android.Views;
using Android.Views.Accessibility;
using Android.Widget;

namespace ShortsGuard;

[Activity(Name = "org.nikita.shortsguard.MainActivity", Label = "BlockShotsStop", MainLauncher = true, Exported = true)]
public sealed class MainActivity : Activity
{
    private TextView status = null!;
    private readonly Handler statusHandler = new(Looper.MainLooper!);
    private bool screenVisible;
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        var layout = new LinearLayout(this) { Orientation = Orientation.Vertical };
        int pad = Dp(20);
        layout.SetPadding(pad, pad, pad, pad);
        layout.SetBackgroundColor(Color.Rgb(21, 25, 32));
        var scroll = new ScrollView(this) { FillViewport = true };
        scroll.SetBackgroundColor(Color.Rgb(21, 25, 32));
        scroll.AddView(layout);
        scroll.SetOnApplyWindowInsetsListener(new SafeInsetsListener());
        SetContentView(scroll);
        scroll.RequestApplyInsets();
        Label("BlockShotsStop", 25);
        Label("BSS 0.6 • Короткие видео и реклама VK", 15);
        status = Label("", 16);
        status.SetPadding(Dp(12), Dp(12), Dp(12), Dp(12));
        status.SetBackgroundColor(Color.Rgb(32, 39, 48));
        var toggle = new Switch(this) { Text = "Включить защиту", Checked = GuardState.Enabled(this) };
        toggle.SetTextColor(Color.White);
        toggle.SetMinimumHeight(Dp(56));
        toggle.CheckedChange += (_, args) =>
        {
            GuardState.SetEnabled(this, args.IsChecked);
            UpdateStatus();
        };
        layout.AddView(toggle);
        Label("Блокировать в приложениях", 17);
        foreach (var app in new[] { ("YouTube Shorts", ShortsDetector.YouTubePackage),
            ("ВКонтакте — клипы", ClipRules.Vk), ("VK Видео — клипы", ClipRules.VkVideo) })
        {
            string package = app.Item2;
            var appToggle = new Switch(this) { Text = app.Item1,
                Checked = GuardState.Preferences(this).GetBoolean("app:" + package, true) };
            appToggle.SetTextColor(Color.White);
            appToggle.SetMinimumHeight(Dp(52));
            appToggle.CheckedChange += (_, args) =>
                GuardState.Preferences(this).Edit()!.PutBoolean("app:" + package, args.IsChecked)!.Apply();
            layout.AddView(appToggle);
        }
        Label("Дополнительно", 17);
        var adsToggle = new Switch(this) { Text = "Скрывать рекламу ВКонтакте",
            Checked = GuardState.Preferences(this).GetBoolean("skip_vk_ads", false) };
        adsToggle.SetTextColor(Color.White);
        adsToggle.SetMinimumHeight(Dp(56));
        adsToggle.CheckedChange += (_, args) =>
            GuardState.Preferences(this).Edit()!.PutBoolean("skip_vk_ads", args.IsChecked)!.Apply();
        layout.AddView(adsToggle);
        var adMode = new Spinner(this);
        var modeAdapter = new ArrayAdapter<string>(this, Android.Resource.Layout.SimpleSpinnerItem,
            new[] { "Чёрная плашка без прокрутки", "Автоматическая прокрутка" });
        modeAdapter.SetDropDownViewResource(Android.Resource.Layout.SimpleSpinnerDropDownItem);
        adMode.Adapter = modeAdapter;
        adMode.SetMinimumHeight(Dp(52));
        adMode.SetSelection(GuardState.BlackoutAds(this) ? 0 : 1);
        adMode.ItemSelected += (_, args) =>
            GuardState.Preferences(this).Edit()!.PutBoolean("blackout_ads", args.Position == 0)!.Apply();
        layout.AddView(adMode);
        Label("Пробная функция. Плашка закрывает распознанную рекламу, а ленту можно листать вручную. Работает при включённой общей защите. Если плашка мешает — выключи скрытие рекламы.", 14);
        Button("Настроить службу Android", () => StartActivity(new Intent(Settings.ActionAccessibilitySettings)));
        Button("Диагностика распознавания", ShowDiagnosticChooser);
        Label("Проверить блокировку", 17);
        Button("Открыть YouTube", () =>
        {
            var launch = PackageManager!.GetLaunchIntentForPackage(ShortsDetector.YouTubePackage);
            if (launch is not null) StartActivity(launch);
            else Toast.MakeText(this, "Официальный YouTube не найден", ToastLength.Long)!.Show();
        });
        Button("Открыть ВКонтакте", () => OpenApp(ClipRules.Vk));
        Button("Открыть VK Видео", () => OpenApp(ClipRules.VkVideo));
        Button("О приложении и доступе", () =>
        {
            new AlertDialog.Builder(this)!.SetTitle("BlockShotsStop 0.6")!
                .SetMessage("BSS выходит из короткой ленты и по отдельному переключателю скрывает рекламу ВКонтакте: чёрной плашкой или прокруткой. Нужны служба Android и общая защита.\n\nПлашка скрывает изображение, но не отменяет загрузку рекламы, её звук или переход по касанию. Свайпы проходят в VK.\n\nСлужба анализирует структуру экранов YouTube, ВКонтакте и VK Видео, точные обозначения вкладок, рекламы, возраста и кнопки перехода. Переписка, названия роликов, видео и звук не сохраняются. Интернет не используется. Диагностика хранится на телефоне.\n\nСкрытие рекламы экспериментальное; работу и границы плашки нужно проверить на телефоне.")!
                .SetPositiveButton("Понятно", (_, _) => { })!.Show();
        });

        TextView Label(string text, float size)
        {
            var view = new TextView(this) { Text = text, TextSize = size };
            view.Typeface = Typeface.Create("sans-serif", TypefaceStyle.Normal);
            view.SetTextColor(Color.White);
            view.SetPadding(0, 0, 0, pad / 2);
            layout.AddView(view);
            return view;
        }
        void Button(string text, System.Action action)
        {
            var button = new Button(this) { Text = text };
            button.SetAllCaps(false);
            button.SetMinHeight(Dp(48));
            button.LayoutParameters = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent,
                ViewGroup.LayoutParams.WrapContent) { BottomMargin = Dp(6) };
            button.Click += (_, _) => action();
            layout.AddView(button);
        }
    }

    private int Dp(int value) => (int)Math.Ceiling(value * Resources!.DisplayMetrics!.Density);

    private void ShowDiagnosticChooser()
    {
        string[] packages = [ShortsDetector.YouTubePackage, ClipRules.Vk, ClipRules.VkVideo];
        new AlertDialog.Builder(this)!.SetTitle("Диагностика приложения")!
            .SetItems(new[] { "YouTube", "ВКонтакте", "VK Видео" }, (_, args) => ShowDiagnostics(packages[args.Which]))!.Show();
    }

    private void OpenApp(string package)
    {
        var launch = PackageManager!.GetLaunchIntentForPackage(package);
        if (launch is not null) StartActivity(launch);
        else Toast.MakeText(this, "Приложение не найдено", ToastLength.Long)!.Show();
    }

    private void ShowDiagnostics(string package)
    {
        string version;
        try { version = PackageManager!.GetPackageInfo(package, 0)?.VersionName ?? "неизвестна"; }
        catch (Android.Content.PM.PackageManager.NameNotFoundException) { version = "приложение не найдено"; }
        var prefs = GuardState.Preferences(this);
        string report = $"Версия приложения: {version}\nПоследнее событие: {prefs.GetString("event:" + package, "ещё нет")}\n" +
            $"Состояние чтения: {prefs.GetString("scan:" + package, "ещё нет")}\n" + prefs.GetString("report:" + package,
            "Проверок ещё нет. Включи службу и защиту, открой короткую ленту, подожди 5 секунд и вернись сюда через список недавних приложений.");
        if (package == ClipRules.Vk)
            report = $"Скрытие рекламы: {GuardState.AdsEnabled(this)}\nСпособ: {(GuardState.BlackoutAds(this) ? "чёрная плашка" : "прокрутка")}\nСостояние: {prefs.GetString("ad_status", "ещё нет")}\n" + report;
        var text = new TextView(this) { Text = report, TextSize = 12 };
        text.SetTextIsSelectable(true);
        text.SetPadding(20, 10, 20, 10);
        var scroll = new ScrollView(this);
        scroll.AddView(text);
        new AlertDialog.Builder(this)!.SetTitle("Диагностика: " + package)!.SetView(scroll)!
            .SetPositiveButton("Закрыть", (_, _) => { })!
            .SetNeutralButton("Копировать", (_, _) =>
            {
                var clipboard = (ClipboardManager)GetSystemService(ClipboardService)!;
                clipboard.PrimaryClip = ClipData.NewPlainText("BSS диагностика", report);
                Toast.MakeText(this, "Отчёт скопирован", ToastLength.Short)!.Show();
            })!.Show();
    }

    protected override void OnResume()
    {
        base.OnResume();
        screenVisible = true;
        RefreshStatus();
    }

    private void RefreshStatus()
    {
        if (!screenVisible) return;
        UpdateStatus();
        statusHandler.PostDelayed(RefreshStatus, 1000);
    }

    protected override void OnPause()
    {
        screenVisible = false;
        statusHandler.RemoveCallbacksAndMessages(null);
        base.OnPause();
    }

    protected override void OnDestroy()
    {
        statusHandler.RemoveCallbacksAndMessages(null);
        statusHandler.Dispose();
        base.OnDestroy();
    }

    private void UpdateStatus()
    {
        string setting = Settings.Secure.GetString(ContentResolver, Settings.Secure.EnabledAccessibilityServices) ?? "";
        bool configured = setting.Split(':', StringSplitOptions.RemoveEmptyEntries)
            .Any(item => ComponentName.UnflattenFromString(item)?.PackageName == PackageName);
        bool connected = ShortsService.IsConnected;
        bool protection = GuardState.Enabled(this);
        status.Text = $"Служба: {(connected ? "подключена" : configured ? "ожидает подключения" : "выключена в Android")}\n" +
            $"Защита: {(protection ? connected ? "активна" : "ожидает службу" : "выключена")}\n" +
            $"Команд выхода: {GuardState.Preferences(this).GetInt("exits", 0)}\n" +
            $"Прокруток рекламы: {GuardState.Preferences(this).GetInt("ad_scrolls", 0)}";
        status.SetTextColor(protection && connected ? Color.Rgb(132, 224, 194) : Color.Rgb(255, 209, 128));
    }
}
