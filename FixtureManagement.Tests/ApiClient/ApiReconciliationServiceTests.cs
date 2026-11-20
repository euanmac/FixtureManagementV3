using System.Net.Http.Json;
using FixtureManagement.ApiClient;
using FixtureManagement.Shared.Reconciliation;
using FixtureManagement.Tests.Api;

namespace FixtureManagement.Tests.ApiClient;

public class ApiReconciliationServiceTests(TestWebApplicationFactory factory) : ApiClientTestBase(factory)
{
    [Fact]
    public async Task Review_and_apply_through_the_api()
    {
        var teams = new ApiTeamService(Api());
        var fixtures = new ApiFixtureService(Api());
        var reconciliation = new ApiReconciliationService(Api());
        var team = await teams.CreateTeamAsync(new TeamDto
        {
            Name = "Thame United Black", AgeGroup = AgeGroup.U10, Gender = Gender.Male, League = League.WycAndSouthBucksMinor,
            FullTimeLeagueId = "1", FullTimeTeamId = "2",
        });
        // Dates from next week, so they're in the review whatever day the test runs.
        var day1 = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7);
        var day2 = day1.AddDays(7);
        await fixtures.CreateFixtureAsync(new FixtureDto { TeamId = team.Id, Date = day1, Opponent = "Rivals U10", IsHome = true, FixtureType = FixtureType.League });
        var page = $"""
            <div class="fixtures-table"><table><tbody>
              <tr><td>L</td><td>{day1:dd/MM/yy} 10:00</td><td class="home-team">Thame United Black U10</td><td>v</td><td class="road-team">Rivals U10</td></tr>
              <tr><td>L</td><td>{day2:dd/MM/yy} 10:00</td><td class="home-team">Others U10</td><td>v</td><td class="road-team">Thame United Black U10</td></tr>
            </tbody></table></div>
            """;
        (await Api().PostAsJsonAsync("api/fulltime/upload", new FullTimeUploadRequest { Pages = [new FullTimePageDto { TeamId = team.Id, Html = page }] }))
            .EnsureSuccessStatusCode();

        var review = await reconciliation.GetReviewAsync("FullTime");

        Assert.Equal("FullTime", review.Upload!.Source);
        Assert.Equal(1, review.SameCount);
        var item = Assert.Single(review.Items);
        Assert.Equal((DifferenceKind.OnlyInSource, "Others U10", Resolution.UseSource), (item.Kind, item.Source!.Opponent, item.Suggested));
        Assert.Equal(team.Id, item.Team!.Id);

        var result = await reconciliation.ApplyAsync("FullTime", new Dictionary<string, Resolution> { [item.Id] = item.Suggested });

        Assert.Equal(1, result.Added);
        Assert.Empty((await reconciliation.GetReviewAsync("FullTime")).Items);
        Assert.Contains(await fixtures.GetFixturesAsync(new FixtureQuery { TeamId = team.Id }), f => f.Date == day2 && f.Opponent == "Others U10" && !f.IsHome);
    }
}
