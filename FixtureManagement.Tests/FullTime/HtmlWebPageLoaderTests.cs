using System.Net;
using FixtureManagement.FullTime;

namespace FixtureManagement.Tests.FullTime;

/// <summary>
/// The real HtmlWeb loader, against a tiny web server on localhost (never FullTime): checks the parts
/// FullTimeService relies on – the status comes back instead of an exception, and the page is parsed.
/// </summary>
public sealed class HtmlWebPageLoaderTests : IDisposable
{
    private readonly LocalPageServer server = new();

    [Fact]
    public async Task Loads_and_parses_the_page_with_the_default_user_agent()
    {
        var page = await new HtmlWebPageLoader().LoadAsync(server.BaseUrl + "team");

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.True(page.IsSuccess);
        Assert.NotNull(page.Document.DocumentNode.SelectSingleNode("//div[contains(@class,'fixtures-table')]"));
        Assert.Equal(HtmlWebPageLoader.UserAgent, Assert.Single(server.UserAgents));
    }

    [Fact]
    public async Task A_refusal_comes_back_as_a_status_not_an_exception()
    {
        var page = await new HtmlWebPageLoader().LoadAsync(server.BaseUrl + "blocked");

        Assert.Equal(HttpStatusCode.Forbidden, page.StatusCode);
        Assert.False(page.IsSuccess);
    }

    public void Dispose() => server.Dispose();
}
