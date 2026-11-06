using FixtureManagement.Shared.Dtos;
using FixtureManagement.Shared.Models;

namespace FixtureManagement.FullTime;

/// <summary>
/// Compares one team's local fixtures with its FullTime fixtures for a week. Pure, so it can be tested
/// without the network. Moved unchanged from the website's FullTimeReconciliationService.
/// </summary>
public static class FullTimeReconciler
{
    /// <param name="fullTimeFixtures">The team's FullTime fixtures already limited to the week (empty if not on FullTime).</param>
    public static List<TeamReconiliationRow> ReconcileTeam(TeamDto team, DateOnly weekStart, DateOnly weekEnd,
        IReadOnlyList<FixtureDto> fixtures, IReadOnlyList<FullTimeFixture> fullTimeFixtures)
    {
        var unmatched = fullTimeFixtures.ToList();
        var local = fixtures
            .Where(f => f.Date >= weekStart && f.Date <= weekEnd && f.TeamId == team.Id)
            .ToList();

        // Nothing either side: one "no fixture" row on the team's usual match day.
        if (unmatched.Count == 0 && local.Count == 0)
        {
            int offset = team.MatchDay == DayOfWeek.Sunday ? 6 : (int)team.MatchDay - 1;
            return [new TeamReconiliationRow { Team = team, MatchDate = weekStart.AddDays(offset), RecStatus = FixtureRecMatchType.noFixture }];
        }

        var rows = local.Select(f =>
        {
            var row = new TeamReconiliationRow
            {
                Team = team,
                Id = f.Id,
                MatchDate = f.Date,
                Opponent = f.Opponent,
                Venue = f.IsHome ? "H" : "A",
                FixtureType = f.FixtureType,
                CanAllocate = f.CanAllocate,
                IsAllocated = f.IsAllocated,
                AllocationId = f.IsAllocated ? f.Allocation!.Id : Guid.Empty,
                Pitch = f.IsAllocated ? f.Allocation!.PitchName : "",
                Start = f.IsAllocated ? f.Allocation!.Start : TimeOnly.MinValue,
                IsConfirmed = f.IsAllocated && f.Allocation!.IsConfirmed,
            };

            // A FullTime fixture matches if date, opponent, venue and type are all the same.
            var matches = unmatched.Where(ft =>
                ft.Date == f.Date && ft.Opponent == f.Opponent && ft.IsHome == f.IsHome && ft.FixtureType == f.FixtureType).ToList();

            if (matches.Count > 0)
            {
                row.RecStatus = FixtureRecMatchType.fullTimematched;
                unmatched = unmatched.Except(matches).ToList();
            }
            else
            {
                row.RecStatus = team.IsOnFullTime ? FixtureRecMatchType.localFixtureUnmatched : FixtureRecMatchType.localFixtureOnly;
            }
            return row;
        }).ToList();

        // Whatever is left on FullTime has no local fixture yet.
        rows.AddRange(unmatched.Select(ft => new TeamReconiliationRow
        {
            Id = ft.Id,
            Team = team,
            MatchDate = ft.Date,
            Opponent = ft.Opponent,
            Venue = ft.IsHome ? "H" : "A",
            RecStatus = FixtureRecMatchType.fullTimeUnmatched,
            FixtureType = ft.FixtureType,
        }));
        return rows;
    }
}
