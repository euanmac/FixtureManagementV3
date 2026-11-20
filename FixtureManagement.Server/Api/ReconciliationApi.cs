using FixtureManagement.Shared.Dtos;
using FixtureManagement.Shared.Services;

public static class ReconciliationApi
{
    /// <summary>/api/reconciliation/{source} – a source's latest upload compared with the app's fixtures, and applying choices.</summary>
    public static void MapReconciliationEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/reconciliation")
            .WithTags("Reconciliation")
            .RequireAuthorization();

        group.MapGet("/{source}", async (string source, IReconciliationService reconciliation, CancellationToken ct) =>
            Results.Ok(await reconciliation.GetReviewAsync(source, ct)));

        group.MapGet("/{source}/week", async (string source, DateOnly from, DateOnly to, IReconciliationService reconciliation, CancellationToken ct) =>
            Results.Ok(await reconciliation.GetWeekAsync(source, from, to, ct)));

        group.MapPost("/{source}/apply", async (string source, ReconciliationApplyRequest request,
            IReconciliationService reconciliation, CancellationToken ct) =>
            Results.Ok(await reconciliation.ApplyAsync(source, request.Choices, ct)));
    }
}
