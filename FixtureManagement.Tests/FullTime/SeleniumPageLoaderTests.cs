using System.Net;
using FixtureManagement.FullTime;

namespace FixtureManagement.Tests.FullTime;

/// <summary>
/// The Selenium loader with a real headless Chrome, against a tiny web server on localhost (never FullTime).
/// Needs Chrome installed (it is on the GitHub runners); Selenium Manager fetches chromedriver.
/// One browser for the whole class, as in the app.
/// </summary>
[Trait("Category", "Browser")]
public sealed class SeleniumPageLoaderTests : IClassFixture<SeleniumPageLoaderTests.Browser>, IDisposable
{
    public sealed class Browser : IDisposable
    {
        public SeleniumPageLoader Loader { get; } = new(new FullTimeOptions { PageLoader = FullTimePageLoaderKind.Selenium, SeleniumHeadless = true });

        public void Dispose() => Loader.Dispose();
    }

    private readonly LocalPageServer server = new();
    private readonly SeleniumPageLoader loader;

    public SeleniumPageLoaderTests(Browser browser) => loader = browser.Loader;

    [Fact]
    public async Task Loads_and_parses_the_page_with_the_browsers_user_agent()
    {
        var page = await loader.LoadAsync(server.BaseUrl + "team");

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.True(page.IsSuccess);
        Assert.NotNull(page.Document.DocumentNode.SelectSingleNode("//div[contains(@class,'fixtures-table')]"));
        Assert.Contains("Chrome", loader.UserAgent);
        Assert.Equal(loader.UserAgent, Assert.Single(server.UserAgents));
    }

    [Fact]
    public async Task A_refusal_comes_back_as_a_status_not_an_exception()
    {
        var page = await loader.LoadAsync(server.BaseUrl + "blocked");

        Assert.Equal(HttpStatusCode.Forbidden, page.StatusCode);
        Assert.False(page.IsSuccess);
    }

    [Fact]
    public async Task Requests_at_the_same_time_share_the_one_browser()
    {
        var pages = await Task.WhenAll(Enumerable.Range(0, 3).Select(i => loader.LoadAsync(server.BaseUrl + $"team{i}")));

        Assert.All(pages, p => Assert.Equal(HttpStatusCode.OK, p.StatusCode));
        Assert.Equal(3, server.UserAgents.Count);
    }

    public void Dispose() => server.Dispose();
}
