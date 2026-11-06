using FixtureManagement.Server.Models;
using FixtureManagement.Server.Services;
using Microsoft.EntityFrameworkCore;

namespace FixtureManagement.Tests.Server;

public sealed class EfFixtureServiceTests : IDisposable
{
    private readonly SqliteTestDb _db = new();
    private readonly EfFixtureService _service;

    public EfFixtureServiceTests() => _service = new EfFixtureService(_db);

    public void Dispose() => _db.Dispose();

    private static FixtureDto NewFixture(Guid teamId, DateOnly date, string opponent = "Rivals", bool isHome = true) => new()
    {
        TeamId = teamId,
        Date = date,
        IsHome = isHome,
        Opponent = opponent,
        FixtureType = FixtureType.League,
    };

    private static FixtureAllocationDto NewAllocation(Guid pitchId, int hour = 9) => new()
    {
        PitchId = pitchId,
        Start = new TimeOnly(hour, 30),
        Duration = TimeSpan.FromMinutes(90),
    };

    private static readonly DateOnly Nov7 = new(2026, 11, 7);

    [Fact]
    public async Task Create_then_Get_returns_the_fixture_with_its_team()
    {
        var team = await _db.AddTeamAsync("Lions", AgeGroup.U12, Gender.Female);

        var result = await _service.CreateFixtureAsync(NewFixture(team.Id, Nov7, "Rivals"));
        var loaded = await _service.GetFixtureAsync(result.Value!.Id);

        Assert.True(result.Succeeded);
        Assert.NotNull(loaded);
        Assert.Equal("Rivals", loaded.Opponent);
        Assert.Equal(Nov7, loaded.Date);
        Assert.Equal(FixtureType.League, loaded.FixtureType);
        Assert.Equal("U12 Girls - Lions", loaded.Team.DisplayName);
        Assert.Equal("U12 Girls - Lions", result.Value.Team.DisplayName); // filled in on create too
        Assert.Null(loaded.Allocation);
    }

    [Fact]
    public async Task Get_returns_the_TeamId()
    {
        var team = await _db.AddTeamAsync();
        var id = (await _service.CreateFixtureAsync(NewFixture(team.Id, Nov7))).Value!.Id;

        Assert.Equal(team.Id, (await _service.GetFixtureAsync(id))!.TeamId);
    }

    [Fact]
    public async Task Writes_ignore_the_output_only_fields()
    {
        var team = await _db.AddTeamAsync();
        var pitch = await _db.AddPitchAsync();
        var sneaky = NewFixture(team.Id, Nov7);
        var fakeId = Guid.NewGuid();
        sneaky.Id = fakeId;
        sneaky.Team = new TeamSummaryDto { Id = Guid.NewGuid(), Name = "Not this team" };
        sneaky.Allocation = new FixtureAllocationDto { PitchId = pitch.Id, Start = new TimeOnly(9, 30), Duration = TimeSpan.FromHours(1) };

        var created = (await _service.CreateFixtureAsync(sneaky)).Value!;
        Assert.True((await _service.UpdateFixtureAsync(created.Id, sneaky)).Succeeded);

        var loaded = await _service.GetFixtureAsync(created.Id);
        Assert.NotEqual(fakeId, created.Id);           // Id is always generated
        Assert.Equal("Lions", loaded!.Team.Name);       // Team comes from TeamId, not the posted summary
        Assert.Null(loaded.Allocation);                 // allocations only change via SetAllocationAsync
        await using var db = _db.CreateDbContext();
        Assert.Equal(0, await db.FixtureAllocation.CountAsync());
    }

    [Fact]
    public async Task Create_with_an_unknown_team_is_invalid()
    {
        var result = await _service.CreateFixtureAsync(NewFixture(Guid.NewGuid(), Nov7));

        Assert.Equal(ServiceStatus.Invalid, result.Status);
        Assert.Contains(nameof(FixtureDto.TeamId), result.Errors.Keys);
    }

