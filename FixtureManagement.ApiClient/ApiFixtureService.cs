using System.Net.Http.Json;
using FixtureManagement.Shared.Dtos;
using FixtureManagement.Shared.Services;

namespace FixtureManagement.ApiClient;

/// <summary>IFixtureService over /api/fixtures.</summary>
public sealed class ApiFixtureService(HttpClient http) : IFixtureService
{
    public async Task<IReadOnlyList<FixtureDto>> GetFixturesAsync(FixtureQuery? query = null, CancellationToken cancellationToken = default)
    {
        var qs = QueryString.From(
            ("from", QueryString.Date(query?.From)),
            ("to", QueryString.Date(query?.To)),
            ("teamId", query?.TeamId?.ToString()));
        using var response = await http.GetAsync($"api/fixtures{qs}", cancellationToken);
        return await ApiResponses.ReadAsync<List<FixtureDto>>(response, cancellationToken);
    }

    public async Task<FixtureDto?> GetFixtureAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync($"api/fixtures/{id}", cancellationToken);
        return await ApiResponses.ReadOrNullAsync<FixtureDto>(response, cancellationToken);
    }

    public async Task<ServiceResult<FixtureDto>> CreateFixtureAsync(FixtureDto fixture, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync("api/fixtures", fixture, ApiResponses.Json, cancellationToken);
        return await ApiResponses.ToResultAsync<FixtureDto>(response, cancellationToken);
    }

    public async Task<ServiceResult<IReadOnlyList<FixtureDto>>> CreateFixturesAsync(IReadOnlyList<FixtureDto> fixtures, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync("api/fixtures/batch", fixtures, ApiResponses.Json, cancellationToken);
        var result = await ApiResponses.ToResultAsync<List<FixtureDto>>(response, cancellationToken);
        if (result.Value is null) return ServiceResult<IReadOnlyList<FixtureDto>>.From(result);
        return ServiceResult<IReadOnlyList<FixtureDto>>.Success(result.Value);
    }

    public async Task<ServiceResult> UpdateFixtureAsync(Guid id, FixtureDto fixture, CancellationToken cancellationToken = default)
    {
        using var response = await http.PutAsJsonAsync($"api/fixtures/{id}", fixture, ApiResponses.Json, cancellationToken);
        return await ApiResponses.ToResultAsync(response, cancellationToken);
    }

    public async Task<bool> DeleteFixtureAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await http.DeleteAsync($"api/fixtures/{id}", cancellationToken);
        return await ApiResponses.SucceededOrNotFoundAsync(response, cancellationToken);
    }

    public async Task<int> DeleteFixturesAsync(Guid? teamId, DateOnly? after, CancellationToken cancellationToken = default)
    {
        // Same guard as the EF service, so a missing filter fails here rather than as a 400 from the server.
        if (teamId is null && after is null)
            throw new ArgumentException("Give a team and/or a date, so this can't delete every fixture.");

        var qs = QueryString.From(("teamId", teamId?.ToString()), ("after", QueryString.Date(after)));
        using var response = await http.DeleteAsync($"api/fixtures{qs}", cancellationToken);
        return (await ApiResponses.ReadAsync<DeletedCount>(response, cancellationToken)).Deleted;
    }

    public async Task<ServiceResult<FixtureAllocationDto>> SetAllocationAsync(Guid fixtureId, FixtureAllocationDto allocation, CancellationToken cancellationToken = default)
    {
        using var response = await http.PutAsJsonAsync($"api/fixtures/{fixtureId}/allocation", allocation, ApiResponses.Json, cancellationToken);
        return await ApiResponses.ToResultAsync<FixtureAllocationDto>(response, cancellationToken);
    }

    public async Task<bool> RemoveAllocationAsync(Guid fixtureId, CancellationToken cancellationToken = default)
    {
        using var response = await http.DeleteAsync($"api/fixtures/{fixtureId}/allocation", cancellationToken);
        return await ApiResponses.SucceededOrNotFoundAsync(response, cancellationToken);
    }

    public async Task<int> ConfirmAllocationsAsync(IReadOnlyCollection<Guid> fixtureIds, CancellationToken cancellationToken = default)
    {
        if (fixtureIds.Count == 0) return 0;
        using var response = await http.PostAsJsonAsync("api/fixtures/allocations/confirm", fixtureIds, ApiResponses.Json, cancellationToken);
        return (await ApiResponses.ReadAsync<ConfirmedCount>(response, cancellationToken)).Confirmed;
    }

    private sealed record DeletedCount(int Deleted);
    private sealed record ConfirmedCount(int Confirmed);
}
