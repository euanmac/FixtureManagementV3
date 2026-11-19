using System.Security.Claims;
using FixtureManagement.FullTime;
using FixtureManagement.Shared.Dtos;
using FixtureManagement.Shared.Reconciliation;
using FixtureManagement.Shared.Services;

public static class FullTimeApi
{
    /// <summary>The club's name on FullTime when appsettings has no "ClubName".</summary>
    public const string DefaultClubName = "Thame United";

    /// <summary>/api/fulltime – what the browser extension fetches from FullTime, and where it sends the pages.</summary>
    public static void MapFullTimeEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/fulltime")
            .WithTags("FullTime")
            .RequireAuthorization();

        group.MapGet("/plan", async (ITeamService teams, IConfiguration configuration, CancellationToken ct) =>
            Results.Ok(FullTimePlan.Build(await teams.GetTeamsAsync(ct), configuration["ClubName"] ?? DefaultClubName)));

        // The team pages from the plan, as fetched. Saved as the latest FullTime upload (replacing the last one),
        // covering the teams whose pages could be read, from today on.
        group.MapPost("/upload", async (FullTimeUploadRequest request, ITeamService teams, ISourceUploadService uploads,
            ClaimsPrincipal user, TimeProvider time, CancellationToken ct) =>
        {
            if (request.Pages.Count == 0)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["Pages"] = ["No pages were sent."] });

            var read = FullTimeUploadReader.Read(request.Pages, await teams.GetTeamsAsync(ct));

            // Nothing usable: keep the previous upload rather than replace it with an empty one.
            if (read.TeamIds.Count == 0)
                return Results.UnprocessableEntity(new FullTimeUploadResultDto { Failed = read.Failed, UnknownTypeCodes = read.UnknownTypeCodes });

            var saved = await uploads.SaveUploadAsync(new SourceUploadDto
            {
                Source = FullTimeUploadReader.Source,
                SourceName = "FullTime",
                UploadedBy = user.Identity?.Name ?? "",
                From = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime),
                To = DateOnly.MaxValue,     // everything FullTime lists from today on
                TeamIds = read.TeamIds,
                Fixtures = read.Fixtures,
            }, ct);

            return Results.Ok(new FullTimeUploadResultDto
            {
                UploadId = saved.Id,
                Teams = read.TeamIds.Count,
                Fixtures = read.Fixtures.Count,
                Failed = read.Failed,
                UnknownTypeCodes = read.UnknownTypeCodes,
            });
        });
    }
}
