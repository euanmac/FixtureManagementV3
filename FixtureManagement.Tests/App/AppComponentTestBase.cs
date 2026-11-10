using Bunit;
using FixtureManagement.ApiClient;
using FixtureManagement.App.Auth;
using FixtureManagement.App.Settings;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;

namespace FixtureManagement.Tests.App;

/// <summary>
/// Renders the desktop app's own components (login, server setting, menu, error handling) with the real
/// ApiSession / auth state provider / persistence, and a stub HTTP handler in place of the server.
/// Set <see cref="Server"/> before rendering to choose how the "server" answers.
/// </summary>
public abstract class AppComponentTestBase : BunitContext
{
    protected ApiSession Session { get; } = new();
    protected InMemoryAppSettingsStore SettingsStore { get; } = new();
    protected InMemorySessionStore SessionStore { get; } = new();
    protected AppSettings Settings { get; }
    protected StubHttpHandler Http { get; }

    /// <summary>How the stub server answers; defaults to 401 for everything.</summary>
    protected Func<HttpRequestMessage, HttpResponseMessage> Server { get; set; } = _ => StubHttpHandler.Status(System.Net.HttpStatusCode.Unauthorized);

    protected AppComponentTestBase()
    {
        Settings = new AppSettings(SettingsStore);
        Http = new StubHttpHandler(request => Server(request));

        Services.AddSingleton(Session);
        Services.AddSingleton(Settings);
        Services.AddTransient(_ => new ApiAuthClient(new HttpClient(Http, disposeHandler: false) { BaseAddress = Settings.ServerAddress }, Session));
        Services.AddSingleton<ISessionStore>(SessionStore);
        Services.AddSingleton(provider => new SessionPersistence(Session, provider, SessionStore, NullLogger<SessionPersistence>.Instance));

        // Real authorization, not bUnit's fake: bUnit pre-registers a placeholder IAuthorizationService (which throws)
        // and AddAuthorizationCore only TryAdds, so replace it explicitly.
        Services.AddAuthorizationCore();
        Services.Replace(ServiceDescriptor.Transient<IAuthorizationService, DefaultAuthorizationService>());
        Services.AddCascadingAuthenticationState();
        Services.AddSingleton<AuthenticationStateProvider>(new ApiAuthenticationStateProvider(Session));

        SetRendererInfo(new RendererInfo("Interactive", isInteractive: true));
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    protected NavigationManager Navigation => Services.GetRequiredService<NavigationManager>();

    /// <summary>The current URL relative to the app root, e.g. "login?returnUrl=%2Fteams".</summary>
    protected string CurrentUrl => Navigation.ToBaseRelativePath(Navigation.Uri);

    protected void GoTo(string relativeUrl) => Navigation.NavigateTo(relativeUrl);

    /// <summary>Signs in without going through the login page (Restore is the public way to set a session).</summary>
    protected void SignIn(string email = "coach@example.com") => Session.Restore(email, "refresh-0");
}
