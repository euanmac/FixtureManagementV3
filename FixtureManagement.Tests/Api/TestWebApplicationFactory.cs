using System.Net.Http.Headers;
using System.Net.Http.Json;
using FixtureManagement.Server.Data;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FixtureManagement.Tests.Api;

/// <summary>
/// Starts the real website in memory against a throwaway SQLite file (never Data/local.db),
/// with one confirmed user that tests can log in as.
/// </summary>
public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string UserEmail = "tester@example.com";
    public const string UserPassword = "Test-Passw0rd!";

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"fm-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:DefaultConnection", $"DataSource={_dbPath}");
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        var services = scope.ServiceProvider;

        // Both contexts share one database file: create the app tables, then add the Identity tables.
        services.GetRequiredService<IDbContextFactory<AppDBContext>>().CreateDbContext().Database.EnsureCreated();
        services.GetRequiredService<AuthenticationDbContext>().GetService<IRelationalDatabaseCreator>().CreateTables();

        // The site requires confirmed accounts, so create the user directly rather than via /register.
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = UserEmail, Email = UserEmail, EmailConfirmed = true };
        var result = users.CreateAsync(user, UserPassword).GetAwaiter().GetResult();
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));

        return host;
    }

    /// <summary>A client that sends a bearer token obtained from POST /api/auth/login.</summary>
    public async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = UserEmail, password = UserPassword });
        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<AccessTokenResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        foreach (var file in new[] { _dbPath, _dbPath + "-wal", _dbPath + "-shm" })
            try { File.Delete(file); } catch (IOException) { }
    }
}
