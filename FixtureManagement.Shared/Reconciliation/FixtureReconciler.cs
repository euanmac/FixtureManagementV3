using FixtureManagement.Shared.Dtos;
using FixtureManagement.Shared.Models;

namespace FixtureManagement.Shared.Reconciliation;

/// <summary>
/// Compares the app's fixtures with a source's (FullTime, a CSV, a league grid …) for some teams over a date range.
/// Pure, so it can be tested without a database or the network.
///
/// Fixtures are paired team by team, in passes, so each pass only sees what the earlier ones left:
/// 1. same date, venue and opponent                 → Same (or Different if only the type differs)
/// 2. same venue, type and opponent, other date      → DateMoved (a TBC source date counts as Same)
/// 3. same date and the same opponent or type        → Different (otherwise they're two separate fixtures)
/// 4. whatever is left                               → OnlyInSource / OnlyInApp
/// Moves are looked for before "same date, different opponent", so a rearranged fixture isn't mistaken for a
/// change of opponent on its old date.
/// </summary>
public static class FixtureReconciler
{
    /// <param name="teams">The teams to compare. App fixtures of other teams, and source fixtures linked to them, are ignored.</param>
    /// <param name="from">First date to compare (inclusive). Fixtures on both sides outside the range are ignored.</param>
    /// <param name="to">Last date to compare (inclusive).</param>
    /// <param name="sourceFixtures">The source's fixtures. TeamId = Guid.Empty means "not linked to a team"; Date null means TBC.</param>
    public static ReconciliationResult Reconcile(IReadOnlyCollection<TeamDto> teams, DateOnly from, DateOnly to,
        IReadOnlyList<FixtureDto> appFixtures, IReadOnlyList<SourceFixture> sourceFixtures)
    {
        bool InRange(DateOnly date) => date >= from && date <= to;
        bool InRangeOrTbc(DateOnly? date) => date is null || InRange(date.Value);

        var teamsById = teams.ToDictionary(t => t.Id);
        var app = appFixtures.Where(f => teamsById.ContainsKey(f.TeamId) && InRange(f.Date)).ToList();
        var source = sourceFixtures.Where(s => teamsById.ContainsKey(s.TeamId) && InRangeOrTbc(s.Date)).ToList();

        var items = new List<ReconciliationItem>();
        foreach (var team in teams)
        {
            items.AddRange(ReconcileTeam(team,
                app.Where(f => f.TeamId == team.Id).ToList(),
                source.Where(s => s.TeamId == team.Id).ToList()));
        }

        items.AddRange(sourceFixtures
            .Where(s => s.TeamId == Guid.Empty && InRangeOrTbc(s.Date))
            .Select(s => new ReconciliationItem { Kind = DifferenceKind.NotLinked, Source = s, Suggested = Resolution.None }));

        var ordered = items
            .OrderBy(i => i.Date is null)
            .ThenBy(i => i.Date)
            .ThenBy(i => i.Team?.DisplayName ?? i.Source?.SourceTeamName)
            .ThenBy(i => i.Kind)
            .ToList();

        return new ReconciliationResult(ordered, FindClashes(app, source));
    }

