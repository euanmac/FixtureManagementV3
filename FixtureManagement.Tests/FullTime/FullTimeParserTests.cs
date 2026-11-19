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
    public void MapFixtureType_codes_every_league_knows(string code, FixtureType expected)
    {
        Assert.Equal(expected, FullTimeParser.MapFixtureType(League.WycAndSouthBucksMinor, code));
        Assert.Equal(expected, FullTimeParser.MapFixtureType(League.OxOver50, code));
    }

    [Theory]
    [InlineData(League.ADL, "DIV1", FixtureType.League)]
    [InlineData(League.ADL, "DIV3", FixtureType.League)]
    [InlineData(League.OxGirls, "O", FixtureType.Other)]
    [InlineData(League.OxOver50, "50D1", FixtureType.League)]
    [InlineData(League.UHL, "PREM", FixtureType.League)]
    public void League_specific_codes(League league, string code, FixtureType expected)
    {
        Assert.Equal(expected, FullTimeParser.MapFixtureType(league, code));
        Assert.Equal(expected, FullTimeLeagues.FixtureTypeFor(league, code));
    }

    [Fact]
    public void A_code_only_means_something_in_its_own_league()
    {
        Assert.Null(FullTimeLeagues.FixtureTypeFor(League.WycAndSouthBucksMinor, "50D1"));
        Assert.Equal(FixtureType.Other, FullTimeParser.MapFixtureType(League.WycAndSouthBucksMinor, "50D1"));
    }

    [Fact]
    public void A_county_cup_tie_listed_twice_is_one_fixture()
    {
        // As FullTime shows it: once as a county cup row (no link), once under the league's name for it (with a link).
        const string page = """
            <div class="fixtures-table"><table><tbody>
              <tr class="fa-county-cup-fixture-row"><td>R1</td><td>06/12/26 10:00</td><td class="home-team">Thame United Black U10</td><td>v</td><td class="road-team">Longford Park FC U10</td></tr>
              <tr><td>XCC</td><td><a href="/displayFixture.html?id=31201701">06/12/26 10:00</a></td><td class="home-team">Thame United Black U10</td><td>v</td><td class="road-team">Longford Park FC U10</td></tr>
              <tr><td>L</td><td>06/12/26 10:00</td><td class="home-team">Others U10</td><td>v</td><td class="road-team">Thame United Black U10</td></tr>
            </tbody></table></div>
            """;

        var fixtures = FullTimeParser.ParseTeamPage(page, Black);

        Assert.Equal(2, fixtures.Count);                 // a different opponent on the same day is kept
        var cup = fixtures[0];
        Assert.Equal((FixtureType.CountyCup, "31201701", "CC"), (cup.FixtureType, cup.SourceFixtureId, cup.TypeCode));
    }

    // A team page with a TBC date and fixture links, as the upload reads it.
    private const string PageWithTbcAndLinks = """
        <html><body>
        <div class="fixtures-table"><table><tbody>
          <tr><td>L</td><td><a href="/displayFixture.html?id=12345">08/11/26 10:00</a></td><td class="home-team">Thame United Black U10</td><td>v</td><td class="road-team">Rivals U10</td></tr>
          <tr class="fa-county-cup-fixture-row"><td>R2</td><td>TBC</td><td class="home-team">Faraway FC U10</td><td>v</td><td class="road-team">Thame United Black U10</td><td><a href="/displayCountyFixture.html?league=9&amp;id=67890">details</a></td></tr>
        </tbody></table></div>
        <div class="results-table"><table><tbody>
          <tr><td>L</td><td>01/11/26 10:00</td><td class="home-team">Thame United Black U10</td><td class="score"><a>P - P</a></td><td class="road-team">Postponed Town</td></tr>
        </tbody></table></div>
        </body></html>
        """;

    [Fact]
    public void ParseTeamPage_reads_rows_as_upload_fixtures_for_the_team()
    {
        var fixtures = FullTimeParser.ParseTeamPage(PageWithTbcAndLinks, Black);

        Assert.Equal(3, fixtures.Count);
        Assert.All(fixtures, f => Assert.Equal(Black.Id, f.TeamId));
        var league = fixtures[0];
        Assert.Equal((new DateOnly(2026, 11, 8), true, "Rivals U10", "Thame United Black U10", FixtureType.League, "12345", "", "L"),
            (league.Date, league.IsHome, league.Opponent, league.SourceTeamName, league.FixtureType, league.SourceFixtureId, league.Status, league.TypeCode));
    }

    [Fact]
    public void ParseTeamPage_keeps_a_TBC_fixture_with_no_date()
    {
        var cup = FullTimeParser.ParseTeamPage(PageWithTbcAndLinks, Black)[1];

        Assert.Null(cup.Date);
        Assert.False(cup.IsHome);
        Assert.Equal(FixtureType.CountyCup, cup.FixtureType);
        Assert.Equal("67890", cup.SourceFixtureId);       // county cup link, &amp; decoded
    }

    [Fact]
    public void ParseTeamPage_marks_postponed_games()
    {
        var postponed = FullTimeParser.ParseTeamPage(PageWithTbcAndLinks, Black)[2];

        Assert.Equal((FixtureType.Postponed, "Postponed", (string?)null), (postponed.FixtureType, postponed.Status, postponed.SourceFixtureId));
    }

    [Fact]
    public void A_TBC_date_no_longer_stops_Gameweek_reading_the_team()
    {
        var fixtures = FullTimeParser.ParseFixtures(PageWithTbcAndLinks, Black);

        Assert.Equal([new DateOnly(2026, 11, 8), new DateOnly(2026, 11, 1)], fixtures.Select(f => f.Date));   // the TBC row is skipped
    }

    [Fact]
    public void TeamPageUrl_uses_both_ids()
    {
        Assert.Equal("https://fulltime.thefa.com/displayTeam.html?divisionseason=111&teamID=222", FullTimeParser.TeamPageUrl(Black));
    }
}
