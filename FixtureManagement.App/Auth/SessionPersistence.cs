using FixtureManagement.ApiClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FixtureManagement.App.Auth;

/// <summary>What's kept between runs to stay signed in. The refresh token is a credential: store it securely.</summary>
public sealed record SavedSession(string Email, string RefreshToken);

/// <summary>
/// Where the signed-in session is kept between runs. The MAUI host implements it with SecureStorage
/// (Keychain on Mac, DPAPI on Windows); tests use an in-memory one.
/// </summary>
public interface ISessionStore
{
    Task<SavedSession?> LoadAsync();

    /// <summary>Saves the session, or deletes the saved one when <paramref name="session"/> is null (signed out).</summary>
    Task SaveAsync(SavedSession? session);
}

/// <summary>
/// Keeps the user signed in between runs: saves the session whenever it changes (sign in, token refresh –
/// refresh tokens are single-use, so every refresh must be saved – and sign out), and restores it at start-up.
/// One per app (singleton); <see cref="Routes"/> calls <see cref="RestoreAsync"/> before showing any page.
/// </summary>
public sealed class SessionPersistence : IDisposable
{
    private readonly ApiSession session;
    private readonly IServiceProvider services;
    private readonly ISessionStore store;
    private readonly ILogger<SessionPersistence> logger;
    private readonly SemaphoreSlim saveLock = new(1, 1);
    private readonly SemaphoreSlim restoreLock = new(1, 1);
    private SavedSession? lastSaved;
    private bool restored;

    public SessionPersistence(ApiSession session, IServiceProvider services, ISessionStore store, ILogger<SessionPersistence> logger)
    {
        this.session = session;
        this.services = services;
        this.store = store;
        this.logger = logger;
        session.Changed += OnSessionChanged;
    }

    /// <summary>
    /// Loads the saved session (first call only) and swaps its refresh token for fresh tokens.
    /// If the server says the token is no longer valid, the user is signed out. If the server can't be reached,
    /// the session is kept and the first API call will try again.
    /// </summary>
    public async Task RestoreAsync(CancellationToken cancellationToken = default)
    {
        await restoreLock.WaitAsync(cancellationToken);
        try
        {
            if (restored)
            {
                return;
            }
            restored = true;

            SavedSession? saved;
            try
            {
                saved = await store.LoadAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Couldn't read the saved session; starting signed out.");
                return;
            }
            if (saved is null || session.IsSignedIn)
            {
                return;
            }

            lastSaved = saved;   // already in the store; Restore's Changed event needn't write it again
            session.Restore(saved.Email, saved.RefreshToken);
            try
            {
                // Resolved here rather than injected: it's a typed HttpClient, and the server address may have changed.
                await services.GetRequiredService<ApiAuthClient>().RefreshAsync(cancellationToken);
            }
            catch (Exception ex) when ((ex is HttpRequestException or TaskCanceledException) && !cancellationToken.IsCancellationRequested)
            {
                logger.LogInformation(ex, "Couldn't reach the server to refresh the saved session; will retry on the first request.");
            }
        }
        finally
        {
            restoreLock.Release();
        }
    }

    private void OnSessionChanged() => _ = SaveCurrentAsync();

    /// <summary>Saves whatever the session is now (not what it was when the event fired), one save at a time.</summary>
    private async Task SaveCurrentAsync()
    {
        await saveLock.WaitAsync();
        try
        {
            var email = session.Email;
            var refreshToken = session.RefreshToken;
            SavedSession? current = null;
            if (email is not null && refreshToken is not null)
            {
                current = new SavedSession(email, refreshToken);
            }
            if (current == lastSaved)
            {
                return;
            }
            await store.SaveAsync(current);
            lastSaved = current;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Couldn't save the session; the user may need to sign in again next time.");
        }
        finally
        {
            saveLock.Release();
        }
    }

    /// <summary>For tests: waits until any pending save has finished.</summary>
    internal async Task FlushAsync()
    {
        await saveLock.WaitAsync();
        saveLock.Release();
    }

    public void Dispose() => session.Changed -= OnSessionChanged;
}
