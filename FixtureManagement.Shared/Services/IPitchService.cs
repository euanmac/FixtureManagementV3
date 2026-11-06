using FixtureManagement.Shared.Dtos;

namespace FixtureManagement.Shared.Services;

/// <summary>Pitches (read-only for now). Website: EF Core; desktop: HttpClient against /api/pitches.</summary>
public interface IPitchService
{
    /// <summary>All pitches in display order.</summary>
    Task<IReadOnlyList<PitchDto>> GetPitchesAsync(CancellationToken cancellationToken = default);

    Task<PitchDto?> GetPitchAsync(Guid id, CancellationToken cancellationToken = default);
}
