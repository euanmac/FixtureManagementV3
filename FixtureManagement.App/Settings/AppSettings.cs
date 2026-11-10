namespace FixtureManagement.App.Settings;

/// <summary>
/// Where the app keeps its settings between runs. The MAUI host implements it with Preferences;
/// tests use an in-memory one. Values are plain strings so any key/value store will do.
/// </summary>
public interface IAppSettingsStore
{
    /// <summary>The server's root URL, or null for the default.</summary>
    string? ServerAddress { get; set; }
}

/// <summary>The app's settings, with defaults applied. One per app (singleton).</summary>
public sealed class AppSettings(IAppSettingsStore store)
{
    /// <summary>The live site. Used until the user picks another server (e.g. a test site).</summary>
    public static readonly Uri DefaultServerAddress = new("https://tufixturemanager.azurewebsites.net/");

    /// <summary>The server the app talks to: the saved one if it's valid, otherwise the default.</summary>
    public Uri ServerAddress
    {
        get
        {
            var saved = store.ServerAddress;
            if (saved is null || !ServerAddressParser.TryParse(saved, out var address, out _))
            {
                return DefaultServerAddress;
            }
            return address!;
        }
    }

    public bool IsDefaultServer => ServerAddress == DefaultServerAddress;

    /// <summary>Raised after the server address changes.</summary>
    public event Action? Changed;

    public void SetServerAddress(Uri address)
    {
        store.ServerAddress = address == DefaultServerAddress ? null : address.AbsoluteUri;
        Changed?.Invoke();
    }

    public void ResetServerAddress()
    {
        store.ServerAddress = null;
        Changed?.Invoke();
    }
}

public static class ServerAddressParser
{
    /// <summary>
    /// Turns what the user typed into a server root URL: adds https:// if there's no scheme, drops any path,
    /// query or fragment, and ends with "/". Plain http is only allowed for this machine (local testing),
    /// because the password and tokens would otherwise cross the network unencrypted.
    /// </summary>
    public static bool TryParse(string? text, out Uri? address, out string? error)
    {
        address = null;
        error = null;
        var trimmed = text?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            error = "Enter the server's address, e.g. https://tufixturemanager.azurewebsites.net.";
            return false;
        }
        if (!trimmed.Contains("://"))
        {
            trimmed = "https://" + trimmed;
        }
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var parsed)
            || (parsed.Scheme != Uri.UriSchemeHttps && parsed.Scheme != Uri.UriSchemeHttp)
            || string.IsNullOrEmpty(parsed.Host))
        {
            error = "That isn't a web address. It should look like https://tufixturemanager.azurewebsites.net.";
            return false;
        }
        if (parsed.Scheme == Uri.UriSchemeHttp && !parsed.IsLoopback)
        {
            error = "Use https:// – plain http is only allowed for localhost.";
            return false;
        }
        address = new Uri(parsed.GetLeftPart(UriPartial.Authority) + "/");
        return true;
    }
}
