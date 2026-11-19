using FixtureManagement.Shared.Models;

namespace FixtureManagement.Shared.Dtos;

/// <summary>
/// What to fetch from FullTime for the club: returned by GET /api/fulltime/plan (used by the browser extension).
/// Only teams with FullTime ids are included; teams without them aren't on FullTime and are ignored.
/// </summary>
public record FullTimePlanDto
{
    /// <summary>The club's name as FullTime shows it, e.g. "Thame United" – used to pick the club's rows.</summary>
    public string ClubName { get; init; } = "";
    /// <summary>The leagues to fetch, each with our teams in it.</summary>
    public IReadOnlyList<FullTimePlanLeagueDto> Leagues { get; init; } = [];
    /// <summary>Teams on FullTime whose league has no FullTime league id yet, so they can't be fetched.</summary>
    public IReadOnlyList<FullTimePlanTeamDto> TeamsWithoutLeagueId { get; init; } = [];
}

public record FullTimePlanLeagueDto
{
    public League League { get; init; }
    public string LeagueName { get; init; } = "";
    /// <summary>The league's id on FullTime (the <c>league=</c> number).</summary>
    public string FullTimeLeagueId { get; init; } = "";
    public IReadOnlyList<FullTimePlanTeamDto> Teams { get; init; } = [];
}

public record FullTimePlanTeamDto
{
    public Guid TeamId { get; init; }
    public string DisplayName { get; init; } = "";
    /// <summary>The team's division for this season on FullTime (stored as Team.FullTimeLeagueId).</summary>
    public string DivisionSeasonId { get; init; } = "";
    /// <summary>The team's id on FullTime.</summary>
    public string FullTimeTeamId { get; init; } = "";
}
