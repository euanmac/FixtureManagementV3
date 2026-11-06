using FixtureManagement.Server.Services;
using Microsoft.EntityFrameworkCore;

namespace FixtureManagement.Tests.Server;

public sealed class EfPersonServiceTests : IDisposable
{
    private readonly SqliteTestDb _db = new();
    private readonly EfPersonService _service;

    public EfPersonServiceTests() => _service = new EfPersonService(_db);

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Create_then_Get_round_trips_every_field()
    {
        var created = await _service.CreatePersonAsync(new PersonDto
        {
            FirstName = "Sam", LastName = "Smith", Tel = "07700 900000", Email = "sam@example.com", IsRef = true,
        });

        var loaded = await _service.GetPersonAsync(created.Id);

        Assert.NotNull(loaded);
        Assert.Equal("Sam Smith", loaded.FullName);
        Assert.Equal("07700 900000", loaded.Tel);
        Assert.Equal("sam@example.com", loaded.Email);
        Assert.True(loaded.IsRef);
        Assert.Empty(loaded.Teams);
    }

    [Fact]
    public async Task GetPeople_orders_by_last_then_first_name_and_lists_their_teams()
    {
        var u8 = await _db.AddTeamAsync("Young", AgeGroup.U8);
        var u14 = await _db.AddTeamAsync("Old", AgeGroup.U14);
        var bea = await _db.AddPersonAsync("Bea", "Jones");
        var al = await _db.AddPersonAsync("Al", "Jones");
        await _db.AddPersonAsync("Zed", "Adams");
        await _db.AddContactAsync(u14, al, ContactType.Coach);
        await _db.AddContactAsync(u8, al, ContactType.Manager);

        var people = await _service.GetPeopleAsync();

        Assert.Equal(["Zed Adams", "Al Jones", "Bea Jones"], people.Select(p => p.FullName));
        var alsTeams = people.Single(p => p.Id == al.Id).Teams;
        Assert.Collection(alsTeams,
            t => { Assert.Equal(AgeGroup.U8, t.Team.AgeGroup); Assert.Equal(ContactType.Manager, t.ContactType); },
            t => { Assert.Equal(AgeGroup.U14, t.Team.AgeGroup); Assert.Equal(ContactType.Coach, t.ContactType); });
        Assert.Empty(people.Single(p => p.Id == bea.Id).Teams);
    }

    [Fact]
    public async Task Update_changes_fields()
    {
        var person = await _db.AddPersonAsync();

        Assert.True(await _service.UpdatePersonAsync(person.Id, new PersonDto { FirstName = "New", LastName = "Name", Email = "new@example.com" }));

        var loaded = await _service.GetPersonAsync(person.Id);
        Assert.Equal("New Name", loaded!.FullName);
        Assert.Equal("new@example.com", loaded.Email);
        Assert.False(await _service.UpdatePersonAsync(Guid.NewGuid(), new PersonDto { FirstName = "X", LastName = "Y" }));
    }

    [Fact]
    public async Task Update_ignores_the_posted_teams_list()
    {
        var team = await _db.AddTeamAsync();
        var person = await _db.AddPersonAsync();
        var edit = new PersonDto
        {
            FirstName = "Sam", LastName = "Smith",
            Teams = [new PersonTeamDto { TeamContactId = Guid.NewGuid(), Team = new TeamSummaryDto { Id = team.Id } }],
        };

        Assert.True(await _service.UpdatePersonAsync(person.Id, edit));

        Assert.Empty((await _service.GetPersonAsync(person.Id))!.Teams);
        await using var db = _db.CreateDbContext();
        Assert.Equal(0, await db.TeamContact.CountAsync());
    }

    [Fact]
    public async Task Delete_removes_the_person_and_their_contacts_but_not_the_team()
    {
        var team = await _db.AddTeamAsync();
        var person = await _db.AddPersonAsync();
        await _db.AddContactAsync(team, person);

        Assert.True((await _service.DeletePersonAsync(person.Id)).Succeeded);

        Assert.Null(await _service.GetPersonAsync(person.Id));
        await using var db = _db.CreateDbContext();
        Assert.Equal(0, await db.TeamContact.CountAsync());
        Assert.Equal(1, await db.Team.CountAsync());
        Assert.Equal(ServiceStatus.NotFound, (await _service.DeletePersonAsync(person.Id)).Status);
    }
}

