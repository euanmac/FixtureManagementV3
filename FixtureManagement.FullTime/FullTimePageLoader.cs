using System.Net;
using HtmlAgilityPack;

namespace FixtureManagement.FullTime;

/// <summary>A downloaded FullTime page: the HTTP status and the parsed document (FullTime sends HTML even with a 403).</summary>
public sealed record FullTimePage(HttpStatusCode StatusCode, HtmlDocument Document)
{
    public bool IsSuccess => (int)StatusCode is >= 200 and < 300;
    public string Html => Document.DocumentNode.OuterHtml;
}

/// <summary>Fetches FullTime pages. One implementation (HtmlWeb); tests substitute a fake so they never go online.</summary>
public interface IFullTimePageLoader
{
    Task<FullTimePage> LoadAsync(string url, CancellationToken cancellationToken = default);
}

/// <summary>
/// Downloads with HtmlAgilityPack's HtmlWeb. Its async loader sends <see cref="HtmlWeb.UserAgent"/> but not
/// PreRequest headers, and doesn't throw on error statuses – <see cref="HtmlWeb.StatusCode"/> carries them.
/// </summary>
public sealed class HtmlWebPageLoader : IFullTimePageLoader
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // Same user agent as before the switch to HtmlWeb.
    public const string UserAgent = "Mozilla/5.0 (Macintosh; Intel Mac OS X 14_0) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Safari/605.1.15";

    public async Task<FullTimePage> LoadAsync(string url, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);

        var web = new HtmlWeb { UserAgent = UserAgent };   // one per request: StatusCode is per instance
        var document = await web.LoadFromWebAsync(url, timeout.Token);
        return new FullTimePage(web.StatusCode, document);
    }
}
