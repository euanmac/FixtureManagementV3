using FixtureManagement.Server.Models;
using FixtureManagement.Shared.Dtos;
using FixtureManagement.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace FixtureManagement.Server.Services;

/// <summary>IPersonService backed by the database.</summary>
public class EfPersonService(IDbContextFactory<AppDBContext> dbFactory) : IPersonService
{
    public async Task<IReadOnlyList<PersonDto>> GetPeopleAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var people = await db.Person.AsNoTracking()
            .OrderBy(p => p.LastName).ThenBy(p => p.FirstName)
            .ToListAsync(cancellationToken);
        // Person has no navigation to its contacts, so load them all once and match in memory.
        var contacts = await db.TeamContact.AsNoTracking().Include(c => c.Team).ToListAsync(cancellationToken);
        return people.Select(p => p.ToDto(contacts)).ToList();
    }

    public async Task<PersonDto?> GetPersonAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var person = await db.Person.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (person is null) return null;
        var contacts = await db.TeamContact.AsNoTracking().Include(c => c.Team)
            .Where(c => c.PersonId == id).ToListAsync(cancellationToken);
        return person.ToDto(contacts);
    }

    public async Task<PersonDto> CreatePersonAsync(PersonDto person, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var entity = new Person { Id = Guid.NewGuid() };
        Apply(person, entity);
        db.Person.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity.ToDto([]);
    }

    public async Task<bool> UpdatePersonAsync(Guid id, PersonDto person, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.Person.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (entity is null) return false;
        Apply(person, entity);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<ServiceResult> DeletePersonAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.Person.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (entity is null) return ServiceResult.NotFound();

        // Remove the contacts explicitly rather than relying on the database's cascade.
        db.TeamContact.RemoveRange(await db.TeamContact.Where(c => c.PersonId == id).ToListAsync(cancellationToken));
        db.Person.Remove(entity);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // e.g. FixtureAllocation.RefereeId, which exists in the database but not in the EF model.
            return ServiceResult.Conflict("This person is still referenced elsewhere (for example as a referee) and can't be deleted.");
        }
        return ServiceResult.Success();
    }

    private static void Apply(PersonDto source, Person target)
    {
        target.FirstName = source.FirstName;
        target.LastName = source.LastName;
        target.Tel = source.Tel ?? "";
        target.Email = source.Email ?? "";
        target.IsRef = source.IsRef;
    }
}
