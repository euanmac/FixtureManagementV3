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

    /// <summary>How pages are fetched: HtmlWeb (plain HTTP, the default) or Selenium (drives a real Chrome).</summary>
    public FullTimePageLoaderKind PageLoader { get; set; } = FullTimePageLoaderKind.HtmlWeb;

    /// <summary>
    /// Selenium only: run Chrome without a window. Off by default: FullTime answered headless Chrome with 403 but a
    /// visible Chrome with the page (2026-11-11).
    /// </summary>
    public bool SeleniumHeadless { get; set; }
}

/// <summary>Which <see cref="IFullTimePageLoader"/> <see cref="FullTimeServiceCollectionExtensions.AddFullTime"/> registers.</summary>
public enum FullTimePageLoaderKind
{
    HtmlWeb,
    Selenium,
}

/// <summary>FullTime turned the request away (403/429), or we're still pausing after it did.</summary>
public sealed class FullTimeRefusedException(string message) : HttpRequestException(message);

/// <summary>
/// IFullTimeService: downloads FullTime pages and caches them in memory. Register once per app (singleton)
/// with <see cref="FullTimeServiceCollectionExtensions.AddFullTime"/>. On the website the cache is shared by
/// everyone; in the desktop app each install has its own.
/// Requests are throttled (<see cref="FullTimeOptions"/>): FullTime answers a burst of requests with 403s.
/// </summary>
public sealed class FullTimeService(IFullTimePageLoader pageLoader, ILogger<FullTimeService>? logger = null, FullTimeOptions? options = null)
    : IFullTimeService
{
    private readonly FullTimeOptions options = options ?? new FullTimeOptions();
    private readonly SemaphoreSlim requestSlots = new(Math.Max(1, (options ?? new FullTimeOptions()).MaxConcurrentRequests));
    private readonly ConcurrentDictionary<(Guid TeamId, DateOnly Start, DateOnly End), List<FullTimeFixture>> cache = new();
    private DateTime? cacheFilledAt;
    private DateTime refusedUntil = DateTime.MinValue;

    public string LastHtml { get; private set; } = "";

    public string UserAgent => pageLoader.UserAgent;

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

            // The loader downloads and parses the page in one go (HtmlWebPageLoader or SeleniumPageLoader).
            var page = await pageLoader.LoadAsync(FullTimeParser.TeamPageUrl(team), cancellationToken);
            LastHtml = page.Html;

            if (page.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests)
            {
                refusedUntil = DateTime.UtcNow + options.PauseAfterRefusal;
                logger?.LogWarning("FullTime refused the request for {Team} ({Status}); not asking again for {Pause}",
                    team.Name, (int)page.StatusCode, options.PauseAfterRefusal);
                throw new FullTimeRefusedException($"FullTime refused the request ({(int)page.StatusCode}).");
            }
            if (!page.IsSuccess)
            {
                logger?.LogWarning("FullTime page for {Team} returned {Status}", team.Name, (int)page.StatusCode);
                throw new HttpRequestException($"FullTime returned {(int)page.StatusCode}.", null, page.StatusCode);
            }

            var fixtures = FullTimeParser.ParseFixtures(page.Document, team);
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
    /// <paramref name="configure"/> can change the throttle and the page loader (see <see cref="FullTimeOptions"/>).
    /// </summary>
    public static IServiceCollection AddFullTime(this IServiceCollection services, Action<FullTimeOptions>? configure = null)
    {
        var options = new FullTimeOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);

        if (options.PageLoader == FullTimePageLoaderKind.Selenium)
        {
            services.AddSingleton<IFullTimePageLoader, SeleniumPageLoader>();
        }
        else
        {
            services.AddSingleton<IFullTimePageLoader, HtmlWebPageLoader>();
        }
        services.AddSingleton<IFullTimeService, FullTimeService>();
        return services;
    }

    /// <summary>
    /// As <see cref="AddFullTime(IServiceCollection, Action{FullTimeOptions}?)"/>, but the page loader is chosen on every
    /// request by <paramref name="pageLoaderChoice"/> (e.g. from an app setting), so it can change without a restart.
    /// <see cref="FullTimeOptions.PageLoader"/> is ignored.
    /// </summary>
    public static IServiceCollection AddFullTime(this IServiceCollection services,
        Func<IServiceProvider, Func<FullTimePageLoaderKind>> pageLoaderChoice, Action<FullTimeOptions>? configure = null)
    {
        var options = new FullTimeOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);

        services.AddSingleton<HtmlWebPageLoader>();
        services.AddSingleton<SeleniumPageLoader>();
        services.AddSingleton<IFullTimePageLoader>(provider => new SelectablePageLoader(
            pageLoaderChoice(provider),
            provider.GetRequiredService<HtmlWebPageLoader>(),
            provider.GetRequiredService<SeleniumPageLoader>()));
        services.AddSingleton<IFullTimeService, FullTimeService>();
        return services;
    }
}
