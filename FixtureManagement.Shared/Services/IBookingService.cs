using FixtureManagement.Shared.Dtos;

namespace FixtureManagement.Shared.Services;

/// <summary>Pitch bookings. Website: EF Core; desktop: HttpClient against /api/bookings.</summary>
public interface IBookingService
{
    /// <summary>All bookings, earliest start first.</summary>
    Task<IReadOnlyList<BookingDto>> GetBookingsAsync(CancellationToken cancellationToken = default);

    Task<BookingDto?> GetBookingAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Invalid if the pitch doesn't exist.</summary>
    Task<ServiceResult<BookingDto>> CreateBookingAsync(BookingDto booking, CancellationToken cancellationToken = default);

    /// <summary>NotFound if the booking doesn't exist; Invalid if the pitch doesn't exist.</summary>
    Task<ServiceResult> UpdateBookingAsync(Guid id, BookingDto booking, CancellationToken cancellationToken = default);

    /// <summary>Returns false if the booking doesn't exist.</summary>
    Task<bool> DeleteBookingAsync(Guid id, CancellationToken cancellationToken = default);
}
