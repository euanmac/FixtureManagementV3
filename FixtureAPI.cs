using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using FixtureManagementV3.Models;
using Microsoft.AspNetCore.Mvc;


public static class FixturesAPI
{
    public static void MapFixtureEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/Fixture").WithTags(nameof(Fixture));

        group.MapGet("/", async (AppDBContext db, HttpContext context) =>
        {
            bool IsAuthenticated = context.User?.Identity?.IsAuthenticated ?? false;
            //Get fixtures
            IQueryable<Fixture> fixture = db.Fixture
                .Include(f => f.Team)
                .Include(f => f.FixtureAllocation)
                    .ThenInclude(fa => fa!.Pitch)
                .Include(f => f.FixtureAllocation)
                .Where(f => (f.FixtureAllocation != null && f.FixtureAllocation.Pitch != null)
                        && (f.FixtureAllocation.IsConfirmed || IsAuthenticated));

            return fixture.Cast<object>().ToList();
        });

        // Add a single fixture
        group.MapPut("/", async ([FromBody] Fixture newFixture, AppDBContext db) =>
        {
            db.Fixture.Add(newFixture);
            await db.SaveChangesAsync();
            return Results.Created($"/api/Fixture/{newFixture.Id}", newFixture);
        });

        // Add a list of fixtures
        group.MapPut("/list", async ([FromBody] List<Fixture> newFixtures, AppDBContext db) =>
        {
            db.Fixture.AddRange(newFixtures);
            await db.SaveChangesAsync();
            return Results.Created("/api/Fixture/list", newFixtures);
        });
    }
}