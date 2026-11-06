using FixtureManagement.Server.Services;

namespace FixtureManagement.Tests.Server;

public sealed class EfBookingServiceTests : IDisposable
{
    private readonly SqliteTestDb _db = new();
    private readonly EfBookingService _service;

    public EfBookingServiceTests() => _service = new EfBookingService(_db);

    public void Dispose() => _db.Dispose();

    private static BookingEditDto NewBooking(Guid pitchId, DateTime start, string title = "Training") => new()
    {
        Title = title,
        Start = start,
        End = start.AddHours(1),
        PitchId = pitchId,
    };

    private static readonly DateTime Sat = new(2026, 11, 7, 9, 0, 0);

    [Fact]
    public async Task Create_then_Get_includes_the_pitch_name()
    {
        var pitch = await _db.AddPitchAsync("Main Pitch");
        var edit = NewBooking(pitch.Id, Sat);
        edit.IsRecurring = true;

        var result = await _service.CreateBookingAsync(edit);
        var loaded = await _service.GetBookingAsync(result.Value!.Id);

        Assert.True(result.Succeeded);
        Assert.Equal("Main Pitch", result.Value.PitchName);
        Assert.Equal("Training", loaded!.Title);
        Assert.Equal(Sat, loaded.Start);
        Assert.Equal(Sat.AddHours(1), loaded.End);
        Assert.True(loaded.IsRecurring);
        Assert.Equal("Main Pitch", loaded.PitchName);
    }

    [Fact]
    public async Task Create_and_Update_check_the_pitch_exists()
    {
        var pitch = await _db.AddPitchAsync();
        var created = await _service.CreateBookingAsync(NewBooking(pitch.Id, Sat));

        var badCreate = await _service.CreateBookingAsync(NewBooking(Guid.NewGuid(), Sat));
        var badUpdate = await _service.UpdateBookingAsync(created.Value!.Id, NewBooking(Guid.NewGuid(), Sat));

        Assert.Equal(ServiceStatus.Invalid, badCreate.Status);
        Assert.Contains(nameof(BookingEditDto.PitchId), badCreate.Errors.Keys);
        Assert.Equal(ServiceStatus.Invalid, badUpdate.Status);
    }

    [Fact]
    public async Task GetBookings_orders_by_start()
    {
        var pitch = await _db.AddPitchAsync();
        await _service.CreateBookingAsync(NewBooking(pitch.Id, Sat.AddDays(1), "Sunday"));
        await _service.CreateBookingAsync(NewBooking(pitch.Id, Sat.AddHours(2), "Late Saturday"));
        await _service.CreateBookingAsync(NewBooking(pitch.Id, Sat, "Early Saturday"));

        Assert.Equal(["Early Saturday", "Late Saturday", "Sunday"], (await _service.GetBookingsAsync()).Select(b => b.Title));
    }

    [Fact]
    public async Task Update_and_Delete()
    {
        var pitch = await _db.AddPitchAsync();
        var id = (await _service.CreateBookingAsync(NewBooking(pitch.Id, Sat))).Value!.Id;

        Assert.True((await _service.UpdateBookingAsync(id, NewBooking(pitch.Id, Sat.AddDays(1), "Moved"))).Succeeded);
        Assert.Equal("Moved", (await _service.GetBookingAsync(id))!.Title);
        Assert.Equal(ServiceStatus.NotFound, (await _service.UpdateBookingAsync(Guid.NewGuid(), NewBooking(pitch.Id, Sat))).Status);

        Assert.True(await _service.DeleteBookingAsync(id));
        Assert.False(await _service.DeleteBookingAsync(id));
        Assert.Null(await _service.GetBookingAsync(id));
    }
}

public sealed class EfPitchServiceTests : IDisposable
{
    private readonly SqliteTestDb _db = new();
    private readonly EfPitchService _service;

    public EfPitchServiceTests() => _service = new EfPitchService(_db);

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task GetPitches_orders_by_display_order()
    {
        await _db.AddPitchAsync("Third", 3);
        await _db.AddPitchAsync("First", 1);
        await _db.AddPitchAsync("Second", 2);

        Assert.Equal(["First", "Second", "Third"], (await _service.GetPitchesAsync()).Select(p => p.Name));
    }

    [Fact]
    public async Task GetPitch_by_id()
    {
        var pitch = await _db.AddPitchAsync("Main", 5);

        var loaded = await _service.GetPitchAsync(pitch.Id);

        Assert.Equal(new PitchDto { Id = pitch.Id, Name = "Main", DisplayOrder = 5 }, loaded);
        Assert.Null(await _service.GetPitchAsync(Guid.NewGuid()));
    }
}
