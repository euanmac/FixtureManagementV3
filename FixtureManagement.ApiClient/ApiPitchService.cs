using System.Net.Http.Json;
using FixtureManagement.Shared.Dtos;
using FixtureManagement.Shared.Services;

namespace FixtureManagement.ApiClient;

/// <summary>IPitchService over /api/pitches (read-only).</summary>
public sealed class ApiPitchService(HttpClient http) : IPitchService
{
    public async Task<IReadOnlyList<PitchDto>> GetPitchesAsync(CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync("api/pitches", cancellationToken);
        return await ApiResponses.ReadAsync<List<PitchDto>>(response, cancellationToken);
    }

    public async Task<PitchDto?> GetPitchAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync($"api/pitches/{id}", cancellationToken);
        return await ApiResponses.ReadOrNullAsync<PitchDto>(response, cancellationToken);
    }
}
