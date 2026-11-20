using FixtureManagement.Shared.Dtos;
using FixtureManagement.Shared.Reconciliation;

namespace FixtureManagement.Shared.Services;

/// <summary>
/// Compares a source's latest upload (e.g. "FullTime") with the app's fixtures and applies the user's choices.
/// Website: EF Core; desktop: HttpClient against /api/reconciliation.
/// </summary>
public interface IReconciliationService
{
    Task<ReconciliationReviewDto> GetReviewAsync(string source, CancellationToken cancellationToken = default);

    /// <summary>
    /// For each choice (by ReconciliationItem.Id): UseSource adds, updates or deletes the app fixture to match the
    /// source (a fixture that moves, or stops being a home game, loses its pitch allocation); KeepApp remembers the
    /// difference so it isn't raised again. The review is worked out afresh first, so choices for differences that
    /// no longer exist are skipped.
    /// </summary>
    Task<ReconciliationApplyResultDto> ApplyAsync(string source, IReadOnlyDictionary<string, Resolution> choices,
        CancellationToken cancellationToken = default);
}
