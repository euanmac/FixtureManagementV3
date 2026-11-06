using FixtureManagement.Shared.Dtos;
using FixtureManagement.Shared.Services;

public static class BookingsApi
{
    /// <summary>/api/bookings – pitch bookings. The calendar still reads them via /api/Event.</summary>
    public static void MapBookingEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/bookings")
            .WithTags("Bookings")
            .RequireAuthorization();

        group.MapGet("/", async (IBookingService bookings, CancellationToken ct) =>
            Results.Ok(await bookings.GetBookingsAsync(ct)));

        group.MapGet("/{id:guid}", async (Guid id, IBookingService bookings, CancellationToken ct) =>
            await bookings.GetBookingAsync(id, ct) is { } booking ? Results.Ok(booking) : Results.NotFound());

        group.MapPost("/", async (BookingEditDto booking, IBookingService bookings, CancellationToken ct) =>
        {
            if (ApiResults.Validate(booking) is { } problem) return problem;
            var result = await bookings.CreateBookingAsync(booking, ct);
            return result.Succeeded
                ? Results.Created($"/api/bookings/{result.Value!.Id}", result.Value)
                : ApiResults.Failure(result);
        });

        group.MapPut("/{id:guid}", async (Guid id, BookingEditDto booking, IBookingService bookings, CancellationToken ct) =>
        {
            if (ApiResults.Validate(booking) is { } problem) return problem;
            var result = await bookings.UpdateBookingAsync(id, booking, ct);
            return result.Succeeded ? Results.NoContent() : ApiResults.Failure(result);
        });

        group.MapDelete("/{id:guid}", async (Guid id, IBookingService bookings, CancellationToken ct) =>
            await bookings.DeleteBookingAsync(id, ct) ? Results.NoContent() : Results.NotFound());
    }
}
