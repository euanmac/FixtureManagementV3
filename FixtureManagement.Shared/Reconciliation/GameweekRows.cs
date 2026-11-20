using FixtureManagement.Shared.Dtos;
using FixtureManagement.Shared.Models;

namespace FixtureManagement.Shared.Reconciliation;

/// <summary>
/// Turns a week compared with the latest upload (ReconciliationWeekDto) into Gameweek's rows: one list per team, in
/// the teams' order, each row a fixture with its status, and a "no fixture" row for a team with nothing that week.
/// Pure, so it can be tested without a database.
/// </summary>
public static class GameweekRows
{
    /// <param name="teams">All teams, in display order.</param>
    /// <param name="weekFixtures">Fixture Manager's fixtures dated in the week.</param>
    public static Dictionary<TeamDto, List<TeamReconiliationRow>> Build(IReadOnlyList<TeamDto> teams,
        DateOnly weekStart, DateOnly weekEnd, IReadOnlyList<FixtureDto> weekFixtures, ReconciliationWeekDto week)
    {
        bool InWeek(DateOnly? date) => date is not null && date.Value >= weekStart && date.Value <= weekEnd;
        var sourceName = string.IsNullOrEmpty(week.Upload?.SourceName) ? "FullTime" : week.Upload.SourceName;

        var rows = new Dictionary<TeamDto, List<TeamReconiliationRow>>();
        foreach (var team in teams)
        {
            var list = new List<TeamReconiliationRow>();
            var compared = new HashSet<Guid>();

            foreach (var item in week.Items.Where(i => i.Team?.Id == team.Id))
            {
                if (item.App is not null) compared.Add(item.App.Id);
                var row = ToRow(team, item, InWeek, sourceName);
                if (row is null) continue;
                if (item.Kind != DifferenceKind.Same)
                {
                    row.ItemId = item.Id;           // something to decide (or to change, if accepted)
                    row.Difference = item.Kind;
                }
                list.Add(row);
            }

            // Fixtures the upload didn't compare: a team it didn't cover, or dates before it.
            bool teamCovered = week.Upload is not null && week.CoveredTeamIds.Contains(team.Id);
            foreach (var fixture in weekFixtures.Where(f => f.TeamId == team.Id && !compared.Contains(f.Id)))
            {
                var row = FromApp(team, fixture, FixtureRecMatchType.localFixtureOnly);
                if (team.IsOnFullTime && week.Upload is not null && !teamCovered)
                    row.Note = $"Not in the last {sourceName} upload";
                else if (team.IsOnFullTime && teamCovered && week.CoveredFrom is not null && fixture.Date < week.CoveredFrom.Value)
                    row.Note = $"Not compared – before the {sourceName} fetch of {week.CoveredFrom.Value:dd/MM}";
                list.Add(row);
            }

            if (list.Count == 0)
            {
                int offset = team.MatchDay == DayOfWeek.Sunday ? 6 : (int)team.MatchDay - 1;
                list.Add(new TeamReconiliationRow { Team = team, MatchDate = weekStart.AddDays(offset), RecStatus = FixtureRecMatchType.noFixture });
            }
            rows[team] = list.OrderBy(r => r.MatchDate).ToList();
        }
        return rows;
    }

    private static TeamReconiliationRow? ToRow(TeamDto team, ReconciliationItem item, Func<DateOnly?, bool> inWeek, string sourceName)
    {
        static string Describe(SourceFixture s) => $"{(s.IsHome ? "H" : "A")} v {s.Opponent} ({s.FixtureType.DisplayName()})";

        switch (item.Kind)
        {
            case DifferenceKind.Same:
                return FromApp(team, item.App!, FixtureRecMatchType.fullTimematched);

            case DifferenceKind.OnlyInSource:
                // Not shown if it has no date (can't be downloaded). One someone chose not to add is accepted, and can
                // still be downloaded.
                if (!inWeek(item.Source!.Date)) return null;
                return item.Kept
                    ? FromSource(team, item, FixtureRecMatchType.fullTimeAccepted, $"Not added – {KeptText(item)}")
                    : FromSource(team, item, FixtureRecMatchType.fullTimeUnmatched, "");

            case DifferenceKind.OnlyInApp:
                return item.Kept
                    ? FromApp(team, item.App!, FixtureRecMatchType.fullTimeAccepted, $"Not on {sourceName} – {KeptText(item)}")
                    : FromApp(team, item.App!, FixtureRecMatchType.localFixtureUnmatched, $"Not on {sourceName}");

            case DifferenceKind.DateMoved when !inWeek(item.App!.Date):
                // Moved into this week: show FullTime's fixture. It isn't a Fixture Manager fixture, so no id (no edit links).
                var movedIn = FromSource(team, item, FixtureRecMatchType.fullTimeDifferent,
                    $"In Fixture Manager on {item.App.Date:ddd dd/MM}");
                movedIn.Id = Guid.Empty;
                return movedIn;

            case DifferenceKind.DateMoved or DifferenceKind.Different:
                var note = item.Kind == DifferenceKind.DateMoved
                    ? $"{sourceName} has it on {item.Source!.Date:ddd dd/MM}"
                    : $"{sourceName}: {Describe(item.Source!)}";
                return item.Kept
                    ? FromApp(team, item.App!, FixtureRecMatchType.fullTimeAccepted, $"{note} – {KeptText(item)}")
                    : FromApp(team, item.App!, FixtureRecMatchType.fullTimeDifferent, note);

            default:
                return null;
        }
    }

    /// <summary>"kept by me@example.com on 20/11" – who chose Fixture Manager's version on Review FullTime, and when.</summary>
    private static string KeptText(ReconciliationItem item)
    {
        var text = "kept";
        if (item.KeptBy.Length > 0) text += $" by {item.KeptBy}";
        if (item.KeptAt is not null) text += $" on {item.KeptAt.Value.ToLocalTime():dd/MM}";
        return text;
    }

    private static TeamReconiliationRow FromApp(TeamDto team, FixtureDto f, FixtureRecMatchType status, string note = "") => new()
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
        RecStatus = status,
        Note = note,
    };

    private static TeamReconiliationRow FromSource(TeamDto team, ReconciliationItem item, FixtureRecMatchType status, string note)
    {
        var s = item.Source!;
        return new()
        {
            Team = team,
            // Not a Fixture Manager fixture: an id for the row only. It must be the same every time the page is built –
            // the row's download form is named after it, and a post is matched to the form on the rebuilt page.
            Id = new Guid(System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(item.Key))),
            MatchDate = s.Date.GetValueOrDefault(),
            Opponent = s.Opponent,
            Venue = s.IsHome ? "H" : "A",
            FixtureType = s.FixtureType,
            RecStatus = status,
            Note = note,
            FromSource = true,
        };
    }
}
