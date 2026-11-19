using System.Net.Http.Json;

namespace FixtureManagement.Tests.Api;

public class FullTimeApiTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task The_plan_lists_the_club_and_its_teams_on_FullTime()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var onFullTime = await CreateTeamAsync(client, "Plan Lions", League.WycAndSouthBucksMinor, "325026169", "111");
        var notOnFullTime = await CreateTeamAsync(client, "Plan Minis", League.SouthBucksMini, "", "");

        var plan = await client.GetFromJsonAsync<FullTimePlanDto>("/api/fulltime/plan");

        Assert.NotNull(plan);
        Assert.Equal("Thame United", plan.ClubName);
        var wycombe = Assert.Single(plan.Leagues, l => l.League == League.WycAndSouthBucksMinor);
        Assert.Equal("882085998", wycombe.FullTimeLeagueId);
        var team = Assert.Single(wycombe.Teams, t => t.TeamId == onFullTime.Id);
        Assert.Equal(("325026169", "111"), (team.DivisionSeasonId, team.FullTimeTeamId));
        Assert.DoesNotContain(plan.Leagues.SelectMany(l => l.Teams), t => t.TeamId == notOnFullTime.Id);
    }

    private static async Task<TeamDto> CreateTeamAsync(HttpClient client, string name, League league, string divisionSeason, string teamId)
    {
        var response = await client.PostAsJsonAsync("/api/teams", new TeamDto
        {
            Name = name, AgeGroup = AgeGroup.U13, Gender = Gender.Male, League = league, Division = Division.One,
            FullTimeLeagueId = divisionSeason, FullTimeTeamId = teamId,
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TeamDto>())!;
    }
}
