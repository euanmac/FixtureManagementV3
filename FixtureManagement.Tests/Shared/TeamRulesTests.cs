using FixtureManagement.Shared;
using FixtureManagement.Shared.Dtos;
using FixtureManagement.Shared.Models;

namespace FixtureManagement.Tests.Shared;

public class TeamRulesTests
{
    [Theory]
    [InlineData(AgeGroup.U10, Gender.Male, "U10 Boys")]
    [InlineData(AgeGroup.U14, Gender.Female, "U14 Girls")]
    [InlineData(AgeGroup.U8, Gender.Mixed, "U8 ")]
    [InlineData(AgeGroup.U18, Gender.Male, "U18 Men")]
    [InlineData(AgeGroup.O50, Gender.Male, "O50 Men")]
    [InlineData(AgeGroup.External, Gender.Female, "External Women")]
    [InlineData(AgeGroup.Adult, Gender.Male, "Men")]
    [InlineData(AgeGroup.Adult, Gender.Female, "Women")]
    [InlineData(AgeGroup.Adult, Gender.Mixed, "")]
    public void GroupDescription_depends_on_age_group_and_gender(AgeGroup ageGroup, Gender gender, string expected)
    {
        Assert.Equal(expected, TeamRules.GroupDescription(ageGroup, gender));
    }

    [Fact]
    public void DisplayName_prefixes_the_group_description()
    {
        Assert.Equal("U12 Girls - Thame United Lions", TeamRules.DisplayName("Thame United Lions", AgeGroup.U12, Gender.Female));
    }

    [Fact]
    public void DisplayShortName_strips_the_club_name()
    {
        Assert.Equal("U12 Girls -  Lions", TeamRules.DisplayShortName("Thame United Lions", AgeGroup.U12, Gender.Female));
    }

    [Fact]
    public void DisplayShortName_is_empty_when_name_is_null()
    {
        Assert.Equal("", TeamRules.DisplayShortName(null, AgeGroup.U12, Gender.Female));
    }

    [Theory]
    [InlineData(AgeGroup.U6, TeamSize._5v5)]
    [InlineData(AgeGroup.U8, TeamSize._5v5)]
    [InlineData(AgeGroup.U9, TeamSize._7v7)]
    [InlineData(AgeGroup.U10, TeamSize._7v7)]
    [InlineData(AgeGroup.U11, TeamSize._9v9)]
    [InlineData(AgeGroup.U12, TeamSize._9v9)]
    [InlineData(AgeGroup.U13, TeamSize._11v11)]
    [InlineData(AgeGroup.Adult, TeamSize._11v11)]
    public void TeamSize_follows_age_group(AgeGroup ageGroup, TeamSize expected)
    {
        Assert.Equal(expected, TeamRules.TeamSize(ageGroup));
    }

    [Theory]
    [InlineData(League.BucksGirls, "Crimson", DayOfWeek.Saturday)]
    [InlineData(League.SouthBucksMini, "OrangeRed", DayOfWeek.Sunday)]
    [InlineData(League.JPL, "MidnightBlue", DayOfWeek.Saturday)]
    [InlineData(League.SSM, "SteelBlue", DayOfWeek.Sunday)] // falls through to the defaults
    public void Colour_and_MatchDay_follow_league(League league, string colour, DayOfWeek matchDay)
    {
        Assert.Equal(colour, TeamRules.Colour(league));
        Assert.Equal(matchDay, TeamRules.MatchDay(league));
    }

    [Fact]
    public void Every_league_has_a_colour_and_rgb()
    {
        foreach (var league in Enum.GetValues<League>())
        {
            Assert.False(string.IsNullOrEmpty(TeamRules.Colour(league)));
            var (r, g, b) = TeamRules.ColourRGB(league);
            Assert.InRange(r, 0, 255);
            Assert.InRange(g, 0, 255);
            Assert.InRange(b, 0, 255);
        }
    }

    [Fact]
    public void FixtureURL_is_built_from_both_ids()
    {
        Assert.Equal(
            "https://fulltime.thefa.com/displayTeam.html?divisionseason=123&teamID=456",
            TeamRules.FixtureURL("123", "456"));
    }

    [Theory]
    [InlineData(null, "456")]
    [InlineData("123", null)]
    [InlineData("", "456")]
    [InlineData("123", "")]
    public void FixtureURL_is_empty_when_an_id_is_missing(string? leagueId, string? teamId)
    {
        Assert.Equal("", TeamRules.FixtureURL(leagueId, teamId));
    }

    [Theory]
    [InlineData("123", "456", true)]
    [InlineData(null, "456", false)]
    [InlineData("123", null, false)]
    [InlineData("123", "", false)]
    public void IsOnFullTime_needs_both_ids(string? leagueId, string? teamId, bool expected)
    {
        Assert.Equal(expected, TeamRules.IsOnFullTime(leagueId, teamId));
    }

    [Fact]
    public void IsOnFullTime_known_quirk_ignores_an_empty_league_id()
    {
        // The original code checks the team id twice instead of the league id (kept as-is, see CLAUDE.md).
        // If that's ever fixed, flip this assertion to False.
        Assert.True(TeamRules.IsOnFullTime("", "456"));
    }
}

public class EnumExtensionsTests
{
    [Fact]
    public void DisplayName_uses_the_Display_attribute()
    {
        Assert.Equal("Junior Premier League", League.JPL.DisplayName());
        Assert.Equal("7 v 7", TeamSize._7v7.DisplayName());
    }

    [Fact]
    public void DisplayName_falls_back_to_the_enum_name()
    {
        Assert.Equal("Female", Gender.Female.DisplayName());
    }
}

public class TeamDtoTests
{
    [Fact]
    public void Derived_properties_are_computed_from_the_fields()
    {
        var dto = new TeamDto
        {
            Name = "Thame United Lions",
            AgeGroup = AgeGroup.U9,
            Gender = Gender.Male,
            League = League.SouthBucksMini,
            FullTimeLeagueId = "123",
            FullTimeTeamId = "456",
        };

        Assert.Equal("U9 Boys", dto.GroupDescription);
        Assert.Equal("U9 Boys - Thame United Lions", dto.DisplayName);
        Assert.Equal("South Bucks Mini Soccer Conference", dto.LeagueName);
        Assert.Equal(TeamSize._7v7, dto.TeamSize);
        Assert.Equal("OrangeRed", dto.Colour);
        Assert.Equal(DayOfWeek.Sunday, dto.MatchDay);
        Assert.True(dto.IsOnFullTime);
        Assert.EndsWith("divisionseason=123&teamID=456", dto.FixtureURL);
    }

    [Fact]
    public void Contact_FullName_joins_first_and_last()
    {
        Assert.Equal("Sam Smith", new TeamContactSummaryDto { FirstName = "Sam", LastName = "Smith" }.FullName);
    }
}
