using Microsoft.AspNetCore.Components;

namespace FixtureManagement.UI.Components;

public static class NavigationExtensions
{
    /// <summary>
    /// Navigates to <paramref name="returnUrl"/> if it points at this site (a relative path, or an absolute URL
    /// under the app's base address, as FixtureGrid builds them); otherwise to <paramref name="fallback"/>.
    /// Stops a crafted ?returnUrl=https://elsewhere link sending people off-site after a save.
    /// </summary>
    public static void NavigateToLocal(this NavigationManager navigation, string? returnUrl, string fallback)
    {
        navigation.NavigateTo(IsLocal(navigation, returnUrl) ? returnUrl! : fallback);
    }

    public static bool IsLocal(NavigationManager navigation, string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }
        // Site-relative "/path". Checked first: on macOS/Linux Uri.TryCreate treats "/path" as an absolute file:// URI.
        // Protocol-relative "//host" and "/\host" are rejected because browsers treat them as another site.
        if (url.StartsWith('/'))
        {
            return !url.StartsWith("//") && !url.StartsWith("/\\");
        }
        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute))
        {
            return (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps)
                && url.StartsWith(navigation.BaseUri, StringComparison.OrdinalIgnoreCase);
        }
        // Relative "path" (resolved against the base address), but nothing with a scheme such as "javascript:".
        return !url.Contains(':');
    }
}
