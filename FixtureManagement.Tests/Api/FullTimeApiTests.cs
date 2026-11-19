using System.Net;
using System.Net.Http.Json;
using FixtureManagement.Tests.FullTime;
using Microsoft.Extensions.DependencyInjection;

namespace FixtureManagement.Tests.Api;

public class FullTimeApiTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task The_plan_lists_the_club_and_its_teams_on_FullTime()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var onFullTime = await CreateTeamAsync(client, "Plan Lions", "325026169", "111");
        var notOnFullTime = await CreateTeamAsync(client, "Plan Minis", "", "");

        var plan = await client.GetFromJsonAsync<FullTimePlanDto>("/api/fulltime/plan");

        Assert.NotNull(plan);
        Assert.Equal("Thame United", plan.ClubName);
        var team = Assert.Single(plan.Teams, t => t.TeamId == onFullTime.Id);
        Assert.Equal(("325026169", "111"), (team.DivisionSeasonId, team.FullTimeTeamId));
        Assert.DoesNotContain(plan.Teams, t => t.TeamId == notOnFullTime.Id);
    }

    [Fact]
    public async Task Uploading_pages_saves_the_readable_ones_as_the_latest_FullTime_upload()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var black = await CreateTeamAsync(client, "Thame United Black", "1", "2");
        var red = await CreateTeamAsync(client, "Thame United Red", "1", "3");

        var response = await client.PostAsJsonAsync("/api/fulltime/upload", new FullTimeUploadRequest
        {
            Pages =
            [
                new FullTimePageDto { TeamId = black.Id, Html = FullTimeUploadReaderTests.TeamPage },
                new FullTimePageDto { TeamId = red.Id, Html = FullTimeUploadReaderTests.BlockedPage },
            ],
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<FullTimeUploadResultDto>();
        Assert.Equal((1, 2), (result!.Teams, result.Fixtures));
        Assert.Equal(red.Id, Assert.Single(result.Failed).TeamId);

        using var scope = factory.Services.CreateScope();
        var saved = await scope.ServiceProvider.GetRequiredService<ISourceUploadService>().GetLatestUploadAsync("FullTime");
        Assert.NotNull(saved);
        Assert.Equal(result.UploadId, saved.Id);
        Assert.Equal([black.Id], saved.TeamIds);
        Assert.Equal(TestWebApplicationFactory.UserEmail, saved.UploadedBy);
        Assert.Equal(["Rivals U10", "Others U10"], saved.Fixtures.Select(f => f.Opponent));
    }

    [Fact]
    public async Task When_no_page_can_be_read_nothing_is_saved()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var team = await CreateTeamAsync(client, "Thame United Blocked", "1", "9");

        var response = await client.PostAsJsonAsync("/api/fulltime/upload", new FullTimeUploadRequest
        {
            Pages = [new FullTimePageDto { TeamId = team.Id, Html = FullTimeUploadReaderTests.BlockedPage }],
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<FullTimeUploadResultDto>();
        Assert.Equal(Guid.Empty, result!.UploadId);
        Assert.Single(result.Failed);
    }

    [Fact]
    public async Task An_upload_with_no_pages_is_rejected()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var response = await client.PostAsJsonAsync("/api/fulltime/upload", new FullTimeUploadRequest());
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static async Task<TeamDto> CreateTeamAsync(HttpClient client, string name, string divisionSeason, string teamId)
    {
        var response = await client.PostAsJsonAsync("/api/teams", new TeamDto
        {
            Name = name, AgeGroup = AgeGroup.U13, Gender = Gender.Male, League = League.WycAndSouthBucksMinor, Division = Division.One,
            FullTimeLeagueId = divisionSeason, FullTimeTeamId = teamId,
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TeamDto>())!;
    }
}