public sealed class EfTeamContactServiceTests : IDisposable
{
    private readonly SqliteTestDb _db = new();
    private readonly EfTeamContactService _service;

    public EfTeamContactServiceTests() => _service = new EfTeamContactService(_db);

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task Create_links_a_person_to_a_team()
    {
        var team = await _db.AddTeamAsync("Lions", AgeGroup.U9, Gender.Female);
        var person = await _db.AddPersonAsync("Mia", "Manager");

        var result = await _service.CreateTeamContactAsync(new TeamContactDto { TeamId = team.Id, PersonId = person.Id, ContactType = ContactType.Coach });

        Assert.True(result.Succeeded);
        Assert.Equal("Mia Manager", result.Value!.FullName);
        Assert.Equal("U9 Girls - Lions", result.Value.Team.DisplayName);
        Assert.Equal(ContactType.Coach, result.Value.ContactType);
        var loaded = await _service.GetTeamContactAsync(result.Value.Id);
        Assert.NotNull(loaded);
        Assert.Equal(team.Id, loaded.TeamId);
        Assert.Equal(person.Id, loaded.PersonId);
        Assert.Equal("Mia Manager", loaded.FullName);
        Assert.Equal(result.Value.Team, loaded.Team); // TeamSummaryDto is a record, so this compares values
    }

    [Fact]
    public async Task Create_checks_the_team_and_person_exist()
    {
        var team = await _db.AddTeamAsync();
        var person = await _db.AddPersonAsync();

        var noTeam = await _service.CreateTeamContactAsync(new TeamContactDto { TeamId = Guid.NewGuid(), PersonId = person.Id });
        var noPerson = await _service.CreateTeamContactAsync(new TeamContactDto { TeamId = team.Id, PersonId = Guid.NewGuid() });

        Assert.Equal(ServiceStatus.Invalid, noTeam.Status);
        Assert.Contains(nameof(TeamContactDto.TeamId), noTeam.Errors.Keys);
        Assert.Equal(ServiceStatus.Invalid, noPerson.Status);
        Assert.Contains(nameof(TeamContactDto.PersonId), noPerson.Errors.Keys);
    }

    [Fact]
    public async Task Create_rejects_an_exact_duplicate_but_allows_another_role()
    {
        var team = await _db.AddTeamAsync();
        var person = await _db.AddPersonAsync();
        var manager = new TeamContactDto { TeamId = team.Id, PersonId = person.Id, ContactType = ContactType.Manager };

        Assert.True((await _service.CreateTeamContactAsync(manager)).Succeeded);
        Assert.Equal(ServiceStatus.Conflict, (await _service.CreateTeamContactAsync(manager)).Status);
        Assert.True((await _service.CreateTeamContactAsync(new TeamContactDto { TeamId = team.Id, PersonId = person.Id, ContactType = ContactType.Coach })).Succeeded);
    }

    [Fact]
    public async Task GetTeamContacts_filters_by_team_and_person()
    {
        var lions = await _db.AddTeamAsync("Lions", AgeGroup.U12);
        var tigers = await _db.AddTeamAsync("Tigers", AgeGroup.U8);
        var sam = await _db.AddPersonAsync("Sam");
        var ali = await _db.AddPersonAsync("Ali");
        await _db.AddContactAsync(lions, sam);
        await _db.AddContactAsync(tigers, sam);
        await _db.AddContactAsync(lions, ali, ContactType.Coach);

        Assert.Equal(3, (await _service.GetTeamContactsAsync()).Count);
        Assert.Equal(["Tigers", "Lions"], (await _service.GetTeamContactsAsync()).Select(c => c.Team.Name).Distinct()); // U8 before U12
        Assert.Equal(2, (await _service.GetTeamContactsAsync(teamId: lions.Id)).Count);
        Assert.Equal(2, (await _service.GetTeamContactsAsync(personId: sam.Id)).Count);
        Assert.Single(await _service.GetTeamContactsAsync(lions.Id, sam.Id));
    }

    [Fact]
    public async Task Delete_removes_the_link_but_keeps_the_person()
    {
        var team = await _db.AddTeamAsync();
        var person = await _db.AddPersonAsync();
        var contact = await _db.AddContactAsync(team, person);

        Assert.True(await _service.DeleteTeamContactAsync(contact.Id));
        Assert.False(await _service.DeleteTeamContactAsync(contact.Id));
        await using var db = _db.CreateDbContext();
        Assert.Equal(1, await db.Person.CountAsync());
    }
}
