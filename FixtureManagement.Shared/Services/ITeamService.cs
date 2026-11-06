using FixtureManagement.Shared.Dtos;

namespace FixtureManagement.Shared.Services;

/// <summary>
/// Team operations used by the UI. The website implements this with EF Core;
/// the desktop app implements it over HttpClient against /api/teams.
/// </summary>
public interface ITeamService
{
    /// <summary>All teams with their contacts, ordered by age group, gender, then division.</summary>
    Task<IReadOnlyList<TeamDto>> GetTeamsAsync(CancellationToken cancellationToken = default);

    /// <summary>One team with its contacts, or null if it doesn't exist.</summary>
    Task<TeamDto?> GetTeamAsync(Guid id, CancellationToken cancellationToken = default);

    Task<TeamDto> CreateTeamAsync(TeamEditDto team, CancellationToken cancellationToken = default);

    /// <summary>Returns false if the team doesn't exist.</summary>
    Task<bool> UpdateTeamAsync(Guid id, TeamEditDto team, CancellationToken cancellationToken = default);

    /// <summary>Returns false if the team doesn't exist.</summary>
    Task<bool> DeleteTeamAsync(Guid id, CancellationToken cancellationToken = default);
}
