using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using FixtureManagementV3.Models;
using Microsoft.AspNetCore.Mvc;


public static class EventsAPI
{
    public static void MapEventEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/Event").WithTags(nameof(Event));

        group.MapGet("/", async (AppDBContext db, HttpContext context) =>
        {
            bool IsAuthenticated = context.User?.Identity?.IsAuthenticated ?? false; 
            //Get fixtures
            IQueryable<Fixture> eventFixture = db.Fixture
                .Include(f => f.Team)
                .Include(f => f.FixtureAllocation)
                    .ThenInclude(fa => fa!.Pitch)
                .Include(f => f.FixtureAllocation)
                .Where(f => (f.FixtureAllocation != null && f.FixtureAllocation.Pitch != null) 
                        && (f.FixtureAllocation.IsConfirmed || IsAuthenticated));
                        
            
            List<Event> events = await eventFixture.Select(f => new Event(f, IsAuthenticated)).ToListAsync();

            //Get single event bookings and add to event list
            IQueryable<Booking> eventBookingSingle = db.Booking
                .Where(b => !b.IsRecurring);

            events.AddRange(await eventBookingSingle
                .Select(b => new Event(b))
                .ToListAsync());

            // //Get recurring bookings and add to event list
            IQueryable<Booking> eventBookingRecurring = db.Booking
                .Where(b => b.IsRecurring);

            events.AddRange(await eventBookingRecurring
                .Select(b => new RecurringEvent(b))
                .ToListAsync());

            return events.Cast<object>().ToList();
        });
        // .WithName("GetAllEvents")
        // .WithOpenApi();


        group.MapGet("/{id}", async (Guid id, AppDBContext db, HttpContext context) =>
        {
            Event @event = await db.Fixture
                .Include(f => f.Team)
                .Include(f => f.FixtureAllocation)
                    .ThenInclude(fa => fa!.Pitch)
                .Include(f => f.FixtureAllocation)
                .Where(f => f.Id == id)
                .Select(f => new FixtureManagementV3.Models.Event(f, true))
                .FirstAsync();

            return @event;
        });
 
        group.MapPut("/{id}",  async (Guid id, FixtureManagementV3.Models.Event @event, AppDBContext db) =>
        {
                FixtureAllocation allocation = await db.FixtureAllocation
                    .FirstAsync(fa => fa.FixtureId == id);
                allocation.Start = TimeOnly.FromDateTime(@event.Start.ToLocalTime());
                allocation.Duration = @event.End - @event.Start;
                allocation.PitchId = @event.ResourceId;
                db.Attach(allocation).State = EntityState.Modified;
                await db.SaveChangesAsync();
            return ;
        }) ;
        // .WithOpenApi();
        
   
    }
}
