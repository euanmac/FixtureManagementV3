using FixtureManagement.Shared.Dtos;

namespace FixtureManagement.Shared.Services;

/// <summary>Links between people and teams. Website: EF Core; desktop: HttpClient against /api/teamcontacts.</summary>
public interface ITeamContactService
{
    /// <summary>Contacts, optionally for one team and/or one person, by team age group then contact type.</summary>
    Task<IReadOnlyList<TeamContactDto>> GetTeamContactsAsync(Guid? teamId = null, Guid? personId = null, CancellationToken cancellationToken = default);

    Task<TeamContactDto?> GetTeamContactAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Invalid if the team or person doesn't exist; Conflict if the same link already exists.</summary>
    Task<ServiceResult<TeamContactDto>> CreateTeamContactAsync(TeamContactCreateDto contact, CancellationToken cancellationToken = default);

    /// <summary>Returns false if the contact doesn't exist. The person themselves is kept.</summary>
    Task<bool> DeleteTeamContactAsync(Guid id, CancellationToken cancellationToken = default);
}
