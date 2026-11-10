using System.Net;
using System.Net.Sockets;
using System.Text;
using FixtureManagement.FullTime;

namespace FixtureManagement.Tests.FullTime;

/// <summary>
/// The real HtmlWeb loader, against a tiny web server on localhost (never FullTime): checks the parts
/// FullTimeService relies on – the status comes back instead of an exception, and the page is parsed.
/// </summary>
public sealed class HtmlWebPageLoaderTests : IDisposable
{
    private readonly HttpListener listener = new();
    private readonly string baseUrl;
    private readonly List<string?> userAgents = [];

    public HtmlWebPageLoaderTests()
    {
        var port = FreePort();
        baseUrl = $"http://localhost:{port}/";
        listener.Prefixes.Add(baseUrl);
        listener.Start();
        _ = Task.Run(ServeAsync);
    }

    private async Task ServeAsync()
    {
        while (listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync();
            }
            catch (Exception) when (!listener.IsListening)
            {
                return;
            }
            lock (userAgents)
            {
                userAgents.Add(context.Request.UserAgent);
            }
            context.Response.StatusCode = context.Request.Url!.AbsolutePath == "/blocked" ? 403 : 200;
            var body = Encoding.UTF8.GetBytes("""<div class="fixtures-table"><table><tbody><tr><td>L</td></tr></tbody></table></div>""");
            await context.Response.OutputStream.WriteAsync(body);
            context.Response.Close();
        }
    }

    [Fact]
    public async Task Loads_and_parses_the_page_with_the_user_agent()
    {
        var page = await new HtmlWebPageLoader().LoadAsync(baseUrl + "team");

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.True(page.IsSuccess);
        Assert.NotNull(page.Document.DocumentNode.SelectSingleNode("//div[contains(@class,'fixtures-table')]"));
        Assert.Equal(HtmlWebPageLoader.UserAgent, Assert.Single(userAgents));
    }

    [Fact]
    public async Task A_refusal_comes_back_as_a_status_not_an_exception()
    {
        var page = await new HtmlWebPageLoader().LoadAsync(baseUrl + "blocked");

        Assert.Equal(HttpStatusCode.Forbidden, page.StatusCode);
        Assert.False(page.IsSuccess);
    }

    private static int FreePort()
    {
        using var socket = new TcpListener(IPAddress.Loopback, 0);
        socket.Start();
        return ((IPEndPoint)socket.LocalEndpoint).Port;
    }

    public void Dispose()
    {
        listener.Stop();
        listener.Close();
    }
}
