using FixtureManagement.Server.Models;
using FixtureManagement.Shared.Dtos;
using FixtureManagement.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace FixtureManagement.Server.Services;

/// <summary>ITeamContactService backed by the database.</summary>
public class EfTeamContactService(IDbContextFactory<AppDBContext> dbFactory) : ITeamContactService
{
    public async Task<IReadOnlyList<TeamContactDto>> GetTeamContactsAsync(Guid? teamId = null, Guid? personId = null, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var query = WithDetails(db.TeamContact.AsNoTracking());
        if (teamId is { } t) query = query.Where(c => c.TeamId == t);
        if (personId is { } p) query = query.Where(c => c.PersonId == p);
        var contacts = await query.ToListAsync(cancellationToken);
        return contacts
            .OrderBy(c => c.Team?.AgeGroup)
                .ThenBy(c => c.Team?.Gender)
                .ThenBy(c => c.Team?.Name)
                .ThenBy(c => c.ContactType)
            .Select(c => c.ToDto())
            .ToList();
    }

    public async Task<TeamContactDto?> GetTeamContactAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var contact = await WithDetails(db.TeamContact.AsNoTracking()).FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        return contact?.ToDto();
    }

    public async Task<ServiceResult<TeamContactDto>> CreateTeamContactAsync(TeamContactDto contact, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (!await db.Team.AnyAsync(t => t.Id == contact.TeamId, cancellationToken))
            return ServiceResult<TeamContactDto>.From(ServiceResult.Invalid(nameof(contact.TeamId), "Team not found."));
        if (!await db.Person.AnyAsync(p => p.Id == contact.PersonId, cancellationToken))
            return ServiceResult<TeamContactDto>.From(ServiceResult.Invalid(nameof(contact.PersonId), "Person not found."));
        if (await db.TeamContact.AnyAsync(c => c.TeamId == contact.TeamId && c.PersonId == contact.PersonId
                                             && c.ContactType == contact.ContactType, cancellationToken))
            return ServiceResult<TeamContactDto>.From(ServiceResult.Conflict("This person is already that type of contact for the team."));

        var entity = new TeamContact
        {
            Id = Guid.NewGuid(),
            TeamId = contact.TeamId,
            PersonId = contact.PersonId,
            ContactType = contact.ContactType,
        };
        db.TeamContact.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        await db.Entry(entity).Reference(c => c.Team).LoadAsync(cancellationToken);
        await db.Entry(entity).Reference(c => c.Person).LoadAsync(cancellationToken);
        return ServiceResult<TeamContactDto>.Success(entity.ToDto());
    }

    public async Task<bool> DeleteTeamContactAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.TeamContact.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (entity is null) return false;
        db.TeamContact.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static IQueryable<TeamContact> WithDetails(IQueryable<TeamContact> query) =>
        query.Include(c => c.Team).Include(c => c.Person);
}
