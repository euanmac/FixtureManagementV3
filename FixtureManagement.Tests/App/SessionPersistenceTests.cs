using FixtureManagement.ApiClient;
using FixtureManagement.App;
using FixtureManagement.App.Auth;
using FixtureManagement.App.Settings;
using FixtureManagement.Tests.Api;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace FixtureManagement.Tests.App;

/// <summary>
/// Staying signed in between runs, against the real site in memory: the app's own registrations
/// (AddFixtureManagementApp) with the HttpClients routed to the test server.
/// </summary>
public class SessionPersistenceTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private ServiceProvider BuildApp(InMemorySessionStore sessions)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IAppSettingsStore>(new InMemoryAppSettingsStore { ServerAddress = "http://localhost/" });
        services.AddSingleton<ISessionStore>(sessions);
        services.AddFixtureManagementApp();
        services.AddHttpClient<ApiAuthClient>().ConfigurePrimaryHttpMessageHandler(() => factory.Server.CreateHandler());
        services.AddHttpClient<ITeamService, ApiTeamService>().ConfigurePrimaryHttpMessageHandler(() => factory.Server.CreateHandler());
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Signing_in_saves_the_session_and_signing_out_deletes_it()
    {
        var sessions = new InMemorySessionStore();
        await using var app = BuildApp(sessions);
        var persistence = app.GetRequiredService<SessionPersistence>();

        Assert.True(await app.GetRequiredService<ApiAuthClient>().LoginAsync(TestWebApplicationFactory.UserEmail, TestWebApplicationFactory.UserPassword));
        await persistence.FlushAsync();
        Assert.Equal(TestWebApplicationFactory.UserEmail, sessions.Saved?.Email);
        Assert.Equal(app.GetRequiredService<ApiSession>().RefreshToken, sessions.Saved?.RefreshToken);

        app.GetRequiredService<ApiAuthClient>().SignOut();
        await persistence.FlushAsync();
        Assert.Null(sessions.Saved);
    }

    [Fact]
    public async Task Next_run_restores_the_saved_session_and_can_call_the_API()
    {
        var sessions = new InMemorySessionStore();
        await using (var firstRun = BuildApp(sessions))
        {
            firstRun.GetRequiredService<SessionPersistence>();
            await firstRun.GetRequiredService<ApiAuthClient>().LoginAsync(TestWebApplicationFactory.UserEmail, TestWebApplicationFactory.UserPassword);
            await firstRun.GetRequiredService<SessionPersistence>().FlushAsync();
        }
        var savedToken = sessions.Saved!.RefreshToken;

        await using var secondRun = BuildApp(sessions);
        var persistence = secondRun.GetRequiredService<SessionPersistence>();
        await persistence.RestoreAsync();
        await persistence.FlushAsync();

        var session = secondRun.GetRequiredService<ApiSession>();
        Assert.True(session.IsSignedIn);
        Assert.NotNull(session.AccessToken);   // refreshed at start-up
        Assert.NotEqual(savedToken, sessions.Saved?.RefreshToken);   // the new refresh token was saved
        var state = await secondRun.GetRequiredService<AuthenticationStateProvider>().GetAuthenticationStateAsync();
        Assert.Equal(TestWebApplicationFactory.UserEmail, state.User.Identity?.Name);
        Assert.NotNull(await secondRun.GetRequiredService<ITeamService>().GetTeamsAsync());   // authorised API call works
    }

    [Fact]
    public async Task A_saved_session_the_server_rejects_signs_out_and_is_forgotten()
    {
        var sessions = new InMemorySessionStore { Saved = new SavedSession(TestWebApplicationFactory.UserEmail, "not-a-real-token") };
        await using var app = BuildApp(sessions);
        var persistence = app.GetRequiredService<SessionPersistence>();

        await persistence.RestoreAsync();
        await persistence.FlushAsync();

        Assert.False(app.GetRequiredService<ApiSession>().IsSignedIn);
        Assert.Null(sessions.Saved);
    }

    [Fact]
    public async Task Restore_only_runs_once()
    {
        var sessions = new InMemorySessionStore { Saved = new SavedSession(TestWebApplicationFactory.UserEmail, "not-a-real-token") };
        await using var app = BuildApp(sessions);
        var persistence = app.GetRequiredService<SessionPersistence>();
        await persistence.RestoreAsync();
        await persistence.FlushAsync();

        sessions.Saved = new SavedSession("someone@example.com", "another-token");
        await persistence.RestoreAsync();
        Assert.False(app.GetRequiredService<ApiSession>().IsSignedIn);
    }
}

/// <summary>Start-up when the server can't be reached or is having a bad moment: stay signed in.</summary>
public class SessionPersistenceOfflineTests
{
    private static (ServiceProvider App, InMemorySessionStore Sessions) BuildApp(Func<HttpRequestMessage, HttpResponseMessage> server)
    {
        var sessions = new InMemorySessionStore { Saved = new SavedSession("coach@example.com", "saved-token") };
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IAppSettingsStore>(new InMemoryAppSettingsStore());
        services.AddSingleton<ISessionStore>(sessions);
        services.AddFixtureManagementApp();
        services.AddHttpClient<ApiAuthClient>().ConfigurePrimaryHttpMessageHandler(() => new StubHttpHandler(server));
        return (services.BuildServiceProvider(), sessions);
    }

    [Fact]
    public async Task No_connection_keeps_the_saved_session()
    {
        var (app, sessions) = BuildApp(StubHttpHandler.Unreachable);
        await using var _ = app;
        await app.GetRequiredService<SessionPersistence>().RestoreAsync();

        Assert.True(app.GetRequiredService<ApiSession>().IsSignedIn);
        Assert.Equal("saved-token", sessions.Saved?.RefreshToken);
    }

    [Theory]
    [InlineData(System.Net.HttpStatusCode.InternalServerError)]
    [InlineData(System.Net.HttpStatusCode.Forbidden)]            // Azure's "Site Disabled" when a Free site's quota runs out
    [InlineData(System.Net.HttpStatusCode.ServiceUnavailable)]
    public async Task A_server_error_keeps_the_saved_session(System.Net.HttpStatusCode status)
    {
        var (app, sessions) = BuildApp(_ => StubHttpHandler.Status(status));
        await using var _ = app;
        await app.GetRequiredService<SessionPersistence>().RestoreAsync();

        Assert.True(app.GetRequiredService<ApiSession>().IsSignedIn);
        Assert.Equal("saved-token", sessions.Saved?.RefreshToken);
    }
}
