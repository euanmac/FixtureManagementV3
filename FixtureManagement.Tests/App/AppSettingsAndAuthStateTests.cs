using FixtureManagement.ApiClient;
using FixtureManagement.App.Auth;
using FixtureManagement.App.Settings;

namespace FixtureManagement.Tests.App;

public class ServerAddressParserTests
{
    [Theory]
    [InlineData("https://tufixturemanager.azurewebsites.net", "https://tufixturemanager.azurewebsites.net/")]
    [InlineData("  tufixturemanager-qa.azurewebsites.net  ", "https://tufixturemanager-qa.azurewebsites.net/")]   // https added
    [InlineData("https://example.org/teams?id=1#top", "https://example.org/")]                                   // path etc. dropped
    [InlineData("https://localhost:7468", "https://localhost:7468/")]
    [InlineData("http://localhost:5000", "http://localhost:5000/")]                                               // http only for this machine
    [InlineData("http://127.0.0.1:5000/", "http://127.0.0.1:5000/")]
    public void Accepts_server_addresses(string typed, string expected)
    {
        Assert.True(ServerAddressParser.TryParse(typed, out var address, out var error));
        Assert.Null(error);
        Assert.Equal(expected, address!.AbsoluteUri);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("http://tufixturemanager.azurewebsites.net")]   // password would cross the network unencrypted
    [InlineData("ftp://example.org")]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://")]
    public void Rejects_anything_else_with_a_message(string? typed)
    {
        Assert.False(ServerAddressParser.TryParse(typed, out var address, out var error));
        Assert.Null(address);
        Assert.False(string.IsNullOrEmpty(error));
    }
}

public class AppSettingsTests
{
    [Fact]
    public void Uses_the_live_site_until_another_server_is_chosen()
    {
        var store = new InMemoryAppSettingsStore();
        var settings = new AppSettings(store);
        Assert.Equal(AppSettings.DefaultServerAddress, settings.ServerAddress);
        Assert.True(settings.IsDefaultServer);

        settings.SetServerAddress(new Uri("https://tufixturemanager-qa.azurewebsites.net/"));
        Assert.Equal("https://tufixturemanager-qa.azurewebsites.net/", store.ServerAddress);
        Assert.False(settings.IsDefaultServer);

        settings.ResetServerAddress();
        Assert.Null(store.ServerAddress);
        Assert.True(settings.IsDefaultServer);
    }

    [Fact]
    public void Choosing_the_live_site_stores_nothing_so_a_future_default_change_applies()
    {
        var store = new InMemoryAppSettingsStore { ServerAddress = "https://other.example.org/" };
        new AppSettings(store).SetServerAddress(AppSettings.DefaultServerAddress);
        Assert.Null(store.ServerAddress);
    }

    [Fact]
    public void A_bad_saved_value_falls_back_to_the_live_site()
    {
        var settings = new AppSettings(new InMemoryAppSettingsStore { ServerAddress = "not a url at all" });
        Assert.Equal(AppSettings.DefaultServerAddress, settings.ServerAddress);
    }
}

public class ApiAuthenticationStateProviderTests
{
    [Fact]
    public async Task Signed_out_until_the_session_has_a_user_then_named_after_them()
    {
        var session = new ApiSession();
        using var provider = new ApiAuthenticationStateProvider(session);
        Assert.False((await provider.GetAuthenticationStateAsync()).User.Identity!.IsAuthenticated);

        session.Restore("coach@example.com", "refresh-token");
        var user = (await provider.GetAuthenticationStateAsync()).User;
        Assert.True(user.Identity!.IsAuthenticated);
        Assert.Equal("coach@example.com", user.Identity.Name);

        session.Clear();
        Assert.False((await provider.GetAuthenticationStateAsync()).User.Identity!.IsAuthenticated);
    }

    [Fact]
    public async Task Tells_the_UI_only_when_the_user_changes_not_on_every_token_refresh()
    {
        var session = new ApiSession();
        using var provider = new ApiAuthenticationStateProvider(session);
        var names = new List<string?>();
        provider.AuthenticationStateChanged += async state => names.Add((await state).User.Identity?.Name);

        session.Restore("coach@example.com", "refresh-1");
        session.Restore("coach@example.com", "refresh-2");   // same user, new token (like a refresh)
        session.Restore("other@example.com", "refresh-3");
        session.Clear();

        Assert.Equal(["coach@example.com", "other@example.com", null], names);
    }
}
