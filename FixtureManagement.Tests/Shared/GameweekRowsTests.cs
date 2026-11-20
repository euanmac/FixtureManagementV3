using FixtureManagement.Shared.Reconciliation;

namespace FixtureManagement.Tests.Shared;

public class GameweekRowsTests
{
    private static readonly DateOnly Monday = new(2026, 12, 7);
    private static readonly DateOnly Sunday = new(2026, 12, 13);

    private readonly TeamDto lions = Team("Lions", League.WycAndSouthBucksMinor);   // plays Sundays
    private readonly TeamDto tigers = Team("Tigers", League.WycAndSouthBucksMinor);
    private readonly TeamDto minis = Team("Minis", League.SouthBucksMini, onFullTime: false);

    private static TeamDto Team(string name, League league, bool onFullTime = true) => new()
    {
        Id = Guid.NewGuid(), Name = name, AgeGroup = AgeGroup.U13, League = league,
        FullTimeLeagueId = onFullTime ? "1" : "", FullTimeTeamId = onFullTime ? "2" : "",
    };

    private static FixtureDto App(TeamDto team, DateOnly date, string opponent, bool home = true) =>
        new() { Id = Guid.NewGuid(), TeamId = team.Id, Date = date, Opponent = opponent, IsHome = home, FixtureType = FixtureType.League };

    private static SourceFixture Src(TeamDto team, DateOnly? date, string opponent, bool home = true) =>
        new() { TeamId = team.Id, Date = date, Opponent = opponent, IsHome = home, FixtureType = FixtureType.League };

    private static readonly DateTime KeptAt = new(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);

    private static ReconciliationItem Item(DifferenceKind kind, TeamDto team, FixtureDto? app, SourceFixture? source, bool kept = false) =>
        new() { Kind = kind, Team = team, App = app, Source = source, Kept = kept,
                KeptBy = kept ? "me@example.com" : "", KeptAt = kept ? KeptAt : null };

    private ReconciliationWeekDto Week(params ReconciliationItem[] items) => new()
    {
        Upload = new SourceUploadSummaryDto { Source = "FullTime", SourceName = "FullTime" },
        CoveredTeamIds = [lions.Id, tigers.Id],
        CoveredFrom = new DateOnly(2026, 11, 20),
        Items = items,
    };

    private Dictionary<TeamDto, List<TeamReconiliationRow>> Build(IReadOnlyList<FixtureDto> weekFixtures, ReconciliationWeekDto week,
        params TeamDto[] teams) =>
        GameweekRows.Build(teams.Length == 0 ? [lions, tigers, minis] : teams, Monday, Sunday, weekFixtures, week);

    [Fact]
    public void Each_kind_of_comparison_gets_its_Gameweek_status()
    {
        var same = App(lions, Sunday, "Same");
        var dropped = App(lions, Sunday, "Dropped", home: false);
        var different = App(tigers, Sunday, "Old opponent");

        var rows = Build([same, dropped, different], Week(
            Item(DifferenceKind.Same, lions, same, Src(lions, Sunday, "Same")),
            Item(DifferenceKind.OnlyInApp, lions, dropped, null),
            Item(DifferenceKind.OnlyInSource, lions, null, Src(lions, Sunday, "New", home: false)),
            Item(DifferenceKind.Different, tigers, different, Src(tigers, Sunday, "New opponent"))));

        Assert.Equal(
            [(FixtureRecMatchType.fullTimematched, "Same"), (FixtureRecMatchType.localFixtureUnmatched, "Dropped"), (FixtureRecMatchType.fullTimeUnmatched, "New")],
            rows[lions].Select(r => (r.RecStatus, r.Opponent)));
        var tigersRow = Assert.Single(rows[tigers]);
        Assert.Equal((FixtureRecMatchType.fullTimeDifferent, different.Id, "FullTime: H v New opponent (League)"),
            (tigersRow.RecStatus, tigersRow.Id, tigersRow.Note));
        Assert.Equal(FixtureRecMatchType.noFixture, Assert.Single(rows[minis]).RecStatus);   // nothing this week
        // Rows with something to decide carry the difference's id (for action buttons); a match doesn't.
        Assert.Equal("", rows[lions][0].ItemId);
        Assert.All(rows[lions].Skip(1).Append(tigersRow), r => Assert.Equal(16, r.ItemId.Length));
    }

    [Fact]
    public void A_FullTime_only_row_has_the_same_id_every_time_so_its_download_form_can_be_found_after_a_post()
    {
        var week = Week(Item(DifferenceKind.OnlyInSource, lions, null, Src(lions, Sunday, "New")),
                        Item(DifferenceKind.OnlyInSource, lions, null, Src(lions, Sunday, "Other")));

        var first = Build([], week, lions)[lions].Select(r => r.Id).ToList();
        var second = Build([], week, lions)[lions].Select(r => r.Id).ToList();

        Assert.Equal(first, second);
        Assert.Equal(2, first.Distinct().Count());
        Assert.DoesNotContain(Guid.Empty, first);
    }

