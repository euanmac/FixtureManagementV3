using System.Net;
using System.Net.Http.Json;
using FixtureManagement.App.Auth;
using FixtureManagement.App.Settings;

namespace FixtureManagement.Tests.App;

public sealed class InMemoryAppSettingsStore : IAppSettingsStore
{
    public string? ServerAddress { get; set; }
    public string? FullTimePageLoader { get; set; }
}

public sealed class InMemorySessionStore : ISessionStore
{
    public SavedSession? Saved { get; set; }
    public int SaveCount { get; private set; }

    public Task<SavedSession?> LoadAsync() => Task.FromResult(Saved);

    public Task SaveAsync(SavedSession? session)
    {
        Saved = session;
        SaveCount++;
        return Task.CompletedTask;
    }
}

/// <summary>Stands in for the server: answers each request with whatever the test's function returns.</summary>
public sealed class StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(respond(request));
    }

    /// <summary>A /api/auth/login or /api/auth/refresh success, in Identity's bearer token format.</summary>
    public static HttpResponseMessage Tokens(string accessToken = "access-1", string refreshToken = "refresh-1")
        => new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { tokenType = "Bearer", accessToken, expiresIn = 3600, refreshToken }),
        };

    public static HttpResponseMessage Status(HttpStatusCode status) => new(status);

    public static Func<HttpRequestMessage, HttpResponseMessage> Unreachable
        => _ => throw new HttpRequestException("No such host is known.");
}
