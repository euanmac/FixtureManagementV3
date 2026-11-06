using FixtureManagement.Server.Models;
using FixtureManagement.Shared.Dtos;
using FixtureManagement.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace FixtureManagement.Server.Services;

/// <summary>IBookingService backed by the database.</summary>
public class EfBookingService(IDbContextFactory<AppDBContext> dbFactory) : IBookingService
{
    public async Task<IReadOnlyList<BookingDto>> GetBookingsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var bookings = await db.Booking.AsNoTracking().Include(b => b.Pitch)
            .OrderBy(b => b.Start)
            .ToListAsync(cancellationToken);
        return bookings.Select(b => b.ToDto()).ToList();
    }

    public async Task<BookingDto?> GetBookingAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var booking = await db.Booking.AsNoTracking().Include(b => b.Pitch).FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        return booking?.ToDto();
    }

    public async Task<ServiceResult<BookingDto>> CreateBookingAsync(BookingDto booking, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (await CheckPitch(db, booking.PitchId, cancellationToken) is { } invalid)
            return ServiceResult<BookingDto>.From(invalid);

        var entity = new Booking { Id = Guid.NewGuid() };
        Apply(booking, entity);
        db.Booking.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        await db.Entry(entity).Reference(b => b.Pitch).LoadAsync(cancellationToken);
        return ServiceResult<BookingDto>.Success(entity.ToDto());
    }

    public async Task<ServiceResult> UpdateBookingAsync(Guid id, BookingDto booking, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.Booking.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (entity is null) return ServiceResult.NotFound();
        if (await CheckPitch(db, booking.PitchId, cancellationToken) is { } invalid) return invalid;

        Apply(booking, entity);
        await db.SaveChangesAsync(cancellationToken);
        return ServiceResult.Success();
    }

    public async Task<bool> DeleteBookingAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.Booking.FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (entity is null) return false;
        db.Booking.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    // The real database has no foreign key from Booking to Pitch, so check it here.
    private static async Task<ServiceResult?> CheckPitch(AppDBContext db, Guid pitchId, CancellationToken ct) =>
        await db.Pitch.AnyAsync(p => p.Id == pitchId, ct)
            ? null
            : ServiceResult.Invalid(nameof(BookingDto.PitchId), "Pitch not found.");

    private static void Apply(BookingDto source, Booking target)
    {
        target.Title = source.Title;
        target.Start = source.Start;
        target.End = source.End;
        target.PitchId = source.PitchId;
        target.IsRecurring = source.IsRecurring;
    }
}
