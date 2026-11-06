using FixtureManagement.Shared.Dtos;
using FixtureManagement.Shared.Services;

public static class PeopleApi
{
    /// <summary>/api/people – contacts and referees. Contains phone numbers and emails, so sign-in is required.</summary>
    public static void MapPeopleEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/people")
            .WithTags("People")
            .RequireAuthorization();

        group.MapGet("/", async (IPersonService people, CancellationToken ct) =>
            Results.Ok(await people.GetPeopleAsync(ct)));

        group.MapGet("/{id:guid}", async (Guid id, IPersonService people, CancellationToken ct) =>
            await people.GetPersonAsync(id, ct) is { } person ? Results.Ok(person) : Results.NotFound());

        group.MapPost("/", async (PersonDto person, IPersonService people, CancellationToken ct) =>
        {
            if (ApiResults.Validate(person) is { } problem) return problem;
            var created = await people.CreatePersonAsync(person, ct);
            return Results.Created($"/api/people/{created.Id}", created);
        });

        group.MapPut("/{id:guid}", async (Guid id, PersonDto person, IPersonService people, CancellationToken ct) =>
        {
            if (ApiResults.Validate(person) is { } problem) return problem;
            return await people.UpdatePersonAsync(id, person, ct) ? Results.NoContent() : Results.NotFound();
        });

        group.MapDelete("/{id:guid}", async (Guid id, IPersonService people, CancellationToken ct) =>
        {
            var result = await people.DeletePersonAsync(id, ct);
            return result.Succeeded ? Results.NoContent() : ApiResults.Failure(result);
        });
    }
}
