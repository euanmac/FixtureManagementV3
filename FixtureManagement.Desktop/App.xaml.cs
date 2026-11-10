namespace FixtureManagement.Desktop;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new MainPage())
        {
            Title = "Fixture Manager",
            Width = 1280,
            Height = 860,
            MinimumWidth = 800,
            MinimumHeight = 600,
        };
    }
}
