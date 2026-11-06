using FixtureManagement.Shared.Dtos;

namespace FixtureManagement.Shared.Services;

/// <summary>Fixtures and their pitch allocations. Website: EF Core; desktop: HttpClient against /api/fixtures.</summary>
public interface IFixtureService
{
    /// <summary>Fixtures matching the query, by date then team age group.</summary>
    Task<IReadOnlyList<FixtureDto>> GetFixturesAsync(FixtureQuery? query = null, CancellationToken cancellationToken = default);

    Task<FixtureDto?> GetFixtureAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Invalid if the team doesn't exist.</summary>
    Task<ServiceResult<FixtureDto>> CreateFixtureAsync(FixtureEditDto fixture, CancellationToken cancellationToken = default);

    /// <summary>Adds several fixtures in one save (e.g. a FullTime upload). Invalid, and nothing saved, if any team doesn't exist.</summary>
    Task<ServiceResult<IReadOnlyList<FixtureDto>>> CreateFixturesAsync(IReadOnlyList<FixtureEditDto> fixtures, CancellationToken cancellationToken = default);

    /// <summary>NotFound if the fixture doesn't exist; Invalid if the team doesn't exist.</summary>
    Task<ServiceResult> UpdateFixtureAsync(Guid id, FixtureEditDto fixture, CancellationToken cancellationToken = default);

    /// <summary>Also removes its allocation. Returns false if the fixture doesn't exist.</summary>
    Task<bool> DeleteFixtureAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes every fixture for a team and/or after a date (exclusive) and returns how many went.
    /// At least one filter is required, so this can never delete everything by accident.
    /// </summary>
    Task<int> DeleteFixturesAsync(Guid? teamId, DateOnly? after, CancellationToken cancellationToken = default);

    /// <summary>Creates or replaces the fixture's allocation. NotFound if the fixture doesn't exist; Invalid if the pitch doesn't exist.</summary>
    Task<ServiceResult<FixtureAllocationDto>> SetAllocationAsync(Guid fixtureId, FixtureAllocationEditDto allocation, CancellationToken cancellationToken = default);

    /// <summary>Returns false if the fixture doesn't exist or has no allocation.</summary>
    Task<bool> RemoveAllocationAsync(Guid fixtureId, CancellationToken cancellationToken = default);
}
