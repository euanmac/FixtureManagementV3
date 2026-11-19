using FixtureManagement.Shared.Reconciliation;
using FixtureManagement.Shared.Services;

public static class FullTimeApi
{
    /// <summary>The club's name on FullTime when appsettings has no "ClubName".</summary>
    public const string DefaultClubName = "Thame United";

    /// <summary>/api/fulltime – what the browser extension fetches from FullTime (and, later, where it sends it).</summary>
    public static void MapFullTimeEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/fulltime")
            .WithTags("FullTime")
            .RequireAuthorization();

        group.MapGet("/plan", async (ITeamService teams, IConfiguration configuration, CancellationToken ct) =>
            Results.Ok(FullTimePlan.Build(await teams.GetTeamsAsync(ct), configuration["ClubName"] ?? DefaultClubName)));
    }
}
