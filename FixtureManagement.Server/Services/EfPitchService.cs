using FixtureManagement.Shared.Dtos;
using FixtureManagement.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace FixtureManagement.Server.Services;

/// <summary>IPitchService backed by the database (read-only for now).</summary>
public class EfPitchService(IDbContextFactory<AppDBContext> dbFactory) : IPitchService
{
    public async Task<IReadOnlyList<PitchDto>> GetPitchesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var pitches = await db.Pitch.AsNoTracking().OrderBy(p => p.DisplayOrder).ToListAsync(cancellationToken);
        return pitches.Select(p => p.ToDto()).ToList();
    }

    public async Task<PitchDto?> GetPitchAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var pitch = await db.Pitch.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        return pitch?.ToDto();
    }
}
