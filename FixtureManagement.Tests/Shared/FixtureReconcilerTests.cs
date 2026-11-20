using FixtureManagement.Shared.Reconciliation;

namespace FixtureManagement.Tests.Shared;

public class FixtureReconcilerTests
{
    private static readonly DateOnly From = new(2026, 11, 1);
    private static readonly DateOnly To = new(2027, 5, 31);
    private static readonly DateOnly Dec6 = new(2026, 12, 6);
    private static readonly DateOnly Dec13 = new(2026, 12, 13);
    private static readonly DateOnly Dec20 = new(2026, 12, 20);

    private readonly TeamDto lions = new() { Id = Guid.NewGuid(), Name = "Lions", AgeGroup = AgeGroup.U13, League = League.WycAndSouthBucksMinor };
    private readonly TeamDto tigers = new() { Id = Guid.NewGuid(), Name = "Tigers", AgeGroup = AgeGroup.U14, League = League.WycAndSouthBucksMinor };

    private static FixtureDto App(TeamDto team, DateOnly date, string opponent, bool home = true,
        FixtureType type = FixtureType.League, bool allocated = false) => new()
    {
        Id = Guid.NewGuid(), TeamId = team.Id, Date = date, Opponent = opponent, IsHome = home, FixtureType = type,
        Allocation = allocated
            ? new FixtureAllocationDto { Id = Guid.NewGuid(), PitchId = Guid.NewGuid(), Start = new TimeOnly(10, 0), Duration = TimeSpan.FromHours(1) }
            : null,
    };

    private static SourceFixture Src(TeamDto? team, DateOnly? date, string opponent, bool home = true,
        FixtureType type = FixtureType.League) => new()
    {
        TeamId = team?.Id ?? Guid.Empty, SourceTeamName = team?.Name ?? "Thame United Pumas",
        Date = date, Opponent = opponent, IsHome = home, FixtureType = type,
    };

    private ReconciliationResult Run(IReadOnlyList<FixtureDto> app, IReadOnlyList<SourceFixture> source) =>
        FixtureReconciler.Reconcile([lions, tigers], From, To, app, source);

    [Fact]
    public void Identical_fixtures_are_the_same_with_nothing_to_decide()
    {
        var item = Assert.Single(Run([App(lions, Dec6, "Rivals")], [Src(lions, Dec6, "Rivals")]).Items);

        Assert.Equal(DifferenceKind.Same, item.Kind);
        Assert.Equal(FixtureChanges.None, item.Changes);
        Assert.Equal(Resolution.None, item.Suggested);
        Assert.Same(lions, item.Team);
    }

    [Theory]
    [InlineData("Ickenham Youth F.C.WHITE", "Ickenham Youth F.C. WHITE")]   // punctuation/spacing inside the name
    [InlineData("Middleton Cheney", "Middletone Cheney")]                   // spelling
    [InlineData("Rivals", "rivals")]                                        // case
    [InlineData("Eynsham", "Eynsham Association U11 White")]                // more detail
    public void Opponent_names_must_match_exactly(string appOpponent, string sourceOpponent)
    {
        var item = Assert.Single(Run([App(lions, Dec6, appOpponent)], [Src(lions, Dec6, sourceOpponent)]).Items);

        Assert.Equal(DifferenceKind.Different, item.Kind);
        Assert.Equal(FixtureChanges.Opponent, item.Changes);
    }

    [Fact]
    public void Spaces_at_the_ends_and_repeated_spaces_are_ignored()
    {
        var item = Assert.Single(Run([App(lions, Dec6, " Holmer  Green ")], [Src(lions, Dec6, "Holmer Green")]).Items);
        Assert.Equal(DifferenceKind.Same, item.Kind);
    }

    [Fact]
    public void A_fixture_moved_with_a_differently_written_opponent_is_not_a_move()
    {
        var result = Run([App(lions, Dec6, "Middleton Cheney")], [Src(lions, Dec20, "Middletone Cheney")]);
        Assert.Equal([DifferenceKind.OnlyInApp, DifferenceKind.OnlyInSource], result.Items.Select(i => i.Kind));
    }

    [Fact]
    public void Only_the_type_differing_is_a_difference()
    {
        var item = Assert.Single(Run([App(lions, Dec6, "Rivals", type: FixtureType.Friendly)], [Src(lions, Dec6, "Rivals", type: FixtureType.Cup)]).Items);

        Assert.Equal(DifferenceKind.Different, item.Kind);
        Assert.Equal(FixtureChanges.Type, item.Changes);
        Assert.Equal(Resolution.UseSource, item.Suggested);
    }

