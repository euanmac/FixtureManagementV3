using System.Net.Http.Json;
using FixtureManagement.Shared.Dtos;
using FixtureManagement.Shared.Services;

namespace FixtureManagement.ApiClient;

/// <summary>ITeamService over /api/teams.</summary>
public sealed class ApiTeamService(HttpClient http) : ITeamService
{
    public async Task<IReadOnlyList<TeamDto>> GetTeamsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync("api/teams", cancellationToken);
        return await ApiResponses.ReadAsync<List<TeamDto>>(response, cancellationToken);
    }

    public async Task<TeamDto?> GetTeamAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync($"api/teams/{id}", cancellationToken);
        return await ApiResponses.ReadOrNullAsync<TeamDto>(response, cancellationToken);
    }

    /// <summary>Throws ApiValidationException if the server rejects the data (the interface can't return errors).</summary>
    public async Task<TeamDto> CreateTeamAsync(TeamDto team, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync("api/teams", team, ApiResponses.Json, cancellationToken);
        return await ApiResponses.ReadAsync<TeamDto>(response, cancellationToken);
    }

    public async Task<bool> UpdateTeamAsync(Guid id, TeamDto team, CancellationToken cancellationToken = default)
    {
        using var response = await http.PutAsJsonAsync($"api/teams/{id}", team, ApiResponses.Json, cancellationToken);
        return await ApiResponses.SucceededOrNotFoundAsync(response, cancellationToken);
    }

    public async Task<bool> DeleteTeamAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await http.DeleteAsync($"api/teams/{id}", cancellationToken);
        return await ApiResponses.SucceededOrNotFoundAsync(response, cancellationToken);
    }
}
