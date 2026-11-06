using FixtureManagement.FullTime;

namespace FixtureManagement.Tests.FullTime;

public class FullTimeReconcilerTests
{
    private static readonly DateOnly Monday = new(2026, 11, 2);
    private static readonly DateOnly Sunday = new(2026, 11, 8);

    private static TeamDto Team(bool onFullTime, League league = League.SouthBucksMini) => new()
    {
        Id = Guid.NewGuid(), Name = "Lions", League = league,
        FullTimeLeagueId = onFullTime ? "1" : "", FullTimeTeamId = onFullTime ? "2" : "",
    };

    private static FixtureDto Local(TeamDto team, DateOnly date, string opponent, bool home = true, FixtureType type = FixtureType.League,
        FixtureAllocationDto? allocation = null) =>
        new() { Id = Guid.NewGuid(), TeamId = team.Id, Date = date, Opponent = opponent, IsHome = home, FixtureType = type, Allocation = allocation };

    private static FullTimeFixture OnFullTime(DateOnly date, string opponent, bool home = true, FixtureType type = FixtureType.League) =>
        new() { Date = date, Opponent = opponent, IsHome = home, FixtureType = type };

    [Fact]
    public void Nothing_either_side_gives_a_no_fixture_row_on_the_match_day()
    {
        var sundayLeague = Team(onFullTime: true, League.SouthBucksMini);    // plays Sundays
        var saturdayLeague = Team(onFullTime: true, League.BucksGirls);      // plays Saturdays

        var sunday = Assert.Single(FullTimeReconciler.ReconcileTeam(sundayLeague, Monday, Sunday, [], []));
        var saturday = Assert.Single(FullTimeReconciler.ReconcileTeam(saturdayLeague, Monday, Sunday, [], []));

        Assert.Equal(FixtureRecMatchType.noFixture, sunday.RecStatus);
        Assert.Equal(Sunday, sunday.MatchDate);
        Assert.Equal(new DateOnly(2026, 11, 7), saturday.MatchDate);
    }

    [Fact]
    public void A_local_fixture_that_matches_FullTime_exactly_is_matched()
    {
        var team = Team(onFullTime: true);
        var row = Assert.Single(FullTimeReconciler.ReconcileTeam(team, Monday, Sunday,
            [Local(team, Sunday, "Rivals")], [OnFullTime(Sunday, "Rivals")]));

        Assert.Equal(FixtureRecMatchType.fullTimematched, row.RecStatus);
        Assert.Equal("H", row.Venue);
    }

    [Theory]
    [InlineData("Other Opponent", true, FixtureType.League)]
    [InlineData("Rivals", false, FixtureType.League)]   // venue differs
    [InlineData("Rivals", true, FixtureType.Cup)]       // type differs
    public void Any_difference_leaves_both_sides_unmatched(string ftOpponent, bool ftHome, FixtureType ftType)
    {
        var team = Team(onFullTime: true);
        var rows = FullTimeReconciler.ReconcileTeam(team, Monday, Sunday,
            [Local(team, Sunday, "Rivals")], [OnFullTime(Sunday, ftOpponent, ftHome, ftType)]);

        Assert.Equal([FixtureRecMatchType.localFixtureUnmatched, FixtureRecMatchType.fullTimeUnmatched], rows.Select(r => r.RecStatus));
    }

    [Fact]
    public void Teams_not_on_FullTime_show_local_fixtures_as_local_only()
    {
        var team = Team(onFullTime: false);
        var row = Assert.Single(FullTimeReconciler.ReconcileTeam(team, Monday, Sunday, [Local(team, Sunday, "Rivals")], []));
        Assert.Equal(FixtureRecMatchType.localFixtureOnly, row.RecStatus);
    }

    [Fact]
    public void Only_this_teams_fixtures_in_this_week_are_considered()
    {
        var team = Team(onFullTime: false);
        var other = Team(onFullTime: false);
        var rows = FullTimeReconciler.ReconcileTeam(team, Monday, Sunday, [
            Local(team, Monday.AddDays(-1), "Last week"),
            Local(team, Sunday.AddDays(1), "Next week"),
            Local(other, Sunday, "Someone else"),
            Local(team, Sunday, "This week")], []);

        Assert.Equal(["This week"], rows.Select(r => r.Opponent));
    }

    [Fact]
    public void Allocation_details_are_carried_onto_the_row()
    {
        var team = Team(onFullTime: false);
        var allocation = new FixtureAllocationDto { Id = Guid.NewGuid(), PitchId = Guid.NewGuid(), PitchName = "Main", Start = new TimeOnly(9, 30), Duration = TimeSpan.FromMinutes(90), IsConfirmed = true };
        var row = Assert.Single(FullTimeReconciler.ReconcileTeam(team, Monday, Sunday, [Local(team, Sunday, "Rivals", allocation: allocation)], []));

        Assert.True(row.IsAllocated);
        Assert.True(row.CanAllocate);
        Assert.Equal(allocation.Id, row.AllocationId);
        Assert.Equal("Main", row.Pitch);
        Assert.Equal(new TimeOnly(9, 30), row.Start);
        Assert.True(row.IsConfirmed);
    }

    [Fact]
    public void Each_FullTime_fixture_matches_at_most_once()
    {
        var team = Team(onFullTime: true);
        var rows = FullTimeReconciler.ReconcileTeam(team, Monday, Sunday,
            [Local(team, Sunday, "Rivals"), Local(team, Sunday, "Rivals")],   // a duplicate local fixture
            [OnFullTime(Sunday, "Rivals")]);

        Assert.Equal([FixtureRecMatchType.fullTimematched, FixtureRecMatchType.localFixtureUnmatched], rows.Select(r => r.RecStatus));
    }
}
