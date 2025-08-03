using FixtureManagementV3.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
public class AppDBContext(DbContextOptions<AppDBContext> options) : DbContext(options)
{
    public DbSet<FixtureManagementV3.Models.Fixture> Fixture { get; set; } = default!;
    public DbSet<FixtureManagementV3.Models.Team> Team { get; set; } = default!;
    public DbSet<FixtureManagementV3.Models.FixtureAllocation> FixtureAllocation { get; set; } = default!;
    public DbSet<FixtureManagementV3.Models.Pitch> Pitch { get; set; } = default!;
    public DbSet<FixtureManagementV3.Models.Person> Person { get; set; } = default!;
    public DbSet<FixtureManagementV3.Models.TeamContact> TeamContact { get; set; } = default!;
    public DbSet<FixtureManagementV3.Models.Booking> Booking { get; set; } = default!;


}