    [Fact]
    public async Task Batch_create_saves_all_or_nothing()
    {
        var team = await _db.AddTeamAsync();

        var bad = await _service.CreateFixturesAsync([NewFixture(team.Id, Nov7), NewFixture(Guid.NewGuid(), Nov7)]);
        Assert.Equal(ServiceStatus.Invalid, bad.Status);
        Assert.Empty(await _service.GetFixturesAsync());

        var good = await _service.CreateFixturesAsync([NewFixture(team.Id, Nov7, "A"), NewFixture(team.Id, Nov7.AddDays(7), "B")]);
        Assert.True(good.Succeeded);
        Assert.Equal(2, good.Value!.Count);
        Assert.Equal(2, (await _service.GetFixturesAsync()).Count);
    }

    [Fact]
    public async Task GetFixtures_filters_by_inclusive_dates_and_team()
    {
        var lions = await _db.AddTeamAsync("Lions");
        var tigers = await _db.AddTeamAsync("Tigers");
        await _service.CreateFixturesAsync([
            NewFixture(lions.Id, Nov7.AddDays(-1), "Before"),
            NewFixture(lions.Id, Nov7, "Start"),
            NewFixture(tigers.Id, Nov7.AddDays(3), "Middle"),
            NewFixture(lions.Id, Nov7.AddDays(6), "End"),
            NewFixture(lions.Id, Nov7.AddDays(7), "After"),
        ]);

        var week = await _service.GetFixturesAsync(new FixtureQuery { From = Nov7, To = Nov7.AddDays(6) });
        Assert.Equal(["Start", "Middle", "End"], week.Select(f => f.Opponent));

        var lionsWeek = await _service.GetFixturesAsync(new FixtureQuery { From = Nov7, To = Nov7.AddDays(6), TeamId = lions.Id });
        Assert.Equal(["Start", "End"], lionsWeek.Select(f => f.Opponent));

        Assert.Equal(5, (await _service.GetFixturesAsync()).Count);
    }

    [Fact]
    public async Task GetFixtures_orders_by_date_then_age_group()
    {
        var older = await _db.AddTeamAsync("Older", AgeGroup.U14);
        var younger = await _db.AddTeamAsync("Younger", AgeGroup.U8);
        await _service.CreateFixturesAsync([
            NewFixture(older.Id, Nov7.AddDays(1), "C"),
            NewFixture(older.Id, Nov7, "B"),
            NewFixture(younger.Id, Nov7, "A"),
        ]);

        Assert.Equal(["A", "B", "C"], (await _service.GetFixturesAsync()).Select(f => f.Opponent));
    }

    [Fact]
    public async Task Update_changes_fields_and_checks_the_team()
    {
        var team = await _db.AddTeamAsync();
        var other = await _db.AddTeamAsync("Other");
        var id = (await _service.CreateFixtureAsync(NewFixture(team.Id, Nov7))).Value!.Id;

        var edit = NewFixture(other.Id, Nov7.AddDays(1), "New Opponent", isHome: false);
        edit.FixtureType = FixtureType.Postponed;
        Assert.True((await _service.UpdateFixtureAsync(id, edit)).Succeeded);

        var loaded = await _service.GetFixtureAsync(id);
        Assert.Equal("New Opponent", loaded!.Opponent);
        Assert.Equal(other.Id, loaded.TeamId);
        Assert.False(loaded.IsHome);
        Assert.False(loaded.CanAllocate);

        Assert.Equal(ServiceStatus.Invalid, (await _service.UpdateFixtureAsync(id, NewFixture(Guid.NewGuid(), Nov7))).Status);
        Assert.Equal(ServiceStatus.NotFound, (await _service.UpdateFixtureAsync(Guid.NewGuid(), edit)).Status);
    }

