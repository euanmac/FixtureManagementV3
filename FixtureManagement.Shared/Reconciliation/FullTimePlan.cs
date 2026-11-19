using FixtureManagement.Shared.Dtos;
using FixtureManagement.Shared.Models;

namespace FixtureManagement.Shared.Reconciliation;

/// <summary>Works out which FullTime team pages to fetch for the club. Pure.</summary>
public static class FullTimePlan
{
    public static FullTimePlanDto Build(IEnumerable<TeamDto> teams, string clubName) => new()
    {
        ClubName = clubName,
        // A team is on FullTime when it has both ids; the rest genuinely aren't and are ignored.
        Teams = teams
            .Where(t => !string.IsNullOrWhiteSpace(t.FullTimeLeagueId) && !string.IsNullOrWhiteSpace(t.FullTimeTeamId))
            .OrderBy(t => t.League.DisplayName())
            .ThenBy(t => t.DisplayName)
            .Select(t =>
            {
                var divisionSeason = t.FullTimeLeagueId!.Trim();
                var teamId = t.FullTimeTeamId!.Trim();
                return new FullTimePlanTeamDto
                {
                    TeamId = t.Id,
                    DisplayName = t.DisplayName,
                    League = t.League,
                    LeagueName = t.League.DisplayName(),
                    DivisionSeasonId = divisionSeason,
                    FullTimeTeamId = teamId,
                    PageUrl = TeamRules.FixtureURL(divisionSeason, teamId),
                };
            })
            .ToList(),
    };
}
