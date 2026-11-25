namespace ShortsGuard;

public partial class MainPage : ContentPage
{
    private readonly IDispatcherTimer statusTimer;
    private bool pageVisible;
    private bool windowActive;
    private bool openingDiagnostics;

    public MainPage()
    {
        InitializeComponent();
        RestrictedSettingsHelp.IsVisible = OperatingSystem.IsAndroidVersionAtLeast(33);

        ProtectionSwitch.IsToggled = GuardDevice.ProtectionEnabled;
        ProtectionSwitch.Toggled += (_, args) =>
        {
            GuardDevice.ProtectionEnabled = args.Value;
            UpdateStatus();
        };
        ConfigureSwitch(YouTubeSwitch, ShortsDetector.YouTubePackage);
        ConfigureSwitch(VkSwitch, ClipRules.Vk);
        ConfigureSwitch(VkVideoSwitch, ClipRules.VkVideo);

        statusTimer = Dispatcher.CreateTimer();
        statusTimer.Interval = TimeSpan.FromSeconds(1);
        statusTimer.Tick += (_, _) => UpdateStatus();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        pageVisible = true;
        UpdateStatus();
        UpdateStatusTimer();
    }

    protected override void OnDisappearing()
    {
        pageVisible = false;
        UpdateStatusTimer();
        base.OnDisappearing();
    }

    internal void SetWindowActive(bool active)
    {
        windowActive = active;
        if (windowActive && pageVisible)
            UpdateStatus();

        UpdateStatusTimer();
    }

    private void UpdateStatusTimer()
    {
        if (windowActive && pageVisible)
            statusTimer.Start();
        else
            statusTimer.Stop();
    }

    private static void ConfigureSwitch(Switch toggle, string package)
    {
        toggle.IsToggled = GuardDevice.IsAppEnabled(package);
        toggle.Toggled += (_, args) => GuardDevice.SetAppEnabled(package, args.Value);
    }

    private void UpdateStatus()
    {
        var (configured, connected, exits) = GuardDevice.ReadStatus();
        SetupPanel.IsVisible = !configured && !connected;
        ServiceSettingsButton.IsVisible = !SetupPanel.IsVisible;
        ServiceStatus.Text = connected ? "Служба подключена" :
            configured ? "Служба ожидает подключения" : "Служба выключена в Android";
        ProtectionStatus.Text = !ProtectionSwitch.IsToggled ? "Защита выключена" :
            connected ? "Защита активна" : "Защита ожидает службу";
        ProtectionStatus.TextColor = ProtectionSwitch.IsToggled && connected
            ? (Color)Application.Current!.Resources["Accent"] : Color.FromArgb("#FFD180");
        ExitCount.Text = $"Команд выхода: {exits}";
    }

    private async void OnSetupClicked(object? sender, EventArgs args)
    {
        if (await OpenAccessibilitySettingsAsync())
            ProtectionSwitch.IsToggled = true;
    }

    private async void OnAccessibilitySettingsClicked(object? sender, EventArgs args) =>
        await OpenAccessibilitySettingsAsync();

    private async Task<bool> OpenAccessibilitySettingsAsync()
    {
        if (GuardDevice.OpenAccessibilitySettings())
            return true;

        await DisplayAlertAsync("Настройки Android",
            "Откройте специальные возможности в системных настройках и выберите BlockShotsStop.", "Закрыть");
        return false;
    }

    private async void OnRestrictedSettingsHelpClicked(object? sender, EventArgs args)
    {
        bool openAppInfo = await DisplayAlertAsync("Ограниченные настройки",
            "Если Android показывает сообщение об ограниченной настройке, откройте сведения о BlockShotsStop. " +
            "В меню ⋮ выберите «Разрешить ограниченные настройки», если этот пункт доступен и вы доверяете приложению. " +
            "Затем вернитесь сюда и включите службу.",
            "Сведения о приложении", "Закрыть");
        if (openAppInfo && !GuardDevice.OpenAppInfo())
            await DisplayAlertAsync("Настройки Android",
                "Откройте «Настройки → Приложения → BlockShotsStop».", "Закрыть");
    }

    private async void OnDiagnosticsClicked(object? sender, EventArgs args)
    {
        if (openingDiagnostics)
            return;

        openingDiagnostics = true;
        try
        {
            await Navigation.PushAsync(new DiagnosticsPage());
        }
        finally
        {
            openingDiagnostics = false;
        }
    }

    private void OnOpenYouTubeClicked(object? sender, EventArgs args) => OpenApp(ShortsDetector.YouTubePackage, "YouTube");
    private void OnOpenVkClicked(object? sender, EventArgs args) => OpenApp(ClipRules.Vk, "ВКонтакте");
    private void OnOpenVkVideoClicked(object? sender, EventArgs args) => OpenApp(ClipRules.VkVideo, "VK Видео");

    private async void OpenApp(string package, string name)
    {
        if (!GuardDevice.OpenApp(package))
            await DisplayAlertAsync(name, "Приложение не найдено.", "Закрыть");
    }

    private async void OnAboutClicked(object? sender, EventArgs args) =>
        await DisplayAlertAsync($"BlockShotsStop {AppInfo.Current.VersionString}",
            "Приложение выходит из распознанных коротких лент YouTube, ВКонтакте и VK Видео. " +
            "Для работы включите службу специальных возможностей Android и защиту.\n\n" +
            "Служба анализирует структуру экрана и обозначения вкладок. Диагностика хранится на телефоне. " +
            "Переписка, названия роликов, видео и звук не сохраняются. Интернет не используется.", "Закрыть");
}
