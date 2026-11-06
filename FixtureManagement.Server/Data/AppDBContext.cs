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


}
