using System.Net.Http.Json;
using FixtureManagement.Shared.Dtos;
using FixtureManagement.Shared.Services;

namespace FixtureManagement.ApiClient;

/// <summary>IBookingService over /api/bookings.</summary>
public sealed class ApiBookingService(HttpClient http) : IBookingService
{
    public async Task<IReadOnlyList<BookingDto>> GetBookingsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync("api/bookings", cancellationToken);
        return await ApiResponses.ReadAsync<List<BookingDto>>(response, cancellationToken);
    }

    public async Task<BookingDto?> GetBookingAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync($"api/bookings/{id}", cancellationToken);
        return await ApiResponses.ReadOrNullAsync<BookingDto>(response, cancellationToken);
    }

    public async Task<ServiceResult<BookingDto>> CreateBookingAsync(BookingDto booking, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync("api/bookings", booking, ApiResponses.Json, cancellationToken);
        return await ApiResponses.ToResultAsync<BookingDto>(response, cancellationToken);
    }

    public async Task<ServiceResult> UpdateBookingAsync(Guid id, BookingDto booking, CancellationToken cancellationToken = default)
    {
        using var response = await http.PutAsJsonAsync($"api/bookings/{id}", booking, ApiResponses.Json, cancellationToken);
        return await ApiResponses.ToResultAsync(response, cancellationToken);
    }

    public async Task<bool> DeleteBookingAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await http.DeleteAsync($"api/bookings/{id}", cancellationToken);
        return await ApiResponses.SucceededOrNotFoundAsync(response, cancellationToken);
    }
}
