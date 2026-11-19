using Microsoft.EntityFrameworkCore;

namespace FixtureManagement.Server.Data;

/// <summary>
/// Adds tables introduced after the original schema to an existing database, at start-up. There are no EF
/// migrations for the app's tables, so this is how production, the test sites and local copies get new tables.
///
/// Only adds tables and indexes (never alters or drops anything), and only to a database that already has the
/// app's tables: a blank database is left to EnsureCreated (the tests), which skips everything if any table exists.
/// The SQL is EF's own (dotnet ef dbcontext script) with IF NOT EXISTS; SchemaUpgradeTests checks it still
/// matches the model.
/// </summary>
public static class SchemaUpgrade
{
    private const string ReconciliationTables = """
        CREATE TABLE IF NOT EXISTS "SourceUpload" (
            "Id" TEXT NOT NULL CONSTRAINT "PK_SourceUpload" PRIMARY KEY,
            "Source" TEXT NOT NULL,
            "SourceName" TEXT NOT NULL,
            "UploadedAt" TEXT NOT NULL,
            "UploadedBy" TEXT NOT NULL,
            "From" TEXT NOT NULL,
            "To" TEXT NOT NULL,
            "TeamIds" TEXT NOT NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_SourceUpload_Source" ON "SourceUpload" ("Source");

        CREATE TABLE IF NOT EXISTS "UploadedFixture" (
            "Id" TEXT NOT NULL CONSTRAINT "PK_UploadedFixture" PRIMARY KEY,
            "SourceUploadId" TEXT NOT NULL,
            "TeamId" TEXT NULL,
            "SourceTeamName" TEXT NOT NULL,
            "Date" TEXT NULL,
            "IsHome" INTEGER NOT NULL,
            "Opponent" TEXT NOT NULL,
            "FixtureType" INTEGER NOT NULL,
            "Competition" TEXT NOT NULL,
            "SourceFixtureId" TEXT NULL,
            "Status" TEXT NOT NULL,
            CONSTRAINT "FK_UploadedFixture_SourceUpload_SourceUploadId" FOREIGN KEY ("SourceUploadId") REFERENCES "SourceUpload" ("Id") ON DELETE CASCADE
        );
        CREATE INDEX IF NOT EXISTS "IX_UploadedFixture_SourceUploadId" ON "UploadedFixture" ("SourceUploadId");

        CREATE TABLE IF NOT EXISTS "ReconciliationDecision" (
            "Id" TEXT NOT NULL CONSTRAINT "PK_ReconciliationDecision" PRIMARY KEY,
            "Key" TEXT NOT NULL,
            "TeamId" TEXT NOT NULL,
            "Description" TEXT NOT NULL,
            "DecidedAt" TEXT NOT NULL,
            "DecidedBy" TEXT NOT NULL
        );
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_ReconciliationDecision_Key" ON "ReconciliationDecision" ("Key");
        """;

    /// <returns>True if the database had the app's tables (so the upgrade ran), false if it was left alone.</returns>
    public static bool Apply(AppDBContext db)
    {
        var hasAppTables = db.Database
            .SqlQueryRaw<int>("""SELECT COUNT(*) AS "Value" FROM sqlite_master WHERE type = 'table' AND name = 'Fixture'""")
            .AsEnumerable().Single() > 0;
        if (!hasAppTables) return false;

        db.Database.ExecuteSqlRaw(ReconciliationTables);
        return true;
    }
}
