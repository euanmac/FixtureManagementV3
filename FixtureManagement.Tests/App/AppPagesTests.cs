using FixtureManagement.FullTime;
using System.Net;
using Bunit;
using FixtureManagement.ApiClient;
using FixtureManagement.App;
using FixtureManagement.App.Components;
using FixtureManagement.App.Layout;
using FixtureManagement.App.Settings;
using Microsoft.AspNetCore.Components;
using LoginPage = FixtureManagement.App.Pages.Login;
using ServerPage = FixtureManagement.App.Pages.Server;

namespace FixtureManagement.Tests.App;

public class LoginPageTests : AppComponentTestBase
{
    private static void FillIn(IRenderedComponent<LoginPage> page, string email, string password)
    {
        page.Find("#email").Change(email);
        page.Find("#password").Change(password);
        page.Find("form").Submit();
    }

    [Fact]
    public void Signs_in_and_goes_back_to_the_page_that_asked()
    {
        Server = _ => StubHttpHandler.Tokens();
        GoTo("login?returnUrl=%2Fteams%2Fdetails%3Fid%3D7");
        var page = Render<LoginPage>();

        FillIn(page, " coach@example.com ", "secret");

        page.WaitForAssertion(() => Assert.Equal("teams/details?id=7", CurrentUrl));
        Assert.True(Session.IsSignedIn);
        Assert.Equal("coach@example.com", Session.Email);   // trimmed
        var request = Assert.Single(Http.Requests);
        Assert.Equal("/api/auth/login", request.RequestUri!.AbsolutePath);
    }

    [Fact]
    public void Ignores_a_return_address_outside_the_app()
    {
        Server = _ => StubHttpHandler.Tokens();
        GoTo("login?returnUrl=https%3A%2F%2Fevil.example.com%2F");
        var page = Render<LoginPage>();

        FillIn(page, "coach@example.com", "secret");

        page.WaitForAssertion(() => Assert.Equal("", CurrentUrl));
    }

    [Fact]
    public void Wrong_password_shows_a_message_and_stays_signed_out()
    {
        Server = _ => StubHttpHandler.Status(HttpStatusCode.Unauthorized);
        var page = Render<LoginPage>();

        FillIn(page, "coach@example.com", "wrong");

        page.WaitForAssertion(() => Assert.Contains("Wrong email or password", page.Find(".alert-danger").TextContent));
        Assert.False(Session.IsSignedIn);
        Assert.Equal("", page.Find("#password").GetAttribute("value") ?? "");   // password field cleared
    }

    [Fact]
    public void No_connection_says_so_and_names_the_server()
    {
        Server = StubHttpHandler.Unreachable;
        var page = Render<LoginPage>();

        FillIn(page, "coach@example.com", "secret");

        page.WaitForAssertion(() => Assert.Contains("Couldn't reach https://tufixturemanager.azurewebsites.net/", page.Find(".alert-danger").TextContent));
    }

    [Fact]
    public void A_server_error_is_reported_as_one()
    {
        Server = _ => StubHttpHandler.Status(HttpStatusCode.ServiceUnavailable);
        var page = Render<LoginPage>();

        FillIn(page, "coach@example.com", "secret");

        page.WaitForAssertion(() => Assert.Contains("(503)", page.Find(".alert-danger").TextContent));
    }

    [Fact]
    public void Checks_the_fields_before_calling_the_server()
    {
        var page = Render<LoginPage>();

        FillIn(page, "not-an-email", "");

        Assert.Contains("That isn't an email address.", page.Markup);
        Assert.Contains("Enter your password.", page.Markup);
        Assert.Empty(Http.Requests);
    }
}

public class ServerPageTests : AppComponentTestBase
{
    [Fact]
    public void Shows_the_current_server()
    {
        var page = Render<ServerPage>();
        Assert.Equal("https://tufixturemanager.azurewebsites.net/", page.Find("#address").GetAttribute("value"));
        Assert.Contains("using the live site", page.Markup);
        Assert.Empty(page.FindAll("button.btn-outline-secondary"));   // no "Use the live site" when already on it
    }

    [Fact]
    public void Switching_server_signs_out_and_goes_to_sign_in()
    {
        SignIn();
        var page = Render<ServerPage>();

        page.Find("#address").Input("tufixturemanager-qa.azurewebsites.net");
        page.Find("button.btn-primary").Click();

        Assert.Equal("https://tufixturemanager-qa.azurewebsites.net/", SettingsStore.ServerAddress);
        Assert.False(Session.IsSignedIn);
        Assert.Equal("login", CurrentUrl);
    }

    [Fact]
    public void Saving_the_same_server_keeps_the_user_signed_in()
    {
        SignIn();
        var page = Render<ServerPage>();

        page.Find("button.btn-primary").Click();

        Assert.True(Session.IsSignedIn);
        Assert.Equal("", CurrentUrl);
    }

    [Fact]
    public void An_invalid_address_is_explained_and_not_saved()
    {
        var page = Render<ServerPage>();

        page.Find("#address").Input("http://tufixturemanager.azurewebsites.net");
        page.Find("button.btn-primary").Click();

        Assert.Contains("Use https://", page.Find(".text-danger").TextContent);
        Assert.Null(SettingsStore.ServerAddress);
    }

