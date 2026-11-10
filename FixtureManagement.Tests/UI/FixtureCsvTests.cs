using System.Text;
using FixtureManagement.UI.Components;

namespace FixtureManagement.Tests.UI;

public class FixtureCsvTests
{
    private static readonly Guid TeamId = Guid.NewGuid();

    private static Task<FixtureCsv.Result> Read(string csv) => FixtureCsv.ReadAsync(new MemoryStream(Encoding.UTF8.GetBytes(csv)), TeamId);

    [Fact]
    public async Task Reads_each_row_as_a_league_fixture_for_the_team()
    {
        var result = await Read("""
            Opponent,Venue,Date
            Rivals FC,Home,2026-11-14
            Next Week FC,Away,2026-11-21
            """);

        Assert.True(result.Succeeded);
        Assert.Collection(result.Fixtures,
            f =>
            {
                Assert.Equal("Rivals FC", f.Opponent);
                Assert.True(f.IsHome);
                Assert.Equal(new DateOnly(2026, 11, 14), f.Date);
                Assert.Equal(FixtureType.League, f.FixtureType);
                Assert.Equal(TeamId, f.TeamId);
                Assert.NotEqual(Guid.Empty, f.Id);
            },
            f =>
            {
                Assert.Equal("Next Week FC", f.Opponent);
                Assert.False(f.IsHome);
            });
    }

    [Fact]
    public async Task Header_names_are_matched_loosely_and_values_trimmed()
    {
        var result = await Read("""
            opponent , VENUE,date
             Rivals FC ,h,14-Nov-2026
            """);

        var fixture = Assert.Single(result.Fixtures);
        Assert.Equal("Rivals FC", fixture.Opponent);
        Assert.True(fixture.IsHome);
        Assert.Equal(new DateOnly(2026, 11, 14), fixture.Date);
    }

    [Fact]
    public async Task Dates_are_read_as_before_the_shared_parser()
    {
        // The website always read with the invariant culture, so 11/14/2026 is 14 November.
        var fixture = Assert.Single((await Read("Opponent,Venue,Date\nRivals,Home,11/14/2026")).Fixtures);
        Assert.Equal(new DateOnly(2026, 11, 14), fixture.Date);
    }

    [Theory]
    [InlineData("Team,Where,When\nRivals,Home,2026-11-14", "header row")]
    [InlineData("Opponent,Venue,Date\nRivals,Home,not a date", "Line 2")]
    [InlineData("Opponent,Venue,Date\n", "no fixtures")]
    [InlineData("", "no fixtures")]
    public async Task Bad_files_give_a_message_instead_of_nothing(string csv, string messagePart)
    {
        var result = await Read(csv);

        Assert.False(result.Succeeded);
        Assert.Empty(result.Fixtures);
        Assert.Contains(messagePart, result.Error);
    }
}
