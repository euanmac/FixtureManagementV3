using System.Net;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace FixtureManagement.FullTime;

/// <summary>
/// Downloads FullTime pages by driving a real Chrome (a visible window by default – see FullTimeOptions.SeleniumHeadless) with Selenium – an alternative to
/// <see cref="HtmlWebPageLoader"/> for when FullTime treats plain HTTP clients differently from a browser.
/// Needs Chrome installed on the machine; Selenium Manager downloads the matching chromedriver on first use.
/// One browser is started on the first request and reused; pages are loaded one at a time (WebDriver isn't
/// thread-safe). Disposing (the DI container does it on shutdown) quits Chrome.
/// </summary>
public sealed class SeleniumPageLoader(FullTimeOptions options, ILogger<SeleniumPageLoader>? logger = null)
    : IFullTimePageLoader, IDisposable
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    private readonly SemaphoreSlim oneAtATime = new(1);
    private ChromeDriver? driver;
    private bool disposed;

    /// <summary>The browser's own user agent; empty until Chrome has started (first request).</summary>
    public string UserAgent { get; private set; } = "";

    public async Task<FullTimePage> LoadAsync(string url, CancellationToken cancellationToken = default)
    {
        await oneAtATime.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            // WebDriver calls are synchronous; keep them off the caller's thread.
            return await Task.Run(() => Load(url), cancellationToken);
        }
        finally
        {
            oneAtATime.Release();
        }
    }

    private FullTimePage Load(string url)
    {
        var browser = driver ??= StartBrowser();
        logger?.LogInformation("FullTime request {Url} via Selenium (Chrome) with User-Agent: {UserAgent}", url, UserAgent);
        try
        {
            browser.Navigate().GoToUrl(url);
        }
        catch (WebDriverException ex)
        {
            // Timeouts or a crashed browser: start a fresh one next time.
            logger?.LogWarning(ex, "Selenium couldn't load {Url}; restarting Chrome on the next request", url);
            QuitBrowser();
            throw new HttpRequestException($"The browser couldn't load the FullTime page: {ex.Message}", ex);
        }

        var document = new HtmlDocument();
        document.LoadHtml(browser.PageSource);
        return new FullTimePage(ReadStatusCode(browser), document);
    }

    /// <summary>
    /// WebDriver doesn't expose the HTTP status. Chrome reports it on the page's navigation timing entry
    /// (responseStatus, Chrome 109+); if it's missing, assume the page loaded (200).
    /// </summary>
    private HttpStatusCode ReadStatusCode(ChromeDriver browser)
    {
        try
        {
            var status = browser.ExecuteScript(
                "const e = performance.getEntriesByType('navigation')[0]; return e && e.responseStatus ? e.responseStatus : 0;");
            var code = Convert.ToInt32(status);
            return code > 0 ? (HttpStatusCode)code : HttpStatusCode.OK;
        }
        catch (WebDriverException ex)
        {
            logger?.LogDebug(ex, "Couldn't read the response status from the browser; assuming 200");
            return HttpStatusCode.OK;
        }
    }

    private ChromeDriver StartBrowser()
    {
        var chrome = new ChromeOptions();
        if (options.SeleniumHeadless)
        {
            chrome.AddArgument("--headless=new");
        }
        chrome.AddArgument("--window-size=1280,1024");

        var service = ChromeDriverService.CreateDefaultService();
        service.HideCommandPromptWindow = true;   // no console window on Windows
        service.SuppressInitialDiagnosticInformation = true;

        logger?.LogInformation("Starting Chrome for FullTime (headless: {Headless})", options.SeleniumHeadless);
        var browser = new ChromeDriver(service, chrome, Timeout);
        browser.Manage().Timeouts().PageLoad = Timeout;
        UserAgent = browser.ExecuteScript("return navigator.userAgent;") as string ?? "";
        return browser;
    }

    private void QuitBrowser()
    {
        try
        {
            driver?.Quit();
        }
        catch (WebDriverException ex)
        {
            logger?.LogDebug(ex, "Chrome didn't quit cleanly");
        }
        driver?.Dispose();
        driver = null;
    }

    public void Dispose()
    {
        oneAtATime.Wait();
        try
        {
            disposed = true;
            QuitBrowser();
        }
        finally
        {
            oneAtATime.Release();
        }
    }
}
