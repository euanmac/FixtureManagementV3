using FixtureManagement.Server.Services;
using FixtureManagement.Shared.Reconciliation;
using Microsoft.EntityFrameworkCore;

namespace FixtureManagement.Tests.Server;

public sealed class EfSourceUploadServiceTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 11, 19, 18, 30, 0, TimeSpan.Zero);

    private readonly SqliteTestDb _db = new();
    private readonly EfSourceUploadService _service;

    public EfSourceUploadServiceTests() => _service = new EfSourceUploadService(_db, new FixedTime(Now));

    public void Dispose() => _db.Dispose();

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static SourceUploadDto Upload(string source, params SourceFixture[] fixtures) => new()
    {
        Source = source, SourceName = source, UploadedBy = "me@example.com",
        From = new DateOnly(2026, 11, 19), To = DateOnly.MaxValue,
        TeamIds = fixtures.Select(f => f.TeamId).Where(id => id != Guid.Empty).Distinct().ToList(),
        Fixtures = fixtures,
    };

    [Fact]
    public async Task Save_then_get_round_trips_everything()
    {
        var teamId = Guid.NewGuid();
        var fixture = new SourceFixture
        {
            TeamId = teamId, SourceTeamName = "Thame United Black", Date = new DateOnly(2026, 12, 6), IsHome = true,
            Opponent = "Rivals", FixtureType = FixtureType.Cup, Competition = "U13 League Cup", SourceFixtureId = "123", Status = "",
        };
        var tbcUnlinked = new SourceFixture { TeamId = Guid.Empty, SourceTeamName = "Thame United Pumas", Date = null, Opponent = "Others", Status = "Postponed" };

        var saved = await _service.SaveUploadAsync(Upload("FullTime", fixture, tbcUnlinked));
        var loaded = await _service.GetLatestUploadAsync("FullTime");

        Assert.NotEqual(Guid.Empty, saved.Id);
        Assert.NotNull(loaded);
        Assert.Equal((saved.Id, Now.UtcDateTime, DateTimeKind.Utc, "me@example.com"), (loaded.Id, loaded.UploadedAt, loaded.UploadedAt.Kind, loaded.UploadedBy));
        Assert.Equal((new DateOnly(2026, 11, 19), DateOnly.MaxValue), (loaded.From, loaded.To));
        Assert.Equal([teamId], loaded.TeamIds);
        // Records compare by value; TBC sorts last; each fixture carries its upload's time and uploader.
        Assert.Equal(
            [fixture with { UploadedAt = Now.UtcDateTime, UploadedBy = "me@example.com" },
             tbcUnlinked with { UploadedAt = Now.UtcDateTime, UploadedBy = "me@example.com" }],
            loaded.Fixtures);
    }

    [Fact]
    public async Task A_new_upload_replaces_the_last_one_from_the_same_source_only()
    {
        var team = Guid.NewGuid();
        await _service.SaveUploadAsync(Upload("FullTime", new SourceFixture { TeamId = team, Opponent = "Old" }));
        await _service.SaveUploadAsync(Upload("Csv", new SourceFixture { TeamId = team, Opponent = "From CSV" }));

        await _service.SaveUploadAsync(Upload("FullTime", new SourceFixture { TeamId = team, Opponent = "New" }));

        Assert.Equal("New", Assert.Single((await _service.GetLatestUploadAsync("FullTime"))!.Fixtures).Opponent);
        Assert.Equal("From CSV", Assert.Single((await _service.GetLatestUploadAsync("Csv"))!.Fixtures).Opponent);
        await using var db = _db.CreateDbContext();
        Assert.Equal(2, await db.SourceUpload.CountAsync());
        Assert.Equal(2, await db.UploadedFixture.CountAsync());     // the old FullTime fixtures went with their upload
    }

    [Fact]
    public async Task Get_returns_null_when_a_source_has_no_upload()
    {
        Assert.Null(await _service.GetLatestUploadAsync("FullTime"));
    }
}
