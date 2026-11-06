using FixtureManagement.Shared.Services;

public static class PitchesApi
{
    /// <summary>/api/pitches – read-only for now.</summary>
    public static void MapPitchEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/pitches")
            .WithTags("Pitches")
            .RequireAuthorization();

        group.MapGet("/", async (IPitchService pitches, CancellationToken ct) =>
            Results.Ok(await pitches.GetPitchesAsync(ct)));

        group.MapGet("/{id:guid}", async (Guid id, IPitchService pitches, CancellationToken ct) =>
            await pitches.GetPitchAsync(id, ct) is { } pitch ? Results.Ok(pitch) : Results.NotFound());
    }
}
