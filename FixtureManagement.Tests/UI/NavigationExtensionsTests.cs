using FixtureManagement.UI.Components;
using Microsoft.AspNetCore.Components;

namespace FixtureManagement.Tests.UI;

public class NavigationExtensionsTests
{
    private sealed class TestNavigationManager : NavigationManager
    {
        public string? NavigatedTo { get; private set; }

        public TestNavigationManager() => Initialize("https://fixtures.example.org/", "https://fixtures.example.org/fixtures");

        protected override void NavigateToCore(string uri, NavigationOptions options) => NavigatedTo = uri;
    }

    [Theory]
    [InlineData("/teams")]                                              // site-relative (an absolute file:// URI to .NET on macOS/Linux)
    [InlineData("/teams/details?id=1&returnUrl=%2Ffixtures")]
    [InlineData("teams/details?id=1")]                                  // relative to the base address
    [InlineData("https://fixtures.example.org/gameweek?matchday=07-Nov-2026")] // absolute, as FixtureGrid builds them
    [InlineData("HTTPS://FIXTURES.EXAMPLE.ORG/teams")]
    public void Local_urls_are_followed(string url)
    {
        var nav = new TestNavigationManager();
        nav.NavigateToLocal(url, "/fallback");
        Assert.Equal(url, nav.NavigatedTo);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("https://evil.example.com/")]
    [InlineData("https://fixtures.example.org.evil.com/")]              // looks similar, different host
    [InlineData("//evil.example.com/path")]                             // protocol-relative
    [InlineData("/\\evil.example.com")]                                 // browsers treat as protocol-relative
    [InlineData("javascript:alert(1)")]
    [InlineData("file:///etc/passwd")]
    [InlineData("ftp://fixtures.example.org/")]
    public void Anything_else_goes_to_the_fallback(string? url)
    {
        var nav = new TestNavigationManager();
        nav.NavigateToLocal(url, "/fallback");
        Assert.Equal("/fallback", nav.NavigatedTo);
    }
}
