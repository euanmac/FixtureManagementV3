using FixtureManagement.Server.Models;
using FixtureManagement.Server.Services;
using FixtureManagement.Shared.Dtos;
using FixtureManagement.Shared.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FixtureManagement.Tests.Server;

/// <summary>
/// Runs EfTeamService against a real SQLite database held in memory, so queries,
/// Includes and ordering behave exactly as they do against local.db.
/// </summary>
public sealed class EfTeamServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TestDbFactory _factory;
    private readonly EfTeamService _service;

    public EfTeamServiceTests()
    {
        // The in-memory database lives as long as this connection stays open.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDBContext>().UseSqlite(_connection).Options;
        _factory = new TestDbFactory(options);
        using (var db = _factory.CreateDbContext())
            db.Database.EnsureCreated();
        _service = new EfTeamService(_factory);
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task GetTeams_is_empty_for_an_empty_database()
    {
        Assert.Empty(await _service.GetTeamsAsync());
    }

    [Fact]
    public async Task Create_then_Get_round_trips_every_editable_field()
    {
        var edit = NewEdit("Lions", AgeGroup.U11, Gender.Female, Division.Two);
        edit.League = League.BucksGirls;
        edit.FullTimeTeamId = "456";
        edit.FullTimeLeagueId = "123";
        edit.RefRequired = true;

        var created = await _service.CreateTeamAsync(edit);
        var loaded = await _service.GetTeamAsync(created.Id);

        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.NotNull(loaded);
        Assert.Equal("Lions", loaded.Name);
        Assert.Equal(AgeGroup.U11, loaded.AgeGroup);
        Assert.Equal(Gender.Female, loaded.Gender);
        Assert.Equal(League.BucksGirls, loaded.League);
        Assert.Equal(Division.Two, loaded.Division);
        Assert.Equal("456", loaded.FullTimeTeamId);
        Assert.Equal("123", loaded.FullTimeLeagueId);
        Assert.True(loaded.RefRequired);
        Assert.Empty(loaded.Contacts);
    }

    [Fact]
    public async Task GetTeam_returns_null_for_an_unknown_id()
    {
        Assert.Null(await _service.GetTeamAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetTeams_orders_by_age_group_then_gender_then_division()
    {
        await _service.CreateTeamAsync(NewEdit("C", AgeGroup.U12, Gender.Male, Division.One));
        await _service.CreateTeamAsync(NewEdit("B", AgeGroup.U9, Gender.Female, Division.One));
        await _service.CreateTeamAsync(NewEdit("A2", AgeGroup.U9, Gender.Male, Division.Two));
        await _service.CreateTeamAsync(NewEdit("A1", AgeGroup.U9, Gender.Male, Division.One));

        var names = (await _service.GetTeamsAsync()).Select(t => t.Name);

        Assert.Equal(["A1", "A2", "B", "C"], names);
    }

    [Fact]
    public async Task Update_changes_fields_and_keeps_the_id()
    {
        var created = await _service.CreateTeamAsync(NewEdit("Old", AgeGroup.U10, Gender.Male, Division.One));
        var edit = NewEdit("New", AgeGroup.U13, Gender.Mixed, Division.Red);

        Assert.True(await _service.UpdateTeamAsync(created.Id, edit));

        var loaded = await _service.GetTeamAsync(created.Id);
        Assert.NotNull(loaded);
        Assert.Equal("New", loaded.Name);
        Assert.Equal(AgeGroup.U13, loaded.AgeGroup);
        Assert.Equal(Division.Red, loaded.Division);
        Assert.Single(await _service.GetTeamsAsync());
    }

    [Fact]
    public async Task Update_and_Delete_return_false_for_an_unknown_id()
    {
        Assert.False(await _service.UpdateTeamAsync(Guid.NewGuid(), NewEdit("X", AgeGroup.U10, Gender.Male, Division.One)));
        Assert.False(await _service.DeleteTeamAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Delete_removes_the_team()
    {
        var created = await _service.CreateTeamAsync(NewEdit("Gone", AgeGroup.U10, Gender.Male, Division.One));

        Assert.True(await _service.DeleteTeamAsync(created.Id));
        Assert.Null(await _service.GetTeamAsync(created.Id));
    }

    [Fact]
    public async Task Contacts_are_mapped_with_person_details_and_ordered_by_contact_type()
    {
        var team = await _service.CreateTeamAsync(NewEdit("Lions", AgeGroup.U10, Gender.Male, Division.One));
        using (var db = _factory.CreateDbContext())
        {
            var coach = new Person { Id = Guid.NewGuid(), FirstName = "Cal", LastName = "Coach", Email = "cal@example.com" };
            var manager = new Person { Id = Guid.NewGuid(), FirstName = "Mia", LastName = "Manager", Email = "mia@example.com" };
            db.Person.AddRange(coach, manager);
            db.TeamContact.AddRange(
                new TeamContact { Id = Guid.NewGuid(), TeamId = team.Id, PersonId = coach.Id, ContactType = ContactType.Coach },
                new TeamContact { Id = Guid.NewGuid(), TeamId = team.Id, PersonId = manager.Id, ContactType = ContactType.Manager });
            await db.SaveChangesAsync();
        }

        var loaded = await _service.GetTeamAsync(team.Id);

        Assert.NotNull(loaded);
        Assert.Collection(loaded.Contacts,
            c => { Assert.Equal(ContactType.Manager, c.ContactType); Assert.Equal("Mia Manager", c.FullName); Assert.Equal("mia@example.com", c.Email); },
            c => { Assert.Equal(ContactType.Coach, c.ContactType); Assert.Equal("Cal Coach", c.FullName); });
        Assert.Equal(2, (await _service.GetTeamsAsync()).Single().Contacts.Count);
    }

    private static TeamEditDto NewEdit(string name, AgeGroup ageGroup, Gender gender, Division division) => new()
    {
        Name = name,
        AgeGroup = ageGroup,
        Gender = gender,
        League = League.SouthBucksMini,
        Division = division,
    };

    private sealed class TestDbFactory(DbContextOptions<AppDBContext> options) : IDbContextFactory<AppDBContext>
    {
        public AppDBContext CreateDbContext() => new(options);
    }
}