    [Fact]
    public void A_rearranged_fixture_is_a_date_move_not_a_delete_and_an_add()
    {
        var app = App(lions, Dec6, "Rivals");
        var item = Assert.Single(Run([app], [Src(lions, Dec20, "Rivals")]).Items);

        Assert.Equal(DifferenceKind.DateMoved, item.Kind);
        Assert.Equal(FixtureChanges.Date, item.Changes);
        Assert.Same(app, item.App);
        Assert.Equal(Dec20, item.Date);
        Assert.False(item.BroughtForward);
    }

    [Fact]
    public void A_fixture_moved_earlier_is_brought_forward()
    {
        var item = Assert.Single(Run([App(lions, Dec20, "Rivals")], [Src(lions, Dec6, "Rivals")]).Items);
        Assert.True(item.BroughtForward);
    }

    [Fact]
    public void A_move_is_found_before_a_new_opponent_on_the_old_date()
    {
        // Rivals moved from the 13th to the 20th, and a new opponent took the 13th.
        var result = Run([App(lions, Dec13, "Rivals")], [Src(lions, Dec13, "Newcomers"), Src(lions, Dec20, "Rivals")]);

        var moved = Assert.Single(result.Items, i => i.Kind == DifferenceKind.DateMoved);
        Assert.Equal("Rivals", moved.Source!.Opponent);
        var added = Assert.Single(result.Items, i => i.Kind == DifferenceKind.OnlyInSource);
        Assert.Equal("Newcomers", added.Source!.Opponent);
        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public void Only_the_moved_leg_of_a_home_and_away_pair_moves()
    {
        var result = Run(
            [App(lions, Dec6, "Rivals", home: true), App(lions, Dec13, "Rivals", home: false)],
            [Src(lions, Dec6, "Rivals", home: true), Src(lions, Dec20, "Rivals", home: false)]);

        Assert.Equal([DifferenceKind.Same, DifferenceKind.DateMoved], result.Items.Select(i => i.Kind));
        Assert.False(result.Items[1].App!.IsHome);
    }

    [Fact]
    public void A_new_opponent_on_the_same_date_is_different()
    {
        var item = Assert.Single(Run([App(lions, Dec6, "Rivals")], [Src(lions, Dec6, "Newcomers")]).Items);

        Assert.Equal(DifferenceKind.Different, item.Kind);
        Assert.Equal(FixtureChanges.Opponent, item.Changes);
    }

    [Fact]
    public void A_different_opponent_and_type_on_the_same_date_are_two_separate_fixtures()
    {
        // e.g. Fixture Manager has a league game, FullTime a cup tie against someone else, the same Saturday.
        var result = Run([App(lions, Dec6, "Hook Norton", home: true)], [Src(lions, Dec6, "Middleton Cheney", home: false, type: FixtureType.Cup)]);

        Assert.Equal([DifferenceKind.OnlyInSource, DifferenceKind.OnlyInApp], result.Items.Select(i => i.Kind));
    }

    [Fact]
    public void Same_day_pairing_prefers_the_fixture_with_the_same_opponent()
    {
        var league = App(lions, Dec6, "Rivals", home: true);
        var cup = App(lions, Dec6, "Cup Opponents", home: true, type: FixtureType.Cup);

        // FullTime has "Cup Opponents" away, as a league game: it shares the opponent with the cup fixture (and only the type
        // with the league one), so it pairs with the cup fixture; the league game is left on its own.
        var result = Run([league, cup], [Src(lions, Dec6, "Cup Opponents", home: false, type: FixtureType.League)]);

        var paired = Assert.Single(result.Items, i => i.Kind == DifferenceKind.Different);
        Assert.Same(cup, paired.App);
        Assert.Equal(FixtureChanges.Venue | FixtureChanges.Type, paired.Changes);
        Assert.Same(league, Assert.Single(result.Items, i => i.Kind == DifferenceKind.OnlyInApp).App);
    }

    [Fact]
    public void Swapped_venue_on_the_same_date_is_different()
    {
        var item = Assert.Single(Run([App(lions, Dec6, "Rivals", home: true)], [Src(lions, Dec6, "Rivals", home: false)]).Items);

        Assert.Equal(DifferenceKind.Different, item.Kind);
        Assert.Equal(FixtureChanges.Venue, item.Changes);
    }

    [Fact]
    public void Unpaired_fixtures_are_only_on_one_side()
    {
        var result = Run([App(lions, Dec6, "Rivals")], [Src(lions, Dec13, "Newcomers")]);

        Assert.Equal([DifferenceKind.OnlyInApp, DifferenceKind.OnlyInSource], result.Items.Select(i => i.Kind));
        Assert.All(result.Items, i => Assert.Equal(Resolution.UseSource, i.Suggested));
    }

    [Fact]
    public void A_friendly_only_in_the_app_is_kept_by_default()
    {
        var item = Assert.Single(Run([App(lions, Dec6, "Pals", type: FixtureType.Friendly)], []).Items);

        Assert.Equal(DifferenceKind.OnlyInApp, item.Kind);
        Assert.Equal(Resolution.KeepApp, item.Suggested);
    }

    [Fact]
    public void A_source_fixture_with_no_date_yet_matches_the_app_fixture()
    {
        var item = Assert.Single(Run([App(lions, Dec6, "Rivals", type: FixtureType.Cup)], [Src(lions, null, "Rivals", type: FixtureType.Cup)]).Items);
        Assert.Equal(DifferenceKind.Same, item.Kind);
    }

    [Fact]
    public void An_unmatched_source_fixture_with_no_date_cant_be_added_and_sorts_last()
    {
        var result = Run([], [Src(lions, null, "Rivals", type: FixtureType.Cup), Src(lions, Dec6, "Others")]);

        Assert.Equal(["Others", "Rivals"], result.Items.Select(i => i.Source!.Opponent));
        Assert.Equal(Resolution.KeepApp, result.Items[1].Suggested);
    }

    [Fact]
    public void Other_teams_and_dates_outside_the_range_are_ignored()
    {
        var otherTeam = new TeamDto { Id = Guid.NewGuid(), Name = "Not in scope" };
        var result = Run(
            [App(otherTeam, Dec6, "Rivals"), App(lions, new DateOnly(2026, 10, 25), "Past"), App(lions, new DateOnly(2027, 6, 6), "Summer")],
            [Src(otherTeam, Dec6, "Rivals"), Src(lions, new DateOnly(2026, 10, 25), "Past")]);

        Assert.Empty(result.Items);
    }

    [Fact]
    public void Source_fixtures_for_an_unknown_club_team_are_not_linked()
    {
        var item = Assert.Single(Run([], [Src(null, Dec6, "Rivals")]).Items);

        Assert.Equal(DifferenceKind.NotLinked, item.Kind);
        Assert.Null(item.Team);
        Assert.Equal(Resolution.None, item.Suggested);
    }

    [Fact]
    public void Teams_are_reconciled_separately()
    {
        var result = Run([App(lions, Dec6, "Rivals")], [Src(tigers, Dec6, "Rivals")]);

        Assert.Equal([DifferenceKind.OnlyInApp, DifferenceKind.OnlyInSource], result.Items.Select(i => i.Kind));
    }

    [Fact]
    public void Two_fixtures_for_a_team_on_one_day_are_a_clash()
    {
        var result = Run(
            [App(tigers, Dec13, "A"), App(tigers, Dec13, "B", type: FixtureType.Postponed)],      // postponed doesn't count
            [Src(lions, Dec13, "League game"), Src(lions, Dec13, "Cup tie", type: FixtureType.CountyCup)]);

        var clash = Assert.Single(result.Clashes);
        Assert.Equal((lions.Id, Dec13, true, 2), (clash.TeamId, clash.Date, clash.InSource, clash.Count));
    }

    [Theory]
    [InlineData("Rivals", true, "2026-12-20", true)]     // moved
    [InlineData("Rivals", false, "2026-12-06", true)]    // no longer at home
    [InlineData("Others", true, "2026-12-06", false)]    // new opponent, same slot: the pitch booking still stands
    public void Changes_that_undo_a_pitch_allocation_are_flagged(string opponent, bool home, string date, bool affects)
    {
        var item = Assert.Single(Run([App(lions, Dec6, "Rivals", allocated: true)], [Src(lions, DateOnly.Parse(date), opponent, home)]).Items);
        Assert.Equal(affects, item.AffectsAllocation);
    }

    [Fact]
    public void The_key_is_stable_but_changes_when_either_side_changes()
    {
        var app = App(lions, Dec6, "Rivals");
        string KeyFor(FixtureDto a, SourceFixture s) => Assert.Single(Run([a], [s]).Items).Key;

        var key = KeyFor(app, Src(lions, Dec6, "Newcomers"));

        Assert.Equal(key, KeyFor(app, Src(lions, Dec6, "Newcomers")));
        Assert.NotEqual(key, KeyFor(app, Src(lions, Dec6, "Newcomers FC")));                         // source changed
        Assert.NotEqual(key, KeyFor(App(lions, Dec6, "Rivals", home: true), Src(lions, Dec6, "Newcomers"))); // another app fixture
    }

    [Fact]
    public void Deleting_an_allocated_fixture_is_flagged()
    {
        var item = Assert.Single(Run([App(lions, Dec6, "Rivals", allocated: true)], []).Items);
        Assert.True(item.AffectsAllocation);
    }
}
