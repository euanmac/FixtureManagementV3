using FixtureManagement.Shared.Reconciliation;

namespace FixtureManagement.Tests.Shared;

public class FullTimePlanTests
{
    private static TeamDto Team(string name, League league, string? divisionSeason = "", string? teamId = "") => new()
    {
        Id = Guid.NewGuid(), Name = name, AgeGroup = AgeGroup.U13, Gender = Gender.Male, League = league,
        FullTimeLeagueId = divisionSeason, FullTimeTeamId = teamId,
    };

    [Fact]
    public void Groups_teams_on_FullTime_by_league_with_the_league_id()
    {
        var lions = Team("Thame United Lions", League.WycAndSouthBucksMinor, "325026169", "111");
        var tigers = Team("Thame United Tigers", League.WycAndSouthBucksMinor, "814657242", "222");
        var vets = Team("Thame United", League.OxOver50, "942258292", "333");

        var plan = FullTimePlan.Build([tigers, vets, lions], "Thame United");

        Assert.Equal("Thame United", plan.ClubName);
        Assert.Equal(["396136022", "882085998"], plan.Leagues.Select(l => l.FullTimeLeagueId));   // ordered by league name
        var wycombe = plan.Leagues[1];
        Assert.Equal(League.WycAndSouthBucksMinor, wycombe.League);
        Assert.Equal("Wycombe and South Bucks Minor League", wycombe.LeagueName);
        Assert.Equal([lions.Id, tigers.Id], wycombe.Teams.Select(t => t.TeamId));
        Assert.Equal(("325026169", "111", lions.DisplayName),
            (wycombe.Teams[0].DivisionSeasonId, wycombe.Teams[0].FullTimeTeamId, wycombe.Teams[0].DisplayName));
        Assert.Empty(plan.TeamsWithoutLeagueId);
    }

    [Theory]
    [InlineData("", "")]
    [InlineData(null, null)]
    [InlineData("325026169", "")]       // half-filled in: not usable
    [InlineData("", "111")]
    [InlineData("  ", "111")]
    public void Teams_without_both_FullTime_ids_are_ignored(string? divisionSeason, string? teamId)
    {
        var plan = FullTimePlan.Build([Team("Minis", League.WycAndSouthBucksMinor, divisionSeason, teamId)], "Thame United");

        Assert.Empty(plan.Leagues);
        Assert.Empty(plan.TeamsWithoutLeagueId);
    }

    [Fact]
    public void Teams_in_a_league_with_no_FullTime_league_id_are_listed_separately()
    {
        var aylesbury = Team("Thame United Reserves", League.ADL, "29430638", "444");

        var plan = FullTimePlan.Build([aylesbury], "Thame United");

        Assert.Empty(plan.Leagues);
        Assert.Equal(aylesbury.Id, Assert.Single(plan.TeamsWithoutLeagueId).TeamId);
    }

    [Fact]
    public void Ids_are_trimmed()
    {
        var plan = FullTimePlan.Build([Team("Lions", League.OxGirls, " 512857575 ", " 555 ")], "Thame United");

        var team = Assert.Single(Assert.Single(plan.Leagues).Teams);
        Assert.Equal(("512857575", "555"), (team.DivisionSeasonId, team.FullTimeTeamId));
    }
}
