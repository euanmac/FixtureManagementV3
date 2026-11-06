using FixtureManagement.Shared.Dtos;
using FixtureManagement.Shared.Services;

public static class TeamsAPI
{
    public static void MapTeamEndpoints(this IEndpointRouteBuilder routes)
    {
        // All team endpoints need a signed-in user: the responses include contact names and emails.
        var group = routes.MapGroup("/api/teams")
            .WithTags("Teams")
            .RequireAuthorization();

        group.MapGet("/", async (ITeamService teams, CancellationToken ct) =>
            Results.Ok(await teams.GetTeamsAsync(ct)));

        group.MapGet("/{id:guid}", async (Guid id, ITeamService teams, CancellationToken ct) =>
            await teams.GetTeamAsync(id, ct) is { } team ? Results.Ok(team) : Results.NotFound());

        group.MapPost("/", async (TeamEditDto team, ITeamService teams, CancellationToken ct) =>
        {
            if (ApiResults.Validate(team) is { } problem) return problem;
            var created = await teams.CreateTeamAsync(team, ct);
            return Results.Created($"/api/teams/{created.Id}", created);
        });

        group.MapPut("/{id:guid}", async (Guid id, TeamEditDto team, ITeamService teams, CancellationToken ct) =>
        {
            if (ApiResults.Validate(team) is { } problem) return problem;
            return await teams.UpdateTeamAsync(id, team, ct) ? Results.NoContent() : Results.NotFound();
        });

        group.MapDelete("/{id:guid}", async (Guid id, ITeamService teams, CancellationToken ct) =>
            await teams.DeleteTeamAsync(id, ct) ? Results.NoContent() : Results.NotFound());
    }
}
