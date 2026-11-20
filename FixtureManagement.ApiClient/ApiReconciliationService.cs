using System.Net.Http.Json;
using FixtureManagement.Shared.Dtos;
using FixtureManagement.Shared.Reconciliation;
using FixtureManagement.Shared.Services;

namespace FixtureManagement.ApiClient;

/// <summary>IReconciliationService over /api/reconciliation.</summary>
public sealed class ApiReconciliationService(HttpClient http) : IReconciliationService
{
    public async Task<ReconciliationReviewDto> GetReviewAsync(string source, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync($"api/reconciliation/{Uri.EscapeDataString(source)}", cancellationToken);
        return await ApiResponses.ReadAsync<ReconciliationReviewDto>(response, cancellationToken);
    }

    public async Task<ReconciliationWeekDto> GetWeekAsync(string source, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync(
            $"api/reconciliation/{Uri.EscapeDataString(source)}/week?from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}", cancellationToken);
        return await ApiResponses.ReadAsync<ReconciliationWeekDto>(response, cancellationToken);
    }

    public async Task<ReconciliationApplyResultDto> ApplyAsync(string source, IReadOnlyDictionary<string, Resolution> choices,
        CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync($"api/reconciliation/{Uri.EscapeDataString(source)}/apply",
            new ReconciliationApplyRequest { Choices = choices }, ApiResponses.Json, cancellationToken);
        return await ApiResponses.ReadAsync<ReconciliationApplyResultDto>(response, cancellationToken);
    }
}
