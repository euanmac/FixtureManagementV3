using FixtureManagement.ApiClient;
using FixtureManagement.App.Auth;
using FixtureManagement.App.Settings;
using FixtureManagement.FullTime;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace FixtureManagement.App;

public static class AppServiceCollectionExtensions
{
    /// <summary>
    /// Everything the desktop app's pages need: the API-backed services (pointed at the server in
    /// <see cref="AppSettings"/>), FullTime scraping (runs on this machine), and sign-in state.
    /// The host must also register an <see cref="IAppSettingsStore"/> and an <see cref="ISessionStore"/>.
    /// </summary>
    public static IServiceCollection AddFixtureManagementApp(this IServiceCollection services)
    {
        services.AddSingleton<AppSettings>();
        services.AddFixtureManagementApi(provider => provider.GetRequiredService<AppSettings>().ServerAddress);
        // The FullTime loader follows the Server page's setting on every request (no restart needed).
        services.AddFullTime(provider =>
        {
            var settings = provider.GetRequiredService<AppSettings>();
            return () => settings.FullTimePageLoader;
        });

        services.AddAuthorizationCore();
        services.AddCascadingAuthenticationState();
        services.AddSingleton<ApiAuthenticationStateProvider>();
        services.AddSingleton<AuthenticationStateProvider>(provider => provider.GetRequiredService<ApiAuthenticationStateProvider>());
        services.AddSingleton<SessionPersistence>();
        return services;
    }
}
