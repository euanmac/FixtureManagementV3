using System.Collections.Concurrent;
using System.Net;
using System.Runtime.CompilerServices;
using FixtureManagement.Shared.Dtos;
using FixtureManagement.Shared.Models;
using FixtureManagement.Shared.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FixtureManagement.FullTime;

/// <summary>How hard the app may hit FullTime. The defaults keep a Gameweek load (~36 teams) to a gentle trickle.</summary>
public sealed class FullTimeOptions
{
    /// <summary>At most this many FullTime requests at once (across the whole app/site).</summary>
    public int MaxConcurrentRequests { get; set; } = 2;

    /// <summary>Pause after each request before that slot is used again.</summary>
    public TimeSpan DelayBetweenRequests { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>After FullTime refuses a request (403/429), don't ask again for this long.</summary>
    public TimeSpan PauseAfterRefusal { get; set; } = TimeSpan.FromMinutes(10);
}

/// <summary>FullTime turned the request away (403/429), or we're still pausing after it did.</summary>
public sealed class FullTimeRefusedException(string message) : HttpRequestException(message);

/// <summary>
/// IFullTimeService: downloads FullTime pages and caches them in memory. Register once per app (singleton)
/// with <see cref="FullTimeServiceCollectionExtensions.AddFullTime"/>. On the website the cache is shared by
/// everyone; in the desktop app each install has its own.
/// Requests are throttled (<see cref="FullTimeOptions"/>): FullTime answers a burst of requests with 403s.
/// </summary>
public sealed class FullTimeService(IHttpClientFactory httpClientFactory, ILogger<FullTimeService>? logger = null, FullTimeOptions? options = null)
    : IFullTimeService
{
    internal const string HttpClientName = "FullTime";

    private readonly FullTimeOptions options = options ?? new FullTimeOptions();
    private readonly SemaphoreSlim requestSlots = new(Math.Max(1, (options ?? new FullTimeOptions()).MaxConcurrentRequests));
    private readonly ConcurrentDictionary<(Guid TeamId, DateOnly Start, DateOnly End), List<FullTimeFixture>> cache = new();
    private DateTime? cacheFilledAt;
    private DateTime refusedUntil = DateTime.MinValue;

    public string LastHtml { get; private set; } = "";

    public TimeSpan CacheAge => cacheFilledAt is null ? TimeSpan.Zero : DateTime.Now - cacheFilledAt.Value;

    public void ClearCache()
    {
        cache.Clear();
        cacheFilledAt = null;
    }

    /// <summary>
    /// One team's FullTime fixtures. Throws (HttpRequestException) if FullTime doesn't answer with the page –
    /// an error page must never be read as "this team has no fixtures", or its local fixtures would look unmatched.
    /// </summary>
    public async Task<IReadOnlyList<FullTimeFixture>> GetFixturesAsync(TeamDto team, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(team.FullTimeTeamId) || string.IsNullOrEmpty(team.FullTimeLeagueId))
        {
            return [];
        }

        await requestSlots.WaitAsync(cancellationToken);
        try
        {
            ThrowIfPausedAfterRefusal();

            // FullTime turns away requests that don't look like a browser, so send browser-like headers.
            using var request = new HttpRequestMessage(HttpMethod.Get, FullTimeParser.TeamPageUrl(team));
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Macintosh; Intel Mac OS X 14_0) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.0 Safari/605.1.15");
            request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            request.Headers.Add("Accept-Language", "en-GB,en;q=0.9");
            request.Headers.Referrer = new Uri("https://www.google.com/");

            var client = httpClientFactory.CreateClient(HttpClientName);
            using var response = await client.SendAsync(request, cancellationToken);
            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            LastHtml = html;

            if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
            {
                refusedUntil = DateTime.UtcNow + options.PauseAfterRefusal;
                logger?.LogWarning("FullTime refused the request for {Team} ({Status}); not asking again for {Pause}",
                    team.Name, (int)response.StatusCode, options.PauseAfterRefusal);
                throw new FullTimeRefusedException($"FullTime refused the request ({(int)response.StatusCode}).");
            }
            if (!response.IsSuccessStatusCode)
            {
                logger?.LogWarning("FullTime page for {Team} returned {Status}", team.Name, (int)response.StatusCode);
                throw new HttpRequestException($"FullTime returned {(int)response.StatusCode}.", null, response.StatusCode);
            }

            var fixtures = FullTimeParser.ParseFixtures(html, team);
            logger?.LogDebug("FullTime page for {Team}: {Count} fixtures", team.Name, fixtures.Count);
            return fixtures;
        }
        finally
        {
            ReleaseSlotAfterDelay();
        }
    }

    private void ThrowIfPausedAfterRefusal()
    {
        if (DateTime.UtcNow < refusedUntil)
        {
            throw new FullTimeRefusedException($"FullTime refused an earlier request; trying again after {refusedUntil.ToLocalTime():HH:mm}.");
        }
    }

    /// <summary>Keeps the slot for a moment after each request, so requests are spaced out, without delaying the caller.</summary>
    private void ReleaseSlotAfterDelay()
    {
        if (options.DelayBetweenRequests <= TimeSpan.Zero)
        {
            requestSlots.Release();
            return;
        }
        _ = Task.Delay(options.DelayBetweenRequests).ContinueWith(_ => requestSlots.Release(), TaskScheduler.Default);
    }

    public async IAsyncEnumerable<List<TeamReconiliationRow>> Reconcile(IReadOnlyList<TeamDto> teams, IReadOnlyList<FixtureDto> fixtures,
        DateOnly weekStart, DateOnly weekEnd, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Give a streaming page a moment to send its "pending" rows first.
        await Task.Delay(100, cancellationToken);

        var pending = teams.Select(t => ReconcileTeamAsync(t, weekStart, weekEnd, fixtures, cancellationToken)).ToList();
        while (pending.Count > 0)
        {
            var finished = await Task.WhenAny(pending);
            pending.Remove(finished);
            yield return await finished;
        }
    }

    private async Task<List<TeamReconiliationRow>> ReconcileTeamAsync(TeamDto team, DateOnly start, DateOnly end,
        IReadOnlyList<FixtureDto> fixtures, CancellationToken cancellationToken)
    {
        List<FullTimeFixture> fullTime = [];
        if (team.IsOnFullTime)
        {
            var key = (team.Id, start, end);
            if (cache.TryGetValue(key, out var cached))
            {
                fullTime = cached;
            }
            else
            {
                try
                {
                    fullTime = (await GetFixturesAsync(team, cancellationToken))
                        .Where(f => f.Date >= start && f.Date <= end)
                        .ToList();
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    logger?.LogWarning(ex, "Couldn't read FullTime for {Team}", team.Name);
                    // One team's FullTime page failing shouldn't break the whole week; show it as unknown.
                    return [new TeamReconiliationRow { Team = team, MatchDate = start, RecStatus = FixtureRecMatchType.unknownTeam }];
                }
                cache[key] = fullTime;
                cacheFilledAt ??= DateTime.Now;
            }
        }
        return FullTimeReconciler.ReconcileTeam(team, start, end, fixtures, fullTime);
    }
}

public static class FullTimeServiceCollectionExtensions
{
    /// <summary>
    /// Registers IFullTimeService (singleton, so its cache and request throttle are shared by the whole app/site).
    /// <paramref name="configure"/> can change the throttle (see <see cref="FullTimeOptions"/>).
    /// </summary>
    public static IServiceCollection AddFullTime(this IServiceCollection services, Action<FullTimeOptions>? configure = null)
    {
        var options = new FullTimeOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);

        // .NET's own HTTP stack everywhere. On Mac/iOS the default would be Apple's NSURLSession, which sends requests
        // differently from the website; this keeps the desktop app's FullTime requests the same as the website's.
        services.AddHttpClient(FullTimeService.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(30))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler());
        services.AddSingleton<IFullTimeService, FullTimeService>();
        return services;
    }
}
