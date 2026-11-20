using FixtureManagement.Shared.Reconciliation;

namespace FixtureManagement.Tests.Shared;

public class SourceUploadsTests
{
    private static readonly Guid Lions = Guid.NewGuid();
    private static readonly Guid Tigers = Guid.NewGuid();

    private static SourceUploadDto Upload(string source, DateTime at, DateOnly from, params (Guid Team, string Opponent)[] fixtures) => new()
    {
        Id = Guid.NewGuid(), Source = source, SourceName = source, UploadedAt = at, UploadedBy = $"{source}@example.com",
        From = from, To = DateOnly.MaxValue,
        TeamIds = fixtures.Select(f => f.Team).Distinct().ToList(),
        Fixtures = fixtures.Select(f => new SourceFixture { TeamId = f.Team, Opponent = f.Opponent }).ToList(),
    };

    [Fact]
    public void Nothing_to_combine_is_null()
    {
        Assert.Null(SourceUploads.Combine("FullTime", []));
    }

    [Fact]
    public void League_uploads_are_combined_with_each_fixture_keeping_its_uploads_stamp()
    {
        var girls = Upload("FullTime:OxGirls", new DateTime(2026, 11, 21), new DateOnly(2026, 11, 21), (Lions, "A"));
        var wycombe = Upload("FullTime:WycAndSouthBucksMinor", new DateTime(2026, 11, 20), new DateOnly(2026, 11, 20), (Tigers, "B"));

        var combined = SourceUploads.Combine("FullTime", [wycombe, girls])!;

        Assert.Equal(("FullTime", "FullTime", girls.UploadedAt), (combined.Source, combined.SourceName, combined.UploadedAt));   // newest part
        Assert.Equal([Lions, Tigers], combined.TeamIds);
        Assert.Equal(new DateOnly(2026, 11, 21), combined.From);       // the latest start, so nothing is compared before its fetch
        Assert.Equal([("A", "FullTime:OxGirls@example.com"), ("B", "FullTime:WycAndSouthBucksMinor@example.com")],
            combined.Fixtures.Select(f => (f.Opponent, f.UploadedBy)));
    }

    [Fact]
    public void A_team_in_two_uploads_takes_its_fixtures_from_the_newest()
    {
        var old = Upload("FullTime", new DateTime(2026, 11, 19), new DateOnly(2026, 11, 19), (Lions, "Old"), (Tigers, "Kept"));
        var fresh = Upload("FullTime:OxSenior", new DateTime(2026, 11, 22), new DateOnly(2026, 11, 22), (Lions, "New"));

        var combined = SourceUploads.Combine("FullTime", [old, fresh])!;

        Assert.Equal(["New", "Kept"], combined.Fixtures.Select(f => f.Opponent));
    }

    [Fact]
    public void Part_names_a_league_upload()
    {
        Assert.Equal("FullTime:OxGirls", SourceUploads.Part("FullTime", "OxGirls"));
    }
}
