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
    public void Lists_each_team_on_FullTime_with_its_page()
    {
        var lions = Team("Thame United Lions", League.WycAndSouthBucksMinor, "325026169", "111");
        var vets = Team("Thame United", League.OxOver50, "942258292", "333");
        var men = Team("Thame United A", League.ADL, "29430638", "444");      // a league with no FullTime league id: still included

        var plan = FullTimePlan.Build([lions, vets, men], "Thame United");

        Assert.Equal("Thame United", plan.ClubName);
        Assert.Equal([men.Id, vets.Id, lions.Id], plan.Teams.Select(t => t.TeamId));   // by league name, then team
        var team = plan.Teams[2];
        Assert.Equal(("325026169", "111", lions.DisplayName, "Wycombe and South Bucks Minor League"),
            (team.DivisionSeasonId, team.FullTimeTeamId, team.DisplayName, team.LeagueName));
        Assert.Equal("https://fulltime.thefa.com/displayTeam.html?divisionseason=325026169&teamID=111", team.PageUrl);
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
        Assert.Empty(plan.Teams);
    }

    [Fact]
    public void Ids_are_trimmed()
    {
        var team = Assert.Single(FullTimePlan.Build([Team("Lions", League.OxGirls, " 512857575 ", " 555 ")], "Thame United").Teams);

        Assert.Equal(("512857575", "555"), (team.DivisionSeasonId, team.FullTimeTeamId));
        Assert.EndsWith("divisionseason=512857575&teamID=555", team.PageUrl);
    }
}
