namespace ShortsGuard;

public partial class DiagnosticsPage : ContentPage
{
    private static readonly (string Name, string Package)[] Apps =
    [
        ("YouTube", ShortsDetector.YouTubePackage),
        ("ВКонтакте", ClipRules.Vk),
        ("VK Видео", ClipRules.VkVideo),
        ("Instagram", ClipRules.Instagram),
        ("TikTok", ClipRules.TikTok),
        ("Chrome", BrowserShortsDetector.Chrome)
    ];

    public DiagnosticsPage()
    {
        InitializeComponent();
        Report.HandlerChanged += (_, _) =>
        {
            if (Report.Handler?.PlatformView is Android.Widget.TextView textView)
                textView.SetTextIsSelectable(true);
        };
        AppPicker.ItemsSource = Apps.Select(app => app.Name).ToArray();
        AppPicker.SelectedIndex = 0;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        UpdateReport();
    }

    private async void OnAppChanged(object? sender, EventArgs args)
    {
        UpdateReport();
        if (ReportScroll.Handler is not null)
            await ReportScroll.ScrollToAsync(0, 0, animated: false);
    }

    private void UpdateReport()
    {
        int index = AppPicker.SelectedIndex;
        if (index >= 0)
            Report.Text = GuardDevice.ReadDiagnostics(Apps[index].Package);
    }

    private async void OnCopyClicked(object? sender, EventArgs args)
    {
        await Clipboard.Default.SetTextAsync(Report.Text);
        await DisplayAlertAsync("Диагностика", "Отчёт скопирован.", "Готово");
    }
}
