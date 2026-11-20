using FixtureManagement.Server.Models;
using FixtureManagement.Server.Services;
using FixtureManagement.Shared.Reconciliation;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace FixtureManagement.Tests.Server;

public sealed class EfReconciliationServiceTests : IDisposable
{
    private const string Source = "FullTime";
    private static readonly DateOnly Today = new(2026, 11, 20);
    private static readonly DateOnly Nov29 = new(2026, 11, 29);
    private static readonly DateOnly Dec6 = new(2026, 12, 6);
    private static readonly DateOnly Dec13 = new(2026, 12, 13);

    private readonly SqliteTestDb _db = new();
    private readonly EfFixtureService _fixtures;
    private readonly EfSourceUploadService _uploads;
    private readonly EfReconciliationService _service;
    private readonly FixedTime _time = new(new DateTimeOffset(Today.ToDateTime(new TimeOnly(9, 0)), TimeSpan.Zero));

    public EfReconciliationServiceTests()
    {
        _fixtures = new EfFixtureService(_db);
        _uploads = new EfSourceUploadService(_db, _time);
        var http = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        http.HttpContext.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity([new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "me@example.com")], "test"));
        _service = new EfReconciliationService(_db, _uploads, new EfTeamService(_db), _fixtures, http, _time);
    }

    public void Dispose() => _db.Dispose();

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private async Task<Fixture> AddFixtureAsync(Team team, DateOnly date, string opponent, bool home = true, FixtureType type = FixtureType.League)
    {
        var fixture = new Fixture { Id = Guid.NewGuid(), TeamId = team.Id, Date = date, Opponent = opponent, IsHome = home, FixtureType = type };
        await _db.AddAsync(fixture);
        return fixture;
    }

    private static SourceFixture Src(Team team, DateOnly? date, string opponent, bool home = true, FixtureType type = FixtureType.League) =>
        new() { TeamId = team.Id, SourceTeamName = team.Name, Date = date, Opponent = opponent, IsHome = home, FixtureType = type };

    private Task UploadAsync(IEnumerable<Team> teams, params SourceFixture[] fixtures) =>
        _uploads.SaveUploadAsync(new SourceUploadDto
        {
            Source = Source, SourceName = "FullTime", UploadedBy = "extension@example.com",
            From = Today, To = DateOnly.MaxValue, TeamIds = teams.Select(t => t.Id).ToList(), Fixtures = fixtures,
        });

    private static Dictionary<string, Resolution> Choose(ReconciliationReviewDto review, Func<ReconciliationItem, Resolution> choice) =>
        review.Items.ToDictionary(i => i.Id, choice);

    [Fact]
    public async Task With_no_upload_there_is_nothing_to_review()
    {
        var review = await _service.GetReviewAsync(Source);

        Assert.Null(review.Upload);
        Assert.Empty(review.Items);
    }

    [Fact]
    public async Task The_review_compares_the_covered_teams_from_today()
    {
        var lions = await _db.AddTeamAsync("Lions");
        var other = await _db.AddTeamAsync("Not covered");
        await AddFixtureAsync(lions, Nov29, "Same");
        await AddFixtureAsync(lions, Dec6, "Moved");
        await AddFixtureAsync(lions, Dec13, "Dropped");
        await AddFixtureAsync(lions, new DateOnly(2026, 11, 15), "Already played");    // before today
        await AddFixtureAsync(other, Dec6, "Someone");                                  // team not in the upload
        await UploadAsync([lions], Src(lions, Nov29, "Same"), Src(lions, Dec13.AddDays(7), "Moved"), Src(lions, Dec6, "Newcomers", home: false));

        var review = await _service.GetReviewAsync(Source);

        Assert.Equal((1, 3, 1, "extension@example.com"), (review.Upload!.TeamCount, review.Upload.FixtureCount, review.SameCount, review.Upload.UploadedBy));
        Assert.Equal(Today, review.From);
        Assert.Equal([DifferenceKind.OnlyInSource, DifferenceKind.OnlyInApp, DifferenceKind.DateMoved],
            review.Items.Select(i => i.Kind));
        Assert.All(review.Items, i => Assert.Equal(lions.Id, i.Team!.Id));
    }

    [Fact]
    public async Task Using_the_source_adds_updates_and_deletes_until_everything_matches()
    {
        var lions = await _db.AddTeamAsync("Lions");
        var pitch = await _db.AddPitchAsync();
        var moved = await AddFixtureAsync(lions, Dec6, "Moved");
        await _fixtures.SetAllocationAsync(moved.Id, new FixtureAllocationDto { PitchId = pitch.Id, Start = new TimeOnly(10, 0), Duration = TimeSpan.FromHours(1) });
        await AddFixtureAsync(lions, Nov29, "Dropped");
        await UploadAsync([lions], Src(lions, Dec13, "Moved"), Src(lions, Nov29, "Newcomers"), Src(lions, Dec6, "Added", home: false));

        var review = await _service.GetReviewAsync(Source);
        var result = await _service.ApplyAsync(Source, Choose(review, _ => Resolution.UseSource));

        Assert.Equal((1, 2, 0, 0, 0), (result.Added, result.Updated, result.Deleted, result.Kept, result.Skipped));   // "Dropped" → "Newcomers" is an update
        Assert.Empty(result.Errors);
        var after = await _service.GetReviewAsync(Source);
        Assert.Empty(after.Items);
        Assert.Equal(3, after.SameCount);
        var movedNow = await _fixtures.GetFixtureAsync(moved.Id);
        Assert.Equal(Dec13, movedNow!.Date);
        Assert.Null(movedNow.Allocation);                          // its pitch was booked for the old date
    }

    [Fact]
    public async Task Only_in_the_app_and_use_source_deletes_it()
    {
        var lions = await _db.AddTeamAsync("Lions");
        var dropped = await AddFixtureAsync(lions, Nov29, "Dropped");
        await UploadAsync([lions], Src(lions, Dec13, "Someone else", home: false));

        var review = await _service.GetReviewAsync(Source);
        var onlyInApp = Assert.Single(review.Items, i => i.Kind == DifferenceKind.OnlyInApp);
        var result = await _service.ApplyAsync(Source, new Dictionary<string, Resolution> { [onlyInApp.Id] = Resolution.UseSource });

        Assert.Equal(1, result.Deleted);
        Assert.Null(await _fixtures.GetFixtureAsync(dropped.Id));
    }

    [Fact]
    public async Task Keep_app_is_remembered_until_the_difference_changes()
    {
        var lions = await _db.AddTeamAsync("Lions");
        var fixture = await AddFixtureAsync(lions, Dec6, "Our spelling");
        await UploadAsync([lions], Src(lions, Dec6, "Their spelling"));

        var review = await _service.GetReviewAsync(Source);
        var result = await _service.ApplyAsync(Source, Choose(review, _ => Resolution.KeepApp));

        Assert.Equal(1, result.Kept);
        Assert.Equal("Our spelling", (await _fixtures.GetFixtureAsync(fixture.Id))!.Opponent);
        var after = await _service.GetReviewAsync(Source);
        Assert.Empty(after.Items);
        Assert.Equal(1, after.KeptCount);
        await using (var db = _db.CreateDbContext())
        {
            var decision = await db.ReconciliationDecision.SingleAsync();
            Assert.StartsWith("FullTime|", decision.Key);
            Assert.Equal(("me@example.com", lions.Id), (decision.DecidedBy, decision.TeamId));
            Assert.Contains("Their spelling", decision.Description);
        }

        // FullTime changes it again: the difference is new, so it's shown again.
        await UploadAsync([lions], Src(lions, Dec6, "Their new spelling"));
        var changed = await _service.GetReviewAsync(Source);
        Assert.Single(changed.Items);
        Assert.Equal(0, changed.KeptCount);

        // Applying anything tidies the decision that no longer matches.
        await _service.ApplyAsync(Source, new Dictionary<string, Resolution>());
        await using (var db = _db.CreateDbContext())
            Assert.Equal(0, await db.ReconciliationDecision.CountAsync());
    }

    [Fact]
    public async Task Choices_for_differences_that_no_longer_exist_are_skipped()
    {
        var lions = await _db.AddTeamAsync("Lions");
        await AddFixtureAsync(lions, Dec6, "Rivals");
        await UploadAsync([lions], Src(lions, Dec6, "Newcomers"));
        var review = await _service.GetReviewAsync(Source);

        await UploadAsync([lions], Src(lions, Dec6, "Rivals"));     // fixed on FullTime meanwhile
        var result = await _service.ApplyAsync(Source, Choose(review, _ => Resolution.UseSource));

        Assert.Equal((0, 1), (result.Updated, result.Skipped));
    }

    [Fact]
    public async Task A_source_fixture_with_no_date_cant_be_added()
    {
        var lions = await _db.AddTeamAsync("Lions");
        await UploadAsync([lions], Src(lions, null, "Cup opponent", type: FixtureType.Cup));

        var review = await _service.GetReviewAsync(Source);
        var result = await _service.ApplyAsync(Source, Choose(review, _ => Resolution.UseSource));

        Assert.Equal(0, result.Added);
        Assert.Contains("without a date", Assert.Single(result.Errors));
    }
}
