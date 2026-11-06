using FixtureManagement.Shared.Dtos;

namespace FixtureManagement.Shared.Services;

/// <summary>People (team contacts and referees). Website: EF Core; desktop: HttpClient against /api/people.</summary>
public interface IPersonService
{
    /// <summary>Everyone, by last name then first name, with the teams they're a contact for.</summary>
    Task<IReadOnlyList<PersonDto>> GetPeopleAsync(CancellationToken cancellationToken = default);

    Task<PersonDto?> GetPersonAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PersonDto> CreatePersonAsync(PersonEditDto person, CancellationToken cancellationToken = default);

    /// <summary>Returns false if the person doesn't exist.</summary>
    Task<bool> UpdatePersonAsync(Guid id, PersonEditDto person, CancellationToken cancellationToken = default);

    /// <summary>
    /// Also removes the person's team contacts. NotFound if they don't exist;
    /// Conflict if the database still references them (e.g. as a referee on an allocation).
    /// </summary>
    Task<ServiceResult> DeletePersonAsync(Guid id, CancellationToken cancellationToken = default);
}
