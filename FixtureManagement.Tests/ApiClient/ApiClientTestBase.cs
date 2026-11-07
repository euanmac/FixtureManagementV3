using FixtureManagement.ApiClient;
using FixtureManagement.Tests.Api;

namespace FixtureManagement.Tests.ApiClient;

/// <summary>
/// Runs the desktop app's HttpClient services against the real site (in memory, temp database),
/// signed in through /api/auth/login – the same path the desktop app will take.
/// </summary>
public abstract class ApiClientTestBase(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>, IAsyncLifetime
{
    protected TestWebApplicationFactory Factory { get; } = factory;
    protected ApiSession Session { get; } = new();

    public async Task InitializeAsync()
    {
        var signedIn = await Auth().LoginAsync(TestWebApplicationFactory.UserEmail, TestWebApplicationFactory.UserPassword);
        Assert.True(signedIn, "test user should be able to sign in");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    protected ApiAuthClient Auth() => new(Factory.CreateClient(), Session);

    /// <summary>An HttpClient that goes through the BearerTokenHandler, like the desktop app's.</summary>
    protected HttpClient Api() => Factory.CreateDefaultClient(new BearerTokenHandler(Session, Auth()));
}