    [Fact]
    public void Can_go_back_to_the_live_site()
    {
        SettingsStore.ServerAddress = "https://tufixturemanager-qa.azurewebsites.net/";
        SignIn();
        var page = Render<ServerPage>();
        Assert.Contains("not", page.Find(".text-warning").TextContent);

        page.Find("button.btn-outline-secondary").Click();

        Assert.Null(SettingsStore.ServerAddress);
        Assert.False(Session.IsSignedIn);
        Assert.Equal("login", CurrentUrl);
    }

    [Fact]
    public void Choosing_how_to_read_FullTime_saves_it_and_keeps_the_user_signed_in()
    {
        SignIn();
        var page = Render<ServerPage>();
        Assert.True(page.Find("#loader-htmlweb").HasAttribute("checked"));
        Assert.False(page.Find("#loader-selenium").HasAttribute("checked"));

        page.Find("#loader-selenium").Change(true);

        Assert.Equal(FullTimePageLoaderKind.Selenium, Settings.FullTimePageLoader);
        Assert.True(page.Find("#loader-selenium").HasAttribute("checked"));
        Assert.True(Session.IsSignedIn);
        Assert.Equal("", CurrentUrl);

        page.Find("#loader-htmlweb").Change(true);
        Assert.Equal(FullTimePageLoaderKind.HtmlWeb, Settings.FullTimePageLoader);
    }
}

public class NavMenuTests : AppComponentTestBase
{
    [Fact]
    public void Signed_out_offers_sign_in_only()
    {
        var menu = Render<NavMenu>();
        Assert.NotNull(menu.Find("a[href='login']"));
        Assert.DoesNotContain("Sign out", menu.Markup);
        Assert.DoesNotContain("href=\"settings\"", menu.Markup);   // website-only Admin page
        Assert.DoesNotContain("href=\"schedule\"", menu.Markup);   // not ported yet
    }

    [Fact]
    public void Signed_in_shows_the_user_and_signs_out()
    {
        SignIn("coach@example.com");
        var menu = Render<NavMenu>();
        Assert.Contains("coach@example.com", menu.Markup);
        Assert.Empty(menu.FindAll("a[href='login']"));

        menu.Find("button.nav-link").Click();

        Assert.False(Session.IsSignedIn);
        Assert.Equal("login", CurrentUrl);
        menu.WaitForAssertion(() => Assert.NotNull(menu.Find("a[href='login']")));   // menu updates on sign-out
    }
}

public class ApiErrorContentTests : AppComponentTestBase
{
    [Fact]
    public void Not_signed_in_goes_to_sign_in_and_comes_back_afterwards()
    {
        GoTo("teams/edit?id=3");
        Render<ApiErrorContent>(p => p.Add(c => c.Exception, new ApiAuthenticationException(HttpStatusCode.Unauthorized)));
        Assert.Equal("login?returnUrl=%2Fteams%2Fedit%3Fid%3D3", CurrentUrl);
    }

    [Fact]
    public void Not_allowed_says_so_without_leaving_the_page()
    {
        GoTo("teams");
        var content = Render<ApiErrorContent>(p => p.Add(c => c.Exception, new ApiAuthenticationException(HttpStatusCode.Forbidden)));
        Assert.Contains("permission", content.Markup);
        Assert.Equal("teams", CurrentUrl);
    }

    [Fact]
    public void Connection_problems_offer_a_retry()
    {
        var retried = false;
        var content = Render<ApiErrorContent>(p => p
            .Add(c => c.Exception, new HttpRequestException("No such host is known."))
            .Add(c => c.OnRetry, () => { retried = true; }));

        Assert.Contains("Couldn't reach the server at https://tufixturemanager.azurewebsites.net/", content.Markup);
        content.Find("button").Click();
        Assert.True(retried);
    }
}

public class LoginLinksTests : AppComponentTestBase
{
    [Theory]
    [InlineData("", "login")]
    [InlineData("login?returnUrl=%2Fteams", "login")]   // never "come back to the login page"
    [InlineData("bookings", "login?returnUrl=%2Fbookings")]
    public void Builds_the_sign_in_link_for_the_current_page(string current, string expected)
    {
        GoTo(current);
        Assert.Equal(expected, LoginLinks.LoginUrl(Navigation));
    }
}

public class RoutesTests : AppComponentTestBase
{
    [Fact]
    public void A_signed_out_user_opening_a_protected_page_is_sent_to_sign_in()
    {
        GoTo("teams/create");   // [Authorize] page from FixtureManagement.UI
        var app = Render<Routes>();

        app.WaitForAssertion(() => Assert.Equal("login?returnUrl=%2Fteams%2Fcreate", CurrentUrl));
    }

    [Fact]
    public void A_saved_session_is_restored_before_the_first_page_shows()
    {
        SessionStore.Saved = new FixtureManagement.App.Auth.SavedSession("coach@example.com", "saved-token");
        Server = _ => StubHttpHandler.Tokens("fresh-access", "fresh-refresh");
        GoTo("login");
        var app = Render<Routes>();

        app.WaitForAssertion(() => Assert.Contains("You're signed in as coach@example.com", app.Markup));
        Assert.Equal("/api/auth/refresh", Assert.Single(Http.Requests).RequestUri!.AbsolutePath);
        Assert.Contains("coach@example.com", app.Find(".sidebar").TextContent);
    }

    [Fact]
    public void Unknown_addresses_show_the_not_found_page()
    {
        GoTo("no/such/page");
        var app = Render<Routes>();
        app.WaitForAssertion(() => Assert.Contains("doesn't exist", app.Markup));
    }
}
