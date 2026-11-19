using FixtureManagement.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
public class AppDBContext(DbContextOptions<AppDBContext> options) : DbContext(options)
{
    public DbSet<FixtureManagement.Server.Models.Fixture> Fixture { get; set; } = default!;
    public DbSet<FixtureManagement.Server.Models.Team> Team { get; set; } = default!;
    public DbSet<FixtureManagement.Server.Models.FixtureAllocation> FixtureAllocation { get; set; } = default!;
    public DbSet<FixtureManagement.Server.Models.Pitch> Pitch { get; set; } = default!;
    public DbSet<FixtureManagement.Server.Models.Person> Person { get; set; } = default!;
    public DbSet<FixtureManagement.Server.Models.TeamContact> TeamContact { get; set; } = default!;
    public DbSet<FixtureManagement.Server.Models.Booking> Booking { get; set; } = default!;

    // Fixture reconciliation. Added after the original schema: existing databases get these tables from
    // SchemaUpgrade at start-up (keep its SQL in step with this model – SchemaUpgradeTests checks).
    public DbSet<SourceUpload> SourceUpload { get; set; } = default!;
    public DbSet<UploadedFixture> UploadedFixture { get; set; } = default!;
    public DbSet<ReconciliationDecision> ReconciliationDecision { get; set; } = default!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Only the latest upload per source is kept.
        modelBuilder.Entity<SourceUpload>().HasIndex(u => u.Source).IsUnique();
        modelBuilder.Entity<UploadedFixture>()
            .HasOne(f => f.SourceUpload).WithMany(u => u.Fixtures)
            .HasForeignKey(f => f.SourceUploadId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ReconciliationDecision>().HasIndex(d => d.Key).IsUnique();
    }

}
