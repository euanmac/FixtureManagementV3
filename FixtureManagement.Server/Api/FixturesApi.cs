using FixtureManagement.Shared.Dtos;
using FixtureManagement.Shared.Services;

public static class FixturesApi
{
    /// <summary>
    /// /api/fixtures – fixtures and their pitch allocations.
    /// Replaces the old /api/Fixture endpoints, which returned EF entities and had no callers.
    /// The calendar still uses /api/Event.
    /// </summary>
    public static void MapFixtureEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/fixtures")
            .WithTags("Fixtures")
            .RequireAuthorization();

        // Optional filters: /api/fixtures?from=2026-11-01&to=2026-11-08&teamId=...
        group.MapGet("/", async (DateOnly? from, DateOnly? to, Guid? teamId, IFixtureService fixtures, CancellationToken ct) =>
            Results.Ok(await fixtures.GetFixturesAsync(new FixtureQuery { From = from, To = to, TeamId = teamId }, ct)));

        group.MapGet("/{id:guid}", async (Guid id, IFixtureService fixtures, CancellationToken ct) =>
            await fixtures.GetFixtureAsync(id, ct) is { } fixture ? Results.Ok(fixture) : Results.NotFound());

        group.MapPost("/", async (FixtureDto fixture, IFixtureService fixtures, CancellationToken ct) =>
        {
            if (ApiResults.Validate(fixture) is { } problem) return problem;
            var result = await fixtures.CreateFixtureAsync(fixture, ct);
            return result.Succeeded
                ? Results.Created($"/api/fixtures/{result.Value!.Id}", result.Value)
                : ApiResults.Failure(result);
        });

        // Several fixtures in one save, e.g. from a FullTime upload. All or nothing.
        group.MapPost("/batch", async (List<FixtureDto> batch, IFixtureService fixtures, CancellationToken ct) =>
        {
            if (ApiResults.ValidateAll(batch) is { } problem) return problem;
            var result = await fixtures.CreateFixturesAsync(batch, ct);
            return result.Succeeded ? Results.Created("/api/fixtures", result.Value) : ApiResults.Failure(result);
        });

        group.MapPut("/{id:guid}", async (Guid id, FixtureDto fixture, IFixtureService fixtures, CancellationToken ct) =>
        {
            if (ApiResults.Validate(fixture) is { } problem) return problem;
            var result = await fixtures.UpdateFixtureAsync(id, fixture, ct);
            return result.Succeeded ? Results.NoContent() : ApiResults.Failure(result);
        });

        group.MapDelete("/{id:guid}", async (Guid id, IFixtureService fixtures, CancellationToken ct) =>
            await fixtures.DeleteFixtureAsync(id, ct) ? Results.NoContent() : Results.NotFound());

        // Bulk delete: /api/fixtures?teamId=... and/or ?after=2026-12-31. A filter is required.
        group.MapDelete("/", async (Guid? teamId, DateOnly? after, IFixtureService fixtures, CancellationToken ct) =>
        {
            if (teamId is null && after is null)
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    [""] = ["Give teamId and/or after, so this can't delete every fixture."]
                });
            return Results.Ok(new { deleted = await fixtures.DeleteFixturesAsync(teamId, after, ct) });
        });

        group.MapPut("/{id:guid}/allocation", async (Guid id, FixtureAllocationDto allocation, IFixtureService fixtures, CancellationToken ct) =>
        {
            if (ApiResults.Validate(allocation) is { } problem) return problem;
            var result = await fixtures.SetAllocationAsync(id, allocation, ct);
            return result.Succeeded ? Results.Ok(result.Value) : ApiResults.Failure(result);
        });

        group.MapDelete("/{id:guid}/allocation", async (Guid id, IFixtureService fixtures, CancellationToken ct) =>
            await fixtures.RemoveAllocationAsync(id, ct) ? Results.NoContent() : Results.NotFound());

        // Confirm the allocations of several fixtures at once. Body: a JSON array of fixture ids.
        group.MapPost("/allocations/confirm", async (List<Guid> fixtureIds, IFixtureService fixtures, CancellationToken ct) =>
            Results.Ok(new { confirmed = await fixtures.ConfirmAllocationsAsync(fixtureIds, ct) }));
    }
}
