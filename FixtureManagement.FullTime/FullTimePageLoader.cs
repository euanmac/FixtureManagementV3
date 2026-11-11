using System.Net;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

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

    /// <summary>The User-Agent header sent with each request (shown on the Gameweek page).</summary>
    string UserAgent => "";
}

/// <summary>
/// Downloads with HtmlAgilityPack's HtmlWeb. Its async loader sends <see cref="HtmlWeb.UserAgent"/> but not
/// PreRequest headers, and doesn't throw on error statuses – <see cref="HtmlWeb.StatusCode"/> carries them.
/// </summary>
public sealed class HtmlWebPageLoader(ILogger<HtmlWebPageLoader>? logger = null) : IFullTimePageLoader
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // HtmlWeb's own default user agent (we don't set one).
    public static string UserAgent { get; } = new HtmlWeb().UserAgent;

    string IFullTimePageLoader.UserAgent => UserAgent;

    public async Task<FullTimePage> LoadAsync(string url, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);

        var web = new HtmlWeb();   // one per request: StatusCode is per instance
        logger?.LogInformation("FullTime request {Url} with User-Agent: {UserAgent}", url, web.UserAgent);
        var document = await web.LoadFromWebAsync(url, timeout.Token);
        return new FullTimePage(web.StatusCode, document);
    }
}
