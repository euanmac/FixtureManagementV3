using FixtureManagement.FullTime;

namespace FixtureManagement.Tests.FullTime;

public class FullTimeUploadReaderTests
{
    private static readonly TeamDto Black = new() { Id = Guid.NewGuid(), Name = "Thame United Black", AgeGroup = AgeGroup.U10, FullTimeLeagueId = "111", FullTimeTeamId = "222" };
    private static readonly TeamDto Red = new() { Id = Guid.NewGuid(), Name = "Thame United Red", AgeGroup = AgeGroup.U10, FullTimeLeagueId = "111", FullTimeTeamId = "333" };
    private static readonly TeamDto Minis = new() { Id = Guid.NewGuid(), Name = "Thame United Minis", AgeGroup = AgeGroup.U7 };

    internal const string TeamPage = """
        <html><body><div class="fixtures-table"><table><tbody>
          <tr><td>L</td><td>08/11/26 10:00</td><td class="home-team">Thame United Black U10</td><td>v</td><td class="road-team">Rivals U10</td></tr>
          <tr><td>L</td><td>15/11/26 10:00</td><td class="home-team">Others U10</td><td>v</td><td class="road-team">Thame United Black U10</td></tr>
        </tbody></table></div></body></html>
        """;

    internal const string BlockedPage = "<html><head><title>Attention Required! | Cloudflare</title></head><body>Sorry, you have been blocked</body></html>";

    private static FullTimePageDto Page(TeamDto team, string html) => new() { TeamId = team.Id, Html = html };

    [Fact]
    public void Reads_each_team_page_and_covers_the_team()
    {
        var result = FullTimeUploadReader.Read([Page(Black, TeamPage)], [Black, Red]);

        Assert.Equal([Black.Id], result.TeamIds);
        Assert.Equal(2, result.Fixtures.Count);
        Assert.All(result.Fixtures, f => Assert.Equal(Black.Id, f.TeamId));
        Assert.Empty(result.Failed);
    }

    [Fact]
    public void A_blocked_page_fails_instead_of_looking_like_a_team_with_no_fixtures()
    {
        var result = FullTimeUploadReader.Read([Page(Black, TeamPage), Page(Red, BlockedPage)], [Black, Red]);

        Assert.Equal([Black.Id], result.TeamIds);          // Red isn't covered, so its app fixtures won't look deleted
        var failed = Assert.Single(result.Failed);
        Assert.Equal((Red.Id, Red.DisplayName), (failed.TeamId, failed.TeamName));
        Assert.Contains("isn't a FullTime team page", failed.Error);
    }

    [Fact]
    public void Pages_for_unknown_teams_teams_without_ids_or_sent_twice_fail()
    {
        var stranger = new TeamDto { Id = Guid.NewGuid() };

        var result = FullTimeUploadReader.Read(
            [Page(stranger, TeamPage), Page(Minis, TeamPage), Page(Black, TeamPage), Page(Black, TeamPage)], [Black, Minis]);

        Assert.Equal([Black.Id], result.TeamIds);
        Assert.Equal(2, result.Fixtures.Count);            // the second copy isn't read again
        Assert.Equal(["Not a team on the site.", "The team has no FullTime ids.", "The team's page was sent more than once."],
            result.Failed.Select(f => f.Error));
    }

    [Fact]
    public void Codes_the_league_doesnt_know_are_reported_per_league()
    {
        var vets = new TeamDto { Id = Guid.NewGuid(), Name = "Thame United", AgeGroup = AgeGroup.O50, League = League.OxOver50, FullTimeLeagueId = "1", FullTimeTeamId = "2" };
        const string page = """
            <div class="fixtures-table"><table><tbody>
              <tr><td>ZZ9</td><td>29/11/26 10:30</td><td class="home-team">Thame United</td><td>v</td><td class="road-team">Kidlington</td></tr>
              <tr><td>ZZ9</td><td>06/12/26 10:30</td><td class="home-team">Witney</td><td>v</td><td class="road-team">Thame United</td></tr>
              <tr><td>L</td><td>13/12/26 10:30</td><td class="home-team">Thame United</td><td>v</td><td class="road-team">Bicester</td></tr>
            </tbody></table></div>
            """;

        var result = FullTimeUploadReader.Read([Page(vets, page)], [vets]);

        var code = Assert.Single(result.UnknownTypeCodes);
        Assert.Equal((League.OxOver50, "ZZ9", 2), (code.League, code.Code, code.Count));
        Assert.Equal($"{vets.DisplayName}: 29/11/2026 v Kidlington", code.Example);
        Assert.Equal(FixtureType.Other, result.Fixtures[0].FixtureType);
    }

    [Fact]
    public void A_page_the_parser_cant_read_fails_with_its_reason()
    {
        const string odd = """<div class="fixtures-table"><table><tbody><tr><td>L</td><td>soon</td></tr></tbody></table></div>""";

        var result = FullTimeUploadReader.Read([Page(Black, odd)], [Black]);

        Assert.Empty(result.TeamIds);
        Assert.Contains("Couldn't read the FullTime page", Assert.Single(result.Failed).Error);
    }
}
