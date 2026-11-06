using System.ComponentModel.DataAnnotations;
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
            if (Validate(team) is { } problem) return problem;
            var created = await teams.CreateTeamAsync(team, ct);
            return Results.Created($"/api/teams/{created.Id}", created);
        });

        group.MapPut("/{id:guid}", async (Guid id, TeamEditDto team, ITeamService teams, CancellationToken ct) =>
        {
            if (Validate(team) is { } problem) return problem;
            return await teams.UpdateTeamAsync(id, team, ct) ? Results.NoContent() : Results.NotFound();
        });

        group.MapDelete("/{id:guid}", async (Guid id, ITeamService teams, CancellationToken ct) =>
            await teams.DeleteTeamAsync(id, ct) ? Results.NoContent() : Results.NotFound());
    }

    // Minimal APIs in .NET 9 don't run DataAnnotations validation, so check the DTO here.
    private static IResult? Validate(object model)
    {
        var results = new List<ValidationResult>();
        if (Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true))
            return null;

        var errors = results
            .SelectMany(r => r.MemberNames.DefaultIfEmpty("").Select(m => (Member: m, r.ErrorMessage)))
            .GroupBy(e => e.Member)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage ?? "Invalid value.").ToArray());
        return Results.ValidationProblem(errors);
    }
}
