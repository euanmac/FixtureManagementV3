using System.Security.Claims;
using FixtureManagement.ApiClient;
using Microsoft.AspNetCore.Components.Authorization;

namespace FixtureManagement.App.Auth;

/// <summary>
/// Tells the shared pages (AuthorizeView, [Authorize], "isAuthenticated" checks) who is signed in, based on
/// <see cref="ApiSession"/>. Signed in = the app holds a refresh token. There are no roles: the bearer token
/// is opaque to the client, and none of the shared pages check roles.
/// </summary>
public sealed class ApiAuthenticationStateProvider : AuthenticationStateProvider, IDisposable
{
    public const string AuthenticationType = "FixtureManagementApi";

    private static readonly AuthenticationState SignedOut = new(new ClaimsPrincipal(new ClaimsIdentity()));

    private readonly ApiSession session;
    private string? lastSignedInEmail;

    public ApiAuthenticationStateProvider(ApiSession session)
    {
        this.session = session;
        lastSignedInEmail = SignedInEmail();
        session.Changed += OnSessionChanged;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(CurrentState());

    private AuthenticationState CurrentState()
    {
        var email = SignedInEmail();
        if (email is null)
        {
            return SignedOut;
        }
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, email), new Claim(ClaimTypes.Email, email)],
            AuthenticationType);
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    private string? SignedInEmail() => session.IsSignedIn ? session.Email : null;

    // ApiSession.Changed also fires on every token refresh; only tell the UI when the user actually changes.
    // It can fire on a background thread – CascadingAuthenticationState marshals to the renderer itself.
    private void OnSessionChanged()
    {
        var email = SignedInEmail();
        var previous = Interlocked.Exchange(ref lastSignedInEmail, email);
        if (!string.Equals(previous, email, StringComparison.Ordinal))
        {
            NotifyAuthenticationStateChanged(Task.FromResult(CurrentState()));
        }
    }

    public void Dispose() => session.Changed -= OnSessionChanged;
}
