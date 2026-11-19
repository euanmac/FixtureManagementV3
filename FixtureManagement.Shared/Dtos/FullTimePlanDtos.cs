using FixtureManagement.Shared.Models;

namespace FixtureManagement.Shared.Dtos;

/// <summary>
/// What to fetch from FullTime for the club: returned by GET /api/fulltime/plan (used by the browser extension).
/// One FullTime team page per team. Only teams with both FullTime ids are included; the rest aren't on FullTime.
/// </summary>
public record FullTimePlanDto
{
    /// <summary>The club's name as FullTime shows it, e.g. "Thame United".</summary>
    public string ClubName { get; init; } = "";
    public IReadOnlyList<FullTimePlanTeamDto> Teams { get; init; } = [];
}

public record FullTimePlanTeamDto
{
    public Guid TeamId { get; init; }
    public string DisplayName { get; init; } = "";
    public League League { get; init; }
    public string LeagueName { get; init; } = "";
    /// <summary>The team's division for this season on FullTime (stored as Team.FullTimeLeagueId).</summary>
    public string DivisionSeasonId { get; init; } = "";
    /// <summary>The team's id on FullTime.</summary>
    public string FullTimeTeamId { get; init; } = "";
    /// <summary>The team's FullTime page, to fetch and send back as it is.</summary>
    public string PageUrl { get; init; } = "";
}
