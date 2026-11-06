using FixtureManagement.Server.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FixtureManagement.Tests.Server;

/// <summary>
/// A fresh in-memory SQLite database per test class instance (xUnit makes one per test),
/// plus helpers to seed the rows a test needs.
/// </summary>
public sealed class SqliteTestDb : IDbContextFactory<AppDBContext>, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<AppDBContext> _options;

    public SqliteTestDb()
    {
        // The in-memory database lives as long as this connection stays open.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDBContext>().UseSqlite(_connection).Options;
        using var db = CreateDbContext();
        db.Database.EnsureCreated();
    }

    public AppDBContext CreateDbContext() => new(_options);

    public void Dispose() => _connection.Dispose();

    public async Task<Team> AddTeamAsync(string name = "Lions", AgeGroup ageGroup = AgeGroup.U10, Gender gender = Gender.Male)
    {
        var team = new Team { Id = Guid.NewGuid(), Name = name, AgeGroup = ageGroup, Gender = gender, League = League.SouthBucksMini, Division = Division.One };
        await AddAsync(team);
        return team;
    }

    public async Task<Pitch> AddPitchAsync(string name = "Pitch 1", int displayOrder = 1)
    {
        var pitch = new Pitch { Id = Guid.NewGuid(), Name = name, DisplayOrder = displayOrder };
        await AddAsync(pitch);
        return pitch;
    }

    public async Task<Person> AddPersonAsync(string firstName = "Sam", string lastName = "Smith")
    {
        var person = new Person { Id = Guid.NewGuid(), FirstName = firstName, LastName = lastName, Email = $"{firstName}@example.com".ToLowerInvariant() };
        await AddAsync(person);
        return person;
    }

    public async Task<TeamContact> AddContactAsync(Team team, Person person, ContactType type = ContactType.Manager)
    {
        var contact = new TeamContact { Id = Guid.NewGuid(), TeamId = team.Id, PersonId = person.Id, ContactType = type };
        await AddAsync(contact);
        return contact;
    }

    public async Task AddAsync(object entity)
    {
        await using var db = CreateDbContext();
        db.Add(entity);
        await db.SaveChangesAsync();
    }
}