    [Fact]
    public async Task SetAllocation_creates_then_replaces_the_same_allocation()
    {
        var team = await _db.AddTeamAsync();
        var pitch1 = await _db.AddPitchAsync("Pitch 1");
        var pitch2 = await _db.AddPitchAsync("Pitch 2", 2);
        var id = (await _service.CreateFixtureAsync(NewFixture(team.Id, Nov7))).Value!.Id;

        var first = await _service.SetAllocationAsync(id, NewAllocation(pitch1.Id, 9));
        var second = await _service.SetAllocationAsync(id, NewAllocation(pitch2.Id, 11));

        Assert.True(first.Succeeded);
        Assert.Equal("Pitch 1", first.Value!.PitchName);
        Assert.Equal(first.Value.Id, second.Value!.Id);
        var loaded = await _service.GetFixtureAsync(id);
        Assert.True(loaded!.IsAllocated);
        Assert.Equal("Pitch 2", loaded.Allocation!.PitchName);
        Assert.Equal(new TimeOnly(11, 30), loaded.Allocation.Start);
        Assert.Equal(new TimeOnly(13, 0), loaded.Allocation.End);
        await using var db = _db.CreateDbContext();
        Assert.Equal(1, await db.FixtureAllocation.CountAsync());
    }

    [Fact]
    public async Task SetAllocation_checks_the_fixture_and_the_pitch()
    {
        var team = await _db.AddTeamAsync();
        var pitch = await _db.AddPitchAsync();
        var id = (await _service.CreateFixtureAsync(NewFixture(team.Id, Nov7))).Value!.Id;

        Assert.Equal(ServiceStatus.NotFound, (await _service.SetAllocationAsync(Guid.NewGuid(), NewAllocation(pitch.Id))).Status);
        var invalid = await _service.SetAllocationAsync(id, NewAllocation(Guid.NewGuid()));
        Assert.Equal(ServiceStatus.Invalid, invalid.Status);
        Assert.Contains(nameof(FixtureAllocationDto.PitchId), invalid.Errors.Keys);
    }

    [Fact]
    public async Task RemoveAllocation_unallocates_the_fixture()
    {
        var team = await _db.AddTeamAsync();
        var pitch = await _db.AddPitchAsync();
        var id = (await _service.CreateFixtureAsync(NewFixture(team.Id, Nov7))).Value!.Id;
        await _service.SetAllocationAsync(id, NewAllocation(pitch.Id));

        Assert.True(await _service.RemoveAllocationAsync(id));
        Assert.False(await _service.RemoveAllocationAsync(id));
        Assert.Null((await _service.GetFixtureAsync(id))!.Allocation);
    }

    [Fact]
    public async Task Delete_removes_the_fixture_and_its_allocation()
    {
        var team = await _db.AddTeamAsync();
        var pitch = await _db.AddPitchAsync();
        var id = (await _service.CreateFixtureAsync(NewFixture(team.Id, Nov7))).Value!.Id;
        await _service.SetAllocationAsync(id, NewAllocation(pitch.Id));

        Assert.True(await _service.DeleteFixtureAsync(id));
        Assert.False(await _service.DeleteFixtureAsync(id));
        await using var db = _db.CreateDbContext();
        Assert.Equal(0, await db.FixtureAllocation.CountAsync());
    }

    [Fact]
    public async Task DeleteFixtures_by_team_and_after_a_date()
    {
        var lions = await _db.AddTeamAsync("Lions");
        var tigers = await _db.AddTeamAsync("Tigers");
        await _service.CreateFixturesAsync([
            NewFixture(lions.Id, Nov7, "L1"), NewFixture(lions.Id, Nov7.AddDays(7), "L2"),
            NewFixture(tigers.Id, Nov7, "T1"), NewFixture(tigers.Id, Nov7.AddDays(7), "T2"),
        ]);

        // Team and date together: only Lions fixtures strictly after Nov 7.
        Assert.Equal(1, await _service.DeleteFixturesAsync(lions.Id, Nov7));
        Assert.Equal(["L1", "T1", "T2"], (await _service.GetFixturesAsync()).Select(f => f.Opponent).Order());

        // Date only: everything strictly after Nov 7 (Nov 7 itself is kept).
        Assert.Equal(1, await _service.DeleteFixturesAsync(null, Nov7));
        Assert.Equal(["L1", "T1"], (await _service.GetFixturesAsync()).Select(f => f.Opponent).Order());

        // Team only.
        Assert.Equal(1, await _service.DeleteFixturesAsync(tigers.Id, null));
        Assert.Equal(["L1"], (await _service.GetFixturesAsync()).Select(f => f.Opponent));
    }

    [Fact]
    public async Task DeleteFixtures_requires_a_filter()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.DeleteFixturesAsync(null, null));
    }
}
