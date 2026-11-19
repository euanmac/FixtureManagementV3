using FixtureManagement.Server.Models;
using FixtureManagement.Shared.Dtos;
using FixtureManagement.Shared.Reconciliation;
using FixtureManagement.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace FixtureManagement.Server.Services;

/// <summary>ISourceUploadService backed by the database (SourceUpload + UploadedFixture).</summary>
public class EfSourceUploadService(IDbContextFactory<AppDBContext> dbFactory, TimeProvider time) : ISourceUploadService
{
    public async Task<SourceUploadDto> SaveUploadAsync(SourceUploadDto upload, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Latest per source only: the old upload's fixtures go with it (cascade).
        await db.SourceUpload.Where(u => u.Source == upload.Source).ExecuteDeleteAsync(cancellationToken);

        var entity = new SourceUpload
        {
            Id = Guid.NewGuid(),
            Source = upload.Source,
            SourceName = upload.SourceName,
            UploadedAt = time.GetUtcNow().UtcDateTime,
            UploadedBy = upload.UploadedBy,
            From = upload.From,
            To = upload.To,
            TeamIds = upload.TeamIds.Distinct().ToList(),
            Fixtures = upload.Fixtures.Select(f => new UploadedFixture
            {
                Id = Guid.NewGuid(),
                TeamId = f.TeamId == Guid.Empty ? null : f.TeamId,
                SourceTeamName = f.SourceTeamName,
                Date = f.Date,
                IsHome = f.IsHome,
                Opponent = f.Opponent,
                FixtureType = f.FixtureType,
                Competition = f.Competition,
                SourceFixtureId = f.SourceFixtureId,
                Status = f.Status,
            }).ToList(),
        };
        db.SourceUpload.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<SourceUploadDto?> GetLatestUploadAsync(string source, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.SourceUpload.AsNoTracking().Include(u => u.Fixtures)
            .FirstOrDefaultAsync(u => u.Source == source, cancellationToken);
        return entity is null ? null : ToDto(entity);
    }

    private static SourceUploadDto ToDto(SourceUpload u) => new()
    {
        Id = u.Id,
        Source = u.Source,
        SourceName = u.SourceName,
        UploadedAt = DateTime.SpecifyKind(u.UploadedAt, DateTimeKind.Utc),
        UploadedBy = u.UploadedBy,
        From = u.From,
        To = u.To,
        TeamIds = u.TeamIds,
        Fixtures = u.Fixtures
            .OrderBy(f => f.Date is null).ThenBy(f => f.Date).ThenBy(f => f.SourceTeamName)
            .Select(f => new SourceFixture
            {
                TeamId = f.TeamId ?? Guid.Empty,
                SourceTeamName = f.SourceTeamName,
                Date = f.Date,
                IsHome = f.IsHome,
                Opponent = f.Opponent,
                FixtureType = f.FixtureType,
                Competition = f.Competition,
                SourceFixtureId = f.SourceFixtureId,
                Status = f.Status,
            })
            .ToList(),
    };
}
