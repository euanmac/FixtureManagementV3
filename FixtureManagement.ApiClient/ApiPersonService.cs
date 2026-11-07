using System.Net.Http.Json;
using FixtureManagement.Shared.Dtos;
using FixtureManagement.Shared.Services;

namespace FixtureManagement.ApiClient;

/// <summary>IPersonService over /api/people.</summary>
public sealed class ApiPersonService(HttpClient http) : IPersonService
{
    public async Task<IReadOnlyList<PersonDto>> GetPeopleAsync(CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync("api/people", cancellationToken);
        return await ApiResponses.ReadAsync<List<PersonDto>>(response, cancellationToken);
    }

    public async Task<PersonDto?> GetPersonAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync($"api/people/{id}", cancellationToken);
        return await ApiResponses.ReadOrNullAsync<PersonDto>(response, cancellationToken);
    }

    /// <summary>Throws ApiValidationException if the server rejects the data (the interface can't return errors).</summary>
    public async Task<PersonDto> CreatePersonAsync(PersonDto person, CancellationToken cancellationToken = default)
    {
        using var response = await http.PostAsJsonAsync("api/people", person, ApiResponses.Json, cancellationToken);
        return await ApiResponses.ReadAsync<PersonDto>(response, cancellationToken);
    }

    public async Task<bool> UpdatePersonAsync(Guid id, PersonDto person, CancellationToken cancellationToken = default)
    {
        using var response = await http.PutAsJsonAsync($"api/people/{id}", person, ApiResponses.Json, cancellationToken);
        return await ApiResponses.SucceededOrNotFoundAsync(response, cancellationToken);
    }

    public async Task<ServiceResult> DeletePersonAsync(Guid id, CancellationToken cancellationToken = default)
    {
        using var response = await http.DeleteAsync($"api/people/{id}", cancellationToken);
        return await ApiResponses.ToResultAsync(response, cancellationToken);
    }
}
