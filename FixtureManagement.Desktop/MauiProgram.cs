using FixtureManagement.App;
using FixtureManagement.App.Auth;
using FixtureManagement.App.Settings;
using Microsoft.Extensions.Logging;

namespace FixtureManagement.Desktop;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();

        // Everything else lives in FixtureManagement.App; the host only says where to keep things.
        builder.Services.AddSingleton<IAppSettingsStore, PreferencesAppSettingsStore>();
        builder.Services.AddSingleton<ISessionStore, SecureStorageSessionStore>();
        builder.Services.AddFixtureManagementApp();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
        builder.Logging.AddConsole();   // visible when started from a terminal (Contents/MacOS/FixtureManagement.Desktop)
        builder.Logging.SetMinimumLevel(LogLevel.Debug);
#endif

        return builder.Build();
    }
}
