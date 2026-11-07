using System.Net.Http.Json;
using FixtureManagement.Shared.Dtos;
using FixtureManagement.Shared.Services;

namespace FixtureManagement.ApiClient;

/// <summary>ITeamContactService over /api/teamcontacts.</summary>
public sealed class ApiTeamContactService(HttpClient http) : ITeamContactService
{
    public async Task<IReadOnlyList<TeamContactDto>> GetTeamContactsAsync(Guid? teamId = null, Guid? personId = null, CancellationToken cancellationToken = default)
    {
        var query = QueryString.From(("teamId", teamId?.ToString()), ("personId", personId?.ToString()));
        using var response = await http.GetAsync($"api/teamcontacts{query}", cancellationToken);
        return await ApiResponses.ReadAsync<List<TeamContactDto>>(response, cancellationToken);
    }

    public async Task<TeamContactDto?> GetTeamContactAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync($"api/teamcontacts/{id}", cancellationToken);
        return await ApiResponses.ReadOrNullAsync<TeamContactDto>(response, cancellationToken);
    }

    public async Task<ServiceResult<TeamContactDto>> CreateTeamContactAsync(TeamContactDto contact, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync("api/teamcontacts", contact, ApiResponses.Json, cancellationToken);
        return await ApiResponses.ToResultAsync<TeamContactDto>(response, cancellationToken);
    }

    public async Task<bool> DeleteTeamContactAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await http.DeleteAsync($"api/teamcontacts/{id}", cancellationToken);
        return await ApiResponses.SucceededOrNotFoundAsync(response, cancellationToken);
    }
}
