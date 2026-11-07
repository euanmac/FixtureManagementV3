using System.Net;

namespace FixtureManagement.ApiClient;

/// <summary>
/// The server said 401/403: not signed in (or the session couldn't be refreshed), or not allowed.
/// The app should send the user to its login screen.
/// </summary>
public sealed class ApiAuthenticationException(HttpStatusCode statusCode)
    : Exception(statusCode == HttpStatusCode.Forbidden ? "You don't have permission to do that." : "You need to sign in.")
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}

/// <summary>
/// The server rejected the data (400) on a method whose interface can't return field errors
/// (e.g. ITeamService.CreateTeamAsync returns a plain TeamDto). Pages validate before calling, so this is rare.
/// </summary>
public sealed class ApiValidationException(IReadOnlyDictionary<string, string[]> errors)
    : Exception("The server rejected the data: " + string.Join(" ", errors.SelectMany(e => e.Value)))
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}
