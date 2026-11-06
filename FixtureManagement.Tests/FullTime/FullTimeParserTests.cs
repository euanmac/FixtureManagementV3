using FixtureManagement.FullTime;

namespace FixtureManagement.Tests.FullTime;

public class FullTimeParserTests
{
    private static readonly TeamDto Black = new() { Id = Guid.NewGuid(), Name = "Thame United Black", AgeGroup = AgeGroup.U10, FullTimeLeagueId = "111", FullTimeTeamId = "222" };

    // The shape of a FullTime displayTeam page: a fixtures table and a results table.
    private const string Page = """
        <html><body>
        <div class="fixtures-table"><table><tbody>
          <tr><td>L</td><td>08/11/26 10:00</td><td class="home-team">Thame United Black U10</td><td>v</td><td class="road-team">Rivals &amp; Co U10</td></tr>
          <tr class="fa-county-cup-fixture-row"><td>R1</td><td>15/11/26 10:00</td><td class="home-team">Faraway FC U10</td><td>v</td><td class="road-team">Thame United Black U10</td></tr>
          <tr><td>Cup</td><td>22/11/26 09:30</td><td class="home-team">Thame United Red U10</td><td>v</td><td class="road-team">Thame United Black U10</td></tr>
          <tr><td>XYZ</td><td>29/11/26 10:00</td><td class="home-team">Thame United Black U10</td><td>v</td><td class="road-team">Mystery FC</td></tr>
        </tbody></table></div>
        <div class="results-table"><table><tbody>
          <tr><td>L</td><td>01/11/26 10:00</td><td class="home-team">Thame United Black U10</td><td class="score"><a>P - P</a></td><td class="road-team">Postponed Town</td></tr>
          <tr><td>L</td><td>25/10/26 10:00</td><td class="home-team">Played FC</td><td class="score"><a>3 - 1</a></td><td class="road-team">Thame United Black U10</td></tr>
        </tbody></table></div>
        </body></html>
        """;

    [Fact]
    public void Reads_each_fixture_row()
    {
        var fixtures = FullTimeParser.ParseFixtures(Page, Black);

        Assert.Equal(5, fixtures.Count); // 4 fixtures + 1 postponed; the played result is ignored
        var first = fixtures[0];
        Assert.Equal(new DateOnly(2026, 11, 8), first.Date);   // dd/MM/yy, UK format
        Assert.True(first.IsHome);
        Assert.Equal("Rivals & Co U10", first.Opponent);       // HTML entities decoded
        Assert.Equal(FixtureType.League, first.FixtureType);
    }

    [Fact]
    public void Away_fixtures_use_the_home_side_as_opponent()
    {
        var cup = FullTimeParser.ParseFixtures(Page, Black)[1];
        Assert.False(cup.IsHome);
        Assert.Equal("Faraway FC U10", cup.Opponent);
    }

    [Fact]
    public void County_cup_rows_are_recognised_by_their_class()
    {
        Assert.Equal(FixtureType.CountyCup, FullTimeParser.ParseFixtures(Page, Black)[1].FixtureType);
    }

    [Fact]
    public void Thame_v_Thame_uses_the_last_word_of_our_team_name()
    {
        var derby = FullTimeParser.ParseFixtures(Page, Black)[2];
        Assert.False(derby.IsHome);                          // we're "Black", the away side
        Assert.Equal("Thame United Red U10", derby.Opponent);
        Assert.Equal(FixtureType.Cup, derby.FixtureType);

        var asRed = FullTimeParser.ParseFixtures(Page, new TeamDto { Id = Guid.NewGuid(), Name = "Thame United Red" })[2];
        Assert.True(asRed.IsHome);
        Assert.Equal("Thame United Black U10", asRed.Opponent);
    }

    [Fact]
    public void Unknown_type_codes_become_Other()
    {
        Assert.Equal(FixtureType.Other, FullTimeParser.ParseFixtures(Page, Black)[3].FixtureType);
    }

    [Fact]
    public void Postponed_results_are_included_but_played_results_are_not()
    {
        var postponed = FullTimeParser.ParseFixtures(Page, Black).Single(f => f.FixtureType == FixtureType.Postponed);
        Assert.Equal(new DateOnly(2026, 11, 1), postponed.Date);
        Assert.Equal("Postponed Town", postponed.Opponent);
        Assert.True(postponed.IsHome);
    }

    [Fact]
    public void A_page_without_tables_gives_no_fixtures()
    {
        Assert.Empty(FullTimeParser.ParseFixtures("<html><body>Nothing here</body></html>", Black));
    }

    [Fact]
    public void A_malformed_row_throws_a_FormatException()
    {
        const string broken = """<div class="fixtures-table"><table><tbody><tr><td>L</td><td>soon</td></tr></tbody></table></div>""";
        Assert.Throws<FormatException>(() => FullTimeParser.ParseFixtures(broken, Black));
    }

    [Theory]
    [InlineData("F", FixtureType.Friendly)]
    [InlineData("Cup", FixtureType.Cup)]
    [InlineData("CC", FixtureType.CountyCup)]
    [InlineData("L", FixtureType.League)]
    [InlineData("ONE", FixtureType.League)]
    [InlineData("TWO", FixtureType.League)]
    [InlineData("HL2N", FixtureType.League)]
    [InlineData("D3N", FixtureType.League)]
    [InlineData("VETP", FixtureType.League)]
    [InlineData("N", FixtureType.League)]
    [InlineData("?", FixtureType.Other)]
    public void MapFixtureType_codes(string code, FixtureType expected)
    {
        Assert.Equal(expected, FullTimeParser.MapFixtureType(code));
    }

    [Fact]
    public void TeamPageUrl_uses_both_ids()
    {
        Assert.Equal("https://fulltime.thefa.com/displayTeam.html?divisionseason=111&teamID=222", FullTimeParser.TeamPageUrl(Black));
    }
}
