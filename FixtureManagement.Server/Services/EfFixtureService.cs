using FixtureManagement.Server.Models;
using FixtureManagement.Shared.Dtos;
using FixtureManagement.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace FixtureManagement.Server.Services;

/// <summary>IFixtureService backed by the database.</summary>
public class EfFixtureService(IDbContextFactory<AppDBContext> dbFactory) : IFixtureService
{
    public async Task<IReadOnlyList<FixtureDto>> GetFixturesAsync(FixtureQuery? query = null, CancellationToken cancellationToken = default)
    {
        query ??= new FixtureQuery();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var fixtures = WithDetails(db.Fixture.AsNoTracking());
        if (query.From is { } from) fixtures = fixtures.Where(f => f.Date >= from);
        if (query.To is { } to) fixtures = fixtures.Where(f => f.Date <= to);
        if (query.TeamId is { } teamId) fixtures = fixtures.Where(f => f.TeamId == teamId);

        var list = await fixtures
            .OrderBy(f => f.Date)
                .ThenBy(f => f.Team!.AgeGroup)
                .ThenBy(f => f.Team!.Gender)
            .ToListAsync(cancellationToken);
        return list.Select(f => f.ToDto()).ToList();
    }

    public async Task<FixtureDto?> GetFixtureAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var fixture = await WithDetails(db.Fixture.AsNoTracking()).FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        return fixture?.ToDto();
    }

    public async Task<ServiceResult<FixtureDto>> CreateFixtureAsync(FixtureDto fixture, CancellationToken cancellationToken = default)
    {
        var result = await CreateFixturesAsync([fixture], cancellationToken);
        return result.Succeeded
            ? ServiceResult<FixtureDto>.Success(result.Value![0])
            : ServiceResult<FixtureDto>.From(result);
    }

    public async Task<ServiceResult<IReadOnlyList<FixtureDto>>> CreateFixturesAsync(IReadOnlyList<FixtureDto> fixtures, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var teamIds = fixtures.Select(f => f.TeamId).Distinct().ToList();
        var teams = await db.Team.Where(t => teamIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, cancellationToken);
        var missing = teamIds.Where(id => !teams.ContainsKey(id)).ToList();
        if (missing.Count > 0)
            return ServiceResult<IReadOnlyList<FixtureDto>>.From(
                ServiceResult.Invalid(nameof(FixtureDto.TeamId), $"Team not found: {string.Join(", ", missing)}."));

        var entities = fixtures.Select(f =>
        {
            var entity = new Fixture { Id = Guid.NewGuid() };
            Apply(f, entity);
            return entity;
        }).ToList();
        db.Fixture.AddRange(entities);
        await db.SaveChangesAsync(cancellationToken);

        // The teams are tracked by this context, so each fixture's Team is already filled in.
        return ServiceResult<IReadOnlyList<FixtureDto>>.Success(entities.Select(f => f.ToDto()).ToList());
    }

    public async Task<ServiceResult> UpdateFixtureAsync(Guid id, FixtureDto fixture, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.Fixture.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        if (entity is null) return ServiceResult.NotFound();
        if (!await db.Team.AnyAsync(t => t.Id == fixture.TeamId, cancellationToken))
            return ServiceResult.Invalid(nameof(fixture.TeamId), "Team not found.");

        Apply(fixture, entity);
        await db.SaveChangesAsync(cancellationToken);
        return ServiceResult.Success();
    }

    public async Task<bool> DeleteFixtureAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.Fixture.Include(f => f.FixtureAllocation).FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
        if (entity is null) return false;
        if (entity.FixtureAllocation is not null) db.FixtureAllocation.Remove(entity.FixtureAllocation);
        db.Fixture.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> DeleteFixturesAsync(Guid? teamId, DateOnly? after, CancellationToken cancellationToken = default)
    {
        if (teamId is null && after is null)
            throw new ArgumentException("Give a team and/or a date, so this can't delete every fixture.");

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Fixture.Include(f => f.FixtureAllocation).AsQueryable();
        if (teamId is { } t) query = query.Where(f => f.TeamId == t);
        if (after is { } a) query = query.Where(f => f.Date > a);

        var fixtures = await query.ToListAsync(cancellationToken);
        db.FixtureAllocation.RemoveRange(fixtures.Where(f => f.FixtureAllocation is not null).Select(f => f.FixtureAllocation!));
        db.Fixture.RemoveRange(fixtures);
        await db.SaveChangesAsync(cancellationToken);
        return fixtures.Count;
    }

    public async Task<ServiceResult<FixtureAllocationDto>> SetAllocationAsync(Guid fixtureId, FixtureAllocationDto allocation, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var fixture = await db.Fixture.Include(f => f.FixtureAllocation).FirstOrDefaultAsync(f => f.Id == fixtureId, cancellationToken);
        if (fixture is null) return ServiceResult<FixtureAllocationDto>.From(ServiceResult.NotFound());
        var pitch = await db.Pitch.FirstOrDefaultAsync(p => p.Id == allocation.PitchId, cancellationToken);
        if (pitch is null)
            return ServiceResult<FixtureAllocationDto>.From(ServiceResult.Invalid(nameof(allocation.PitchId), "Pitch not found."));

        var entity = fixture.FixtureAllocation;
        if (entity is null)
        {
            entity = new FixtureAllocation { Id = Guid.NewGuid(), FixtureId = fixtureId };
            db.FixtureAllocation.Add(entity);
        }
        entity.PitchId = allocation.PitchId;
        entity.Start = allocation.Start;
        entity.Duration = allocation.Duration;
        entity.IsConfirmed = allocation.IsConfirmed;
        await db.SaveChangesAsync(cancellationToken);

        entity.Pitch = pitch;
        return ServiceResult<FixtureAllocationDto>.Success(entity.ToDto());
    }

    public async Task<bool> RemoveAllocationAsync(Guid fixtureId, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.FixtureAllocation.FirstOrDefaultAsync(a => a.FixtureId == fixtureId, cancellationToken);
        if (entity is null) return false;
        db.FixtureAllocation.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> ConfirmAllocationsAsync(IReadOnlyCollection<Guid> fixtureIds, CancellationToken cancellationToken = default)
    {
        if (fixtureIds.Count == 0) return 0;
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var ids = fixtureIds.Distinct().ToList();
        var allocations = await db.FixtureAllocation
            .Where(a => ids.Contains(a.FixtureId) && !a.IsConfirmed)
            .ToListAsync(cancellationToken);
        foreach (var allocation in allocations)
        {
            allocation.IsConfirmed = true;
        }
        await db.SaveChangesAsync(cancellationToken);
        return allocations.Count;
    }

    private static IQueryable<Fixture> WithDetails(IQueryable<Fixture> query) =>
        query.Include(f => f.Team)
            .Include(f => f.FixtureAllocation)
                .ThenInclude(a => a!.Pitch);

    // Only the editable fields are copied, so a caller can't overwrite Id or the allocation.
    private static void Apply(FixtureDto source, Fixture target)
    {
        target.TeamId = source.TeamId;
        target.Date = source.Date;
        target.IsHome = source.IsHome;
        target.Opponent = source.Opponent;
        target.FixtureType = source.FixtureType;
    }
}
