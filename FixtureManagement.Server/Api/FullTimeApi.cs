using FixtureManagement.Shared;
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

        // The team pages from the plan, as fetched. Saved as one upload per league ("FullTime:OxGirls" …), each replacing
        // only that league's last upload – so fetching some leagues leaves the others' fixtures in place – covering the
        // teams whose pages could be read, from today on.
        group.MapPost("/upload", async (FullTimeUploadRequest request, ITeamService teams, ISourceUploadService uploads,
            ClaimsPrincipal user, TimeProvider time, CancellationToken ct) =>
        {
            if (request.Pages.Count == 0)
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["Pages"] = ["No pages were sent."] });

            var allTeams = await teams.GetTeamsAsync(ct);
            var read = FullTimeUploadReader.Read(request.Pages, allTeams);

            // Nothing usable: keep the previous uploads rather than replace them with empty ones.
            if (read.TeamIds.Count == 0)
                return Results.UnprocessableEntity(new FullTimeUploadResultDto { Failed = read.Failed, UnknownTypeCodes = read.UnknownTypeCodes });

            var leagueOf = allTeams.ToDictionary(t => t.Id, t => t.League);
            var today = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
            SourceUploadDto? saved = null;
            foreach (var league in read.TeamIds.GroupBy(id => leagueOf[id]))
            {
                var teamIds = league.ToHashSet();
                var fixtures = read.Fixtures.Where(f => teamIds.Contains(f.TeamId)).ToList();
                saved = await uploads.SaveUploadAsync(new SourceUploadDto
                {
                    Source = SourceUploads.Part(FullTimeUploadReader.Source, league.Key.ToString()),
                    SourceName = $"FullTime – {league.Key.DisplayName()}",
                    UploadedBy = user.Identity?.Name ?? "",
                    From = today,
                    To = DateOnly.MaxValue,     // everything FullTime lists from today on
                    TeamIds = teamIds.ToList(),
                    Fixtures = fixtures,
                }, ct);
            }

            return Results.Ok(new FullTimeUploadResultDto
            {
                UploadId = saved!.Id,
                Teams = read.TeamIds.Count,
                Fixtures = read.Fixtures.Count,
                Failed = read.Failed,
                UnknownTypeCodes = read.UnknownTypeCodes,
            });
        });
    }
}