    [Fact]
    public void A_fixture_moved_out_of_the_week_shows_Fixture_Managers_fixture_with_where_it_went()
    {
        var moved = App(lions, Sunday, "Rivals");

        var row = Assert.Single(Build([moved], Week(Item(DifferenceKind.DateMoved, lions, moved, Src(lions, Sunday.AddDays(7), "Rivals"))))[lions]);

        Assert.Equal((FixtureRecMatchType.fullTimeDifferent, moved.Id, Sunday), (row.RecStatus, row.Id, row.MatchDate));
        Assert.Equal("FullTime has it on Sun 20/12", row.Note);
    }

    [Fact]
    public void A_fixture_moved_into_the_week_shows_FullTimes_fixture_without_an_id()
    {
        var moved = App(lions, Sunday.AddDays(-7), "Rivals");     // Fixture Manager still has it last week

        var row = Assert.Single(Build([], Week(Item(DifferenceKind.DateMoved, lions, moved, Src(lions, Sunday, "Rivals"))))[lions]);

        Assert.Equal((FixtureRecMatchType.fullTimeDifferent, Guid.Empty, Sunday), (row.RecStatus, row.Id, row.MatchDate));
        Assert.Equal("In Fixture Manager on Sun 06/12", row.Note);
    }

    [Fact]
    public void Accepted_differences_are_their_own_status_with_who_and_when_and_can_still_be_acted_on()
    {
        var different = App(lions, Sunday, "Our name");
        var dropped = App(tigers, Sunday, "Friendly-ish");

        var rows = Build([different, dropped], Week(
            Item(DifferenceKind.Different, lions, different, Src(lions, Sunday, "Their name"), kept: true),
            Item(DifferenceKind.OnlyInSource, lions, null, Src(lions, Sunday, "Not wanted"), kept: true),
            Item(DifferenceKind.OnlyInApp, tigers, dropped, null, kept: true)));

        var kept = $"kept by me@example.com on {KeptAt.ToLocalTime():dd/MM}";
        Assert.Equal(
            [(FixtureRecMatchType.fullTimeAccepted, $"FullTime: H v Their name (League) – {kept}", false, DifferenceKind.Different),
             (FixtureRecMatchType.fullTimeAccepted, $"Not added – {kept}", true, DifferenceKind.OnlyInSource)],
            rows[lions].Select(r => (r.RecStatus, r.Note, r.FromSource, r.Difference.GetValueOrDefault())));
        var tigersRow = Assert.Single(rows[tigers]);
        Assert.Equal((FixtureRecMatchType.fullTimeAccepted, $"Not on FullTime – {kept}"), (tigersRow.RecStatus, tigersRow.Note));
        Assert.All(rows[lions].Append(tigersRow), r => Assert.NotEqual("", r.ItemId));    // the decision can be changed
    }

    [Fact]
    public void Fixtures_the_upload_didnt_compare_are_shown_as_Fixture_Managers_only_with_why()
    {
        var notCovered = Team("Not fetched", League.WycAndSouthBucksMinor);       // on FullTime but its page failed
        var notOnFullTime = App(minis, Sunday, "Minis game");
        var missed = App(notCovered, Sunday, "Someone");

        var rows = Build([notOnFullTime, missed], Week(), minis, notCovered);

        Assert.Equal((FixtureRecMatchType.localFixtureOnly, ""), (rows[minis][0].RecStatus, rows[minis][0].Note));
        Assert.Equal((FixtureRecMatchType.localFixtureOnly, "Not in the last FullTime upload"), (rows[notCovered][0].RecStatus, rows[notCovered][0].Note));
    }

    [Fact]
    public void Fixtures_before_the_fetch_say_they_werent_compared()
    {
        var early = App(lions, new DateOnly(2026, 11, 15), "Last week");    // the upload covers from 20/11

        var rows = GameweekRows.Build([lions], new DateOnly(2026, 11, 9), new DateOnly(2026, 11, 15), [early], Week());

        var row = Assert.Single(rows[lions]);
        Assert.Equal((FixtureRecMatchType.localFixtureOnly, "Not compared – before the FullTime fetch of 20/11"), (row.RecStatus, row.Note));
    }

    [Fact]
    public void With_no_upload_every_fixture_is_Fixture_Managers_only()
    {
        var fixture = App(lions, Sunday, "Rivals");

        var rows = Build([fixture], new ReconciliationWeekDto(), lions);

        Assert.Equal((FixtureRecMatchType.localFixtureOnly, ""), (rows[lions][0].RecStatus, rows[lions][0].Note));
    }

    [Fact]
    public void Allocation_details_come_from_the_Fixture_Manager_fixture()
    {
        var allocated = App(lions, Sunday, "Rivals");
        allocated.Allocation = new FixtureAllocationDto { Id = Guid.NewGuid(), PitchId = Guid.NewGuid(), PitchName = "Main", Start = new TimeOnly(10, 30), Duration = TimeSpan.FromHours(1), IsConfirmed = true };

        var row = Assert.Single(Build([allocated], Week(Item(DifferenceKind.Same, lions, allocated, Src(lions, Sunday, "Rivals"))))[lions]);

        Assert.Equal((true, allocated.Allocation.Id, "Main", new TimeOnly(10, 30), true), (row.IsAllocated, row.AllocationId, row.Pitch, row.Start, row.IsConfirmed));
    }
}
