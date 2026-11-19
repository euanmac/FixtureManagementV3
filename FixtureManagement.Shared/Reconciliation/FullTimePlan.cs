using FixtureManagement.Shared.Dtos;
using FixtureManagement.Shared.Models;

namespace FixtureManagement.Shared.Reconciliation;

/// <summary>Works out what to fetch from FullTime for the club's teams. Pure.</summary>
public static class FullTimePlan
{
    public static FullTimePlanDto Build(IEnumerable<TeamDto> teams, string clubName)
    {
        // A team is on FullTime when it has both ids; the rest genuinely aren't and are ignored.
        var onFullTime = teams
            .Where(t => !string.IsNullOrWhiteSpace(t.FullTimeLeagueId) && !string.IsNullOrWhiteSpace(t.FullTimeTeamId))
            .OrderBy(t => t.DisplayName)
            .ToList();

        static FullTimePlanTeamDto ToPlanTeam(TeamDto t) => new()
        {
            TeamId = t.Id,
            DisplayName = t.DisplayName,
            DivisionSeasonId = t.FullTimeLeagueId!.Trim(),
            FullTimeTeamId = t.FullTimeTeamId!.Trim(),
        };

        var leagues = onFullTime
            .Where(t => FullTimeLeagues.LeagueId(t.League) is not null)
            .GroupBy(t => t.League)
            .OrderBy(g => g.Key.DisplayName())
            .Select(g => new FullTimePlanLeagueDto
            {
                League = g.Key,
                LeagueName = g.Key.DisplayName(),
                FullTimeLeagueId = FullTimeLeagues.LeagueId(g.Key)!,
                Teams = g.Select(ToPlanTeam).ToList(),
            })
            .ToList();

        return new FullTimePlanDto
        {
            ClubName = clubName,
            Leagues = leagues,
            TeamsWithoutLeagueId = onFullTime.Where(t => FullTimeLeagues.LeagueId(t.League) is null).Select(ToPlanTeam).ToList(),
        };
    }
}
