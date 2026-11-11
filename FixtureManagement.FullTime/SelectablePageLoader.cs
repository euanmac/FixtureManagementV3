namespace FixtureManagement.FullTime;

/// <summary>
/// Picks HtmlWeb or Selenium on every request, from a setting that can change while the app runs (the desktop
/// app's Server page). Switching back to HtmlWeb closes the Selenium browser so no Chrome window is left open.
/// </summary>
public sealed class SelectablePageLoader(Func<FullTimePageLoaderKind> choose, IFullTimePageLoader htmlWeb, IFullTimePageLoader selenium)
    : IFullTimePageLoader
{
    private IFullTimePageLoader Current => choose() == FullTimePageLoaderKind.Selenium ? selenium : htmlWeb;

    public string UserAgent => Current.UserAgent;

    public Task<FullTimePage> LoadAsync(string url, CancellationToken cancellationToken = default)
    {
        var loader = Current;
        if (loader != selenium && selenium is SeleniumPageLoader browser)
        {
            browser.CloseBrowser();
        }
        return loader.LoadAsync(url, cancellationToken);
    }
}
