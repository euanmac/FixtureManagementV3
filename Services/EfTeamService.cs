using FixtureManagement.Shared.Dtos;
using FixtureManagement.Shared.Services;
using FixtureManagementV3.Models;
using Microsoft.EntityFrameworkCore;

namespace FixtureManagementV3.Services;

/// <summary>ITeamService backed directly by the database. Used by the website and the /api/teams endpoints.</summary>
public class EfTeamService(IDbContextFactory<AppDBContext> dbFactory) : ITeamService
{
    public async Task<IReadOnlyList<TeamDto>> GetTeamsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var teams = await db.Team
            .AsNoTracking()
            .Include(t => t.Contacts)
                .ThenInclude(c => c.Person)
            .OrderBy(t => t.AgeGroup)
                .ThenBy(t => t.Gender)
                .ThenBy(t => t.Division)
            .ToListAsync(cancellationToken);
        return teams.Select(ToDto).ToList();
    }

    public async Task<TeamDto?> GetTeamAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var team = await db.Team
            .AsNoTracking()
            .Include(t => t.Contacts)
                .ThenInclude(c => c.Person)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        return team is null ? null : ToDto(team);
    }

    public async Task<TeamDto> CreateTeamAsync(TeamEditDto team, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var entity = new Team { Id = Guid.NewGuid() };
        Apply(team, entity);
        db.Team.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<bool> UpdateTeamAsync(Guid id, TeamEditDto team, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.Team.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (entity is null) return false;
        Apply(team, entity);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteTeamAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.Team.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (entity is null) return false;
        db.Team.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    // Only the editable fields are copied, so a caller can't overwrite Id or navigation data.
    private static void Apply(TeamEditDto source, Team target)
    {
        target.Name = source.Name;
        target.AgeGroup = source.AgeGroup;
        target.Gender = source.Gender;
        target.League = source.League;
        target.Division = source.Division;
        target.FullTimeTeamId = source.FullTimeTeamId;
        target.FullTimeLeagueId = source.FullTimeLeagueId;
        target.RefRequired = source.RefRequired;
    }

    private static TeamDto ToDto(Team team) => new()
    {
        Id = team.Id,
        Name = team.Name,
        AgeGroup = team.AgeGroup,
        Gender = team.Gender,
        League = team.League,
        Division = team.Division,
        FullTimeTeamId = team.FullTimeTeamId,
        FullTimeLeagueId = team.FullTimeLeagueId,
        RefRequired = team.RefRequired,
        Contacts = team.Contacts
            .OrderBy(c => c.ContactType)
            .Select(c => new TeamContactSummaryDto
            {
                Id = c.Id,
                PersonId = c.PersonId,
                ContactType = c.ContactType,
                FirstName = c.Person?.FirstName ?? "",
                LastName = c.Person?.LastName ?? "",
                Email = c.Person?.Email ?? "",
            })
            .ToList(),
    };
}
