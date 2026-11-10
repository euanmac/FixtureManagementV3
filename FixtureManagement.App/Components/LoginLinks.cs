using Microsoft.AspNetCore.Components;

namespace FixtureManagement.App.Components;

public static class LoginLinks
{
    /// <summary>"login?returnUrl=…" for the current page, so signing in comes back to it.</summary>
    public static string LoginUrl(NavigationManager navigation)
    {
        var current = navigation.ToBaseRelativePath(navigation.Uri);
        if (current.Length == 0 || current.StartsWith("login", StringComparison.OrdinalIgnoreCase))
        {
            return "login";
        }
        return "login?returnUrl=" + Uri.EscapeDataString("/" + current);
    }
}
