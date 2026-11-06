using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FixtureManagement.Server.Data;

public class AuthenticationDbContext(DbContextOptions<AuthenticationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
}
