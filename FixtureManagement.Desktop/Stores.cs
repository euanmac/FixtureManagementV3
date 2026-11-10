using System.Text.Json;
using FixtureManagement.App.Auth;
using FixtureManagement.App.Settings;

namespace FixtureManagement.Desktop;

/// <summary>Settings in the OS's per-app preferences (NSUserDefaults on Mac, LocalSettings/registry on Windows).</summary>
public sealed class PreferencesAppSettingsStore : IAppSettingsStore
{
    private const string ServerAddressKey = "serverAddress";

    public string? ServerAddress
    {
        get => Preferences.Default.Get<string?>(ServerAddressKey, null);
        set
        {
            if (value is null)
            {
                Preferences.Default.Remove(ServerAddressKey);
            }
            else
            {
                Preferences.Default.Set(ServerAddressKey, value);
            }
        }
    }
}

/// <summary>
/// The signed-in session (email + refresh token) in SecureStorage: the Keychain on Mac, DPAPI-encrypted on Windows.
/// If the store can't be used (e.g. a Mac build without the Keychain entitlement), SessionPersistence logs it and the
/// user simply has to sign in each time.
/// </summary>
public sealed class SecureStorageSessionStore : ISessionStore
{
    private const string Key = "session";

    public async Task<SavedSession?> LoadAsync()
    {
        var json = await SecureStorage.Default.GetAsync(Key);
        if (json is null)
        {
            return null;
        }
        try
        {
            return JsonSerializer.Deserialize<SavedSession>(json);
        }
        catch (JsonException)
        {
            SecureStorage.Default.Remove(Key);   // unreadable (e.g. from an older version): forget it
            return null;
        }
    }

    public async Task SaveAsync(SavedSession? session)
    {
        if (session is null)
        {
            SecureStorage.Default.Remove(Key);
            return;
        }
        await SecureStorage.Default.SetAsync(Key, JsonSerializer.Serialize(session));
    }
}
