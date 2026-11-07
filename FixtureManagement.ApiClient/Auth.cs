using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace FixtureManagement.ApiClient;

/// <summary>
/// The signed-in user's tokens from /api/auth/login. One per app (register as a singleton).
/// The desktop app can persist <see cref="RefreshToken"/> (e.g. in secure storage) and call
/// <see cref="ApiAuthClient.RefreshAsync"/> at start-up to stay signed in.
/// </summary>
public sealed class ApiSession
{
    private readonly object gate = new();

    public string? Email { get; private set; }
    public string? AccessToken { get; private set; }
    public string? RefreshToken { get; private set; }
    public DateTimeOffset AccessTokenExpires { get; private set; }

    public bool IsSignedIn => RefreshToken is not null;

    /// <summary>Raised when the user signs in, out, or the tokens are refreshed (e.g. to update the UI's auth state).</summary>
    public event Action? Changed;

    internal SemaphoreSlim RefreshLock { get; } = new(1, 1);

    /// <summary>For restoring a saved session (e.g. a stored refresh token) before calling RefreshAsync.</summary>
    public void Restore(string email, string refreshToken)
    {
        lock (gate)
        {
            Email = email;
            RefreshToken = refreshToken;
            AccessToken = null;
            AccessTokenExpires = DateTimeOffset.MinValue;
        }
        Changed?.Invoke();
    }

    internal void Set(string email, TokenResponse tokens)
    {
        lock (gate)
        {
            Email = email;
            AccessToken = tokens.AccessToken;
            RefreshToken = tokens.RefreshToken;
            AccessTokenExpires = DateTimeOffset.UtcNow.AddSeconds(tokens.ExpiresIn);
        }
        Changed?.Invoke();
    }

    public void Clear()
    {
        lock (gate)
        {
            Email = null;
            AccessToken = null;
            RefreshToken = null;
            AccessTokenExpires = DateTimeOffset.MinValue;
        }
        Changed?.Invoke();
    }
}

/// <summary>/api/auth/login and /api/auth/refresh response (ASP.NET Identity's bearer token format).</summary>
internal sealed record TokenResponse(string TokenType, string AccessToken, long ExpiresIn, string RefreshToken);

/// <summary>
/// Signs in and refreshes tokens against the server's Identity endpoints (/api/auth).
/// Uses its own HttpClient without the BearerTokenHandler.
/// </summary>
public sealed class ApiAuthClient(HttpClient http, ApiSession session)
{
    /// <summary>Returns false for a wrong email/password (or an unconfirmed account).</summary>
    public async Task<bool> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync("api/auth/login", new { email, password }, ApiResponses.Json, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized) return false;
        response.EnsureSuccessStatusCode();
        var tokens = await response.Content.ReadFromJsonAsync<TokenResponse>(ApiResponses.Json, cancellationToken);
        if (tokens is null) return false;
        session.Set(email, tokens);
        return true;
    }

    /// <summary>
    /// Swaps the refresh token for new tokens. Returns false (and signs out) if there's no session or it has expired.
    /// Safe to call from several requests at once: only one refresh runs.
    /// </summary>
    public async Task<bool> RefreshAsync(CancellationToken cancellationToken = default)
    {
        var tokenBefore = session.AccessToken;
        await session.RefreshLock.WaitAsync(cancellationToken);
        try
        {
            // Another request may have refreshed while we waited.
            if (session.AccessToken is not null && session.AccessToken != tokenBefore) return true;
            if (session.RefreshToken is null || session.Email is null) return false;

            using var response = await http.PostAsJsonAsync("api/auth/refresh", new { refreshToken = session.RefreshToken }, ApiResponses.Json, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                session.Clear();
                return false;
            }
            var tokens = await response.Content.ReadFromJsonAsync<TokenResponse>(ApiResponses.Json, cancellationToken);
            if (tokens is null)
            {
                session.Clear();
                return false;
            }
            session.Set(session.Email, tokens);
            return true;
        }
        finally
        {
            session.RefreshLock.Release();
        }
    }

    public void SignOut() => session.Clear();
}

/// <summary>
/// Adds "Authorization: Bearer …" to every API request. Refreshes the access token shortly before it expires,
/// and once more if the server still answers 401, then retries the request.
/// </summary>
public sealed class BearerTokenHandler(ApiSession session, ApiAuthClient auth) : DelegatingHandler
{
    private static readonly TimeSpan RefreshMargin = TimeSpan.FromSeconds(30);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (session.IsSignedIn && (session.AccessToken is null || session.AccessTokenExpires - DateTimeOffset.UtcNow < RefreshMargin))
        {
            await auth.RefreshAsync(cancellationToken);
        }

        // Buffer the body so the request can be resent after a refresh.
        if (request.Content is not null) await request.Content.LoadIntoBufferAsync();

        var response = await SendWithTokenAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized || !session.IsSignedIn) return response;

        if (!await auth.RefreshAsync(cancellationToken)) return response;
        response.Dispose();
        return await SendWithTokenAsync(await CloneAsync(request), cancellationToken);
    }

    private Task<HttpResponseMessage> SendWithTokenAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Authorization = session.AccessToken is null ? null : new AuthenticationHeaderValue("Bearer", session.AccessToken);
        return base.SendAsync(request, cancellationToken);
    }

    private static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage original)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri) { Version = original.Version };
        foreach (var header in original.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
        if (original.Content is not null)
        {
            var body = await original.Content.ReadAsByteArrayAsync();
            clone.Content = new ByteArrayContent(body);
            foreach (var header in original.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }
        return clone;
    }
}
