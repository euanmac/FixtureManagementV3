using FixtureManagement.Server.Data;
using FixtureManagement.Server.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FixtureManagement.Tests.Server;

public sealed class SchemaUpgradeTests : IDisposable
{
    private static readonly string[] NewTables = ["SourceUpload", "UploadedFixture", "ReconciliationDecision"];

    private readonly List<SqliteConnection> _connections = [];

    public void Dispose()
    {
        foreach (var c in _connections) c.Dispose();
        SqliteConnection.ClearAllPools();
    }

    private AppDBContext InMemoryDb()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        _connections.Add(connection);
        return new AppDBContext(new DbContextOptionsBuilder<AppDBContext>().UseSqlite(connection).Options);
    }

    /// <summary>A database as it was before the reconciliation tables: EF's schema minus the new tables.</summary>
    private AppDBContext OldStyleDb()
    {
        var db = InMemoryDb();
        db.Database.EnsureCreated();
        foreach (var table in NewTables.Reverse())
            db.Database.ExecuteSqlRaw($"DROP TABLE \"{table}\"");
        return db;
    }

    [Fact]
    public void A_blank_database_is_left_for_EnsureCreated()
    {
        using var db = InMemoryDb();

        Assert.False(SchemaUpgrade.Apply(db));
        Assert.Empty(Tables(db));
        Assert.True(db.Database.EnsureCreated());   // would be false (and create nothing) if any table existed
    }

    [Fact]
    public void An_existing_database_gets_the_new_tables_exactly_as_EF_would_create_them()
    {
        using var upgraded = OldStyleDb();
        using var fresh = InMemoryDb();
        fresh.Database.EnsureCreated();

        Assert.True(SchemaUpgrade.Apply(upgraded));

        foreach (var table in NewTables)
            Assert.Equal(Describe(fresh, table), Describe(upgraded, table));
    }

    [Fact]
    public void Running_it_again_changes_nothing()
    {
        using var db = OldStyleDb();
        SchemaUpgrade.Apply(db);
        var before = NewTables.Select(t => Describe(db, t)).ToList();

        Assert.True(SchemaUpgrade.Apply(db));

        Assert.Equal(before, NewTables.Select(t => Describe(db, t)).ToList());
    }

    [Fact]
    public async Task The_repo_database_upgrades_without_touching_existing_data()
    {
        var copy = Path.Combine(Path.GetTempPath(), $"schema-upgrade-{Guid.NewGuid():N}.db");
        File.Copy(RepoDatabasePath(), copy);
        try
        {
            var options = new DbContextOptionsBuilder<AppDBContext>().UseSqlite($"DataSource={copy}").Options;
            await using (var db = new AppDBContext(options))
            {
                var fixtures = await db.Fixture.CountAsync();
                var teams = await db.Team.CountAsync();

                Assert.True(SchemaUpgrade.Apply(db));

                Assert.Equal(fixtures, await db.Fixture.CountAsync());
                Assert.Equal(teams, await db.Team.CountAsync());
            }

            // The new tables work through EF, including the team list column and the cascade delete.
            var teamIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
            await using (var db = new AppDBContext(options))
            {
                db.SourceUpload.Add(new SourceUpload
                {
                    Id = Guid.NewGuid(), Source = "FullTime:1", SourceName = "Test", UploadedAt = DateTime.UtcNow, UploadedBy = "me",
                    From = new DateOnly(2026, 11, 1), To = new DateOnly(2027, 5, 31), TeamIds = teamIds,
                    Fixtures = [new UploadedFixture { Id = Guid.NewGuid(), TeamId = teamIds[0], Opponent = "Rivals", Date = null }],
                });
                await db.SaveChangesAsync();
            }
            await using (var db = new AppDBContext(options))
            {
                var upload = await db.SourceUpload.Include(u => u.Fixtures).SingleAsync();
                Assert.Equal(teamIds, upload.TeamIds);
                Assert.Null(Assert.Single(upload.Fixtures).Date);

                db.SourceUpload.Remove(upload);
                await db.SaveChangesAsync();
                Assert.Equal(0, await db.UploadedFixture.CountAsync());
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(copy);
        }
    }

    private static string RepoDatabasePath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "FixtureManagement.Server", "Data", "local.db")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir.FullName, "FixtureManagement.Server", "Data", "local.db");
    }

    private static List<string> Tables(AppDBContext db) =>
        Query(db, "SELECT name FROM sqlite_master WHERE type = 'table' ORDER BY name");

    /// <summary>Columns, foreign keys and indexes of a table, as text that can be compared.</summary>
    private static List<string> Describe(AppDBContext db, string table)
    {
        var lines = new List<string>();
        lines.AddRange(Query(db, $"SELECT name || ' ' || type || ' notnull=' || \"notnull\" || ' pk=' || pk FROM pragma_table_info('{table}') ORDER BY cid"));
        lines.AddRange(Query(db, $"SELECT 'fk ' || \"from\" || '->' || \"table\" || '.' || \"to\" || ' ' || on_delete FROM pragma_foreign_key_list('{table}')"));
        foreach (var index in Query(db, $"SELECT name || ' unique=' || \"unique\" FROM pragma_index_list('{table}') WHERE origin = 'c' ORDER BY name"))
        {
            var name = index.Split(' ')[0];
            lines.Add($"index {index} on {string.Join(",", Query(db, $"SELECT name FROM pragma_index_info('{name}') ORDER BY seqno"))}");
        }
        return lines;
    }

    private static List<string> Query(AppDBContext db, string sql)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open) connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        var rows = new List<string>();
        while (reader.Read()) rows.Add(reader.GetString(0));
        return rows;
    }
}