    private static List<ReconciliationItem> ReconcileTeam(TeamDto team, List<FixtureDto> app, List<SourceFixture> source)
    {
        var items = new List<ReconciliationItem>();

        void Pair(SourceFixture s, FixtureDto a)
        {
            source.Remove(s);
            app.Remove(a);
            items.Add(Paired(team, a, s));
        }

        // 1. Same date, venue and opponent.
        foreach (var s in source.Where(s => s.Date is not null).ToList())
        {
            var match = app
                .Where(a => a.Date == s.Date && a.IsHome == s.IsHome && SameOpponent(a.Opponent, s.Opponent))
                .OrderByDescending(a => a.FixtureType == s.FixtureType)
                .FirstOrDefault();
            if (match is not null) Pair(s, match);
        }

        // 2. Same venue, type and opponent on another date: the fixture moved (or the source hasn't fixed a date yet).
        foreach (var s in source.ToList())
        {
            var match = app
                .Where(a => a.IsHome == s.IsHome && a.FixtureType == s.FixtureType && SameOpponent(a.Opponent, s.Opponent))
                .OrderBy(a => s.Date is null ? 0 : Math.Abs(a.Date.DayNumber - s.Date.Value.DayNumber))
                .ThenBy(a => a.Date)
                .FirstOrDefault();
            if (match is not null) Pair(s, match);
        }

        // 3. Same date, something else differs – but only if they have something real in common: the same opponent (the
        //    venue or type changed) or the same type (the opponent changed, e.g. within the league). A league game and a
        //    cup tie against someone else on the same day are two different fixtures, not one that changed.
        foreach (var s in source.Where(s => s.Date is not null).ToList())
        {
            var match = app
                .Where(a => a.Date == s.Date
                    && (SameOpponent(a.Opponent, s.Opponent) || a.FixtureType == s.FixtureType))
                .OrderByDescending(a => SameOpponent(a.Opponent, s.Opponent))
                .ThenByDescending(a => a.IsHome == s.IsHome)
                .ThenByDescending(a => a.FixtureType == s.FixtureType)
                .FirstOrDefault();
            if (match is not null) Pair(s, match);
        }

        // 4. Leftovers.
        items.AddRange(source.Select(s => new ReconciliationItem
        {
            Kind = DifferenceKind.OnlyInSource,
            Team = team,
            Source = s,
            // Can't add a fixture without a date.
            Suggested = s.Date is null ? Resolution.KeepApp : Resolution.UseSource,
        }));
        items.AddRange(app.Select(a => new ReconciliationItem
        {
            Kind = DifferenceKind.OnlyInApp,
            Team = team,
            App = a,
            // Sources list competitive fixtures; friendlies and the like are normally only in the app.
            Suggested = IsCompetitive(a.FixtureType) ? Resolution.UseSource : Resolution.KeepApp,
        }));
        return items;
    }

    private static ReconciliationItem Paired(TeamDto team, FixtureDto app, SourceFixture source)
    {
        var changes = FixtureChanges.None;
        if (source.Date is not null && source.Date.Value != app.Date) changes |= FixtureChanges.Date;
        if (!SameOpponent(app.Opponent, source.Opponent)) changes |= FixtureChanges.Opponent;
        if (app.IsHome != source.IsHome) changes |= FixtureChanges.Venue;
        if (app.FixtureType != source.FixtureType) changes |= FixtureChanges.Type;

        var kind = changes == FixtureChanges.None ? DifferenceKind.Same
            : changes.HasFlag(FixtureChanges.Date) ? DifferenceKind.DateMoved
            : DifferenceKind.Different;

        return new ReconciliationItem
        {
            Kind = kind,
            Team = team,
            App = app,
            Source = source,
            Changes = changes,
            Suggested = kind == DifferenceKind.Same ? Resolution.None : Resolution.UseSource,
        };
    }

    /// <summary>
    /// Opponent names must match exactly (case, punctuation and spelling); only spaces at the ends and repeated
    /// spaces are ignored. Anything else is a difference for the user to resolve – no guessing.
    /// </summary>
    internal static bool SameOpponent(string a, string b) => CleanSpaces(a) == CleanSpaces(b);

    private static string CleanSpaces(string name) => string.Join(' ', name.Split(' ', StringSplitOptions.RemoveEmptyEntries));

    private static bool IsCompetitive(FixtureType type) =>
        type is FixtureType.League or FixtureType.Cup or FixtureType.CountyCup;

    private static List<FixtureClash> FindClashes(List<FixtureDto> app, List<SourceFixture> source)
    {
        var inSource = source
            .Where(s => s.Date is not null)
            .GroupBy(s => (s.TeamId, Date: s.Date.GetValueOrDefault()))
            .Where(g => g.Count() > 1)
            .Select(g => new FixtureClash(g.Key.TeamId, g.Key.Date, InSource: true, g.Count()));
        var inApp = app
            .Where(a => a.FixtureType is not (FixtureType.Postponed or FixtureType.Cancelled))
            .GroupBy(a => (a.TeamId, a.Date))
            .Where(g => g.Count() > 1)
            .Select(g => new FixtureClash(g.Key.TeamId, g.Key.Date, InSource: false, g.Count()));
        return inSource.Concat(inApp).OrderBy(c => c.Date).ToList();
    }
}
