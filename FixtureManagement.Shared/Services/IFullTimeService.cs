using FixtureManagement.Shared.Dtos;
using FixtureManagement.Shared.Models;

namespace FixtureManagement.Shared.Services;

/// <summary>
/// Reads fixtures from the FA's FullTime site and compares them with the local fixtures.
/// Implemented in FixtureManagement.FullTime and registered by each host (website and desktop app),
/// so the scraping runs wherever the app runs; the local fixtures come in as DTOs (via IFixtureService).
/// </summary>
public interface IFullTimeService
{
    /// <summary>
    /// Fixtures (and postponed games) on the team's FullTime page. Empty if the team has no FullTime ids.
    /// Not cached. Throws if the page can't be downloaded or read.
    /// </summary>
    Task<IReadOnlyList<FullTimeFixture>> GetFixturesAsync(TeamDto team, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reconciles every team for the week, yielding each team's rows as soon as that team is done
    /// (so a page can stream them). FullTime pages are cached per team and week until <see cref="ClearCache"/>.
    /// A team whose FullTime page fails gets a single <see cref="FixtureRecMatchType.unknownTeam"/> row.
    /// </summary>
    IAsyncEnumerable<List<TeamReconiliationRow>> Reconcile(IReadOnlyList<TeamDto> teams, IReadOnlyList<FixtureDto> fixtures,
        DateOnly weekStart, DateOnly weekEnd, CancellationToken cancellationToken = default);

    void ClearCache();

    /// <summary>How long ago the cache was first filled (zero if empty).</summary>
    TimeSpan CacheAge { get; }

    /// <summary>The raw HTML of the last FullTime page downloaded, for troubleshooting.</summary>
    string LastHtml { get; }
}
