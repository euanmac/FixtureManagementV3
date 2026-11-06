using FixtureManagement.Shared.Dtos;
using FixtureManagement.Shared.Services;

public static class TeamContactsApi
{
    /// <summary>/api/teamcontacts – links between people and teams (add and remove only).</summary>
    public static void MapTeamContactEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/teamcontacts")
            .WithTags("Team contacts")
            .RequireAuthorization();

        // Optional filters: /api/teamcontacts?teamId=...&personId=...
        group.MapGet("/", async (Guid? teamId, Guid? personId, ITeamContactService contacts, CancellationToken ct) =>
            Results.Ok(await contacts.GetTeamContactsAsync(teamId, personId, ct)));

        group.MapGet("/{id:guid}", async (Guid id, ITeamContactService contacts, CancellationToken ct) =>
            await contacts.GetTeamContactAsync(id, ct) is { } contact ? Results.Ok(contact) : Results.NotFound());

        group.MapPost("/", async (TeamContactDto contact, ITeamContactService contacts, CancellationToken ct) =>
        {
            if (ApiResults.Validate(contact) is { } problem) return problem;
            var result = await contacts.CreateTeamContactAsync(contact, ct);
            return result.Succeeded
                ? Results.Created($"/api/teamcontacts/{result.Value!.Id}", result.Value)
                : ApiResults.Failure(result);
        });

        group.MapDelete("/{id:guid}", async (Guid id, ITeamContactService contacts, CancellationToken ct) =>
            await contacts.DeleteTeamContactAsync(id, ct) ? Results.NoContent() : Results.NotFound());
    }
}
