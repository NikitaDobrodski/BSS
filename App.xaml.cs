namespace ShortsGuard;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var mainPage = new MainPage();
        var window = new Window(new NavigationPage(mainPage)
        {
            BarBackgroundColor = (Color)Resources["PageBackground"],
            BarTextColor = (Color)Resources["PrimaryText"]
        });
        window.Activated += (_, _) => mainPage.SetWindowActive(true);
        window.Deactivated += (_, _) => mainPage.SetWindowActive(false);
        return window;
    }
}
