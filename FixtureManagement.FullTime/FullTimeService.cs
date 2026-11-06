using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using FixtureManagement.Shared.Dtos;
using FixtureManagement.Shared.Models;
using FixtureManagement.Shared.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FixtureManagement.FullTime;

/// <summary>
/// IFullTimeService: downloads FullTime pages and caches them in memory. Register once per app (singleton)
/// with <see cref="FullTimeServiceCollectionExtensions.AddFullTime"/>. On the website the cache is shared by
/// everyone; in the desktop app each install has its own.
/// </summary>
public sealed class FullTimeService(IHttpClientFactory httpClientFactory) : IFullTimeService
{
    internal const string HttpClientName = "FullTime";

    private readonly ConcurrentDictionary<(Guid TeamId, DateOnly Start, DateOnly End), List<FullTimeFixture>> cache = new();
    private DateTime? cacheFilledAt;

    public string LastHtml { get; private set; } = "";

    public TimeSpan CacheAge => cacheFilledAt is null ? TimeSpan.Zero : DateTime.Now - cacheFilledAt.Value;

    public void ClearCache()
    {
        cache.Clear();
        cacheFilledAt = null;
    }

    public async Task<IReadOnlyList<FullTimeFixture>> GetFixturesAsync(TeamDto team, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(team.FullTimeTeamId) || string.IsNullOrEmpty(team.FullTimeLeagueId))
        {
            return [];
        }

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
        return FullTimeParser.ParseFixtures(html, team);
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
                catch (Exception) when (!cancellationToken.IsCancellationRequested)
                {
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
    /// <summary>Registers IFullTimeService (singleton, so its cache lasts for the life of the app).</summary>
    public static IServiceCollection AddFullTime(this IServiceCollection services)
    {
        services.AddHttpClient(FullTimeService.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(30));
        services.AddSingleton<IFullTimeService, FullTimeService>();
        return services;
    }
}
