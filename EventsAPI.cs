using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using FixtureManagementV3.Models;


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
        })
        .WithName("GetAllEvents")
        .WithOpenApi();

        // group.MapGet("/{id}", async Task<Results<Ok<Event>, NotFound>> (System.Guid id, API db) =>
        // {
        //     return await db.Event.AsNoTracking()
        //         .FirstOrDefaultAsync(model => model.Id == id)
        //         is Event model
        //             ? TypedResults.Ok(model)
        //             : TypedResults.NotFound();
        // })
        // .WithName("GetEventById")
        // .WithOpenApi();

        // group.MapPut("/{id}", async Task<Results<Ok, NotFound>> (System.Guid id, Event event, API db) =>
        // {
        //     var affected = await db.Event
        //         .Where(model => model.Id == id)
        //         .ExecuteUpdateAsync(setters => setters
        //         .SetProperty(m => m.Title, event.Title)
        //         .SetProperty(m => m.Start, event.Start)
        //         .SetProperty(m => m.End, event.End)
        //         .SetProperty(m => m.ResourceId, event.ResourceId)
        //         .SetProperty(m => m.RGBColor, event.RGBColor)
        //         .SetProperty(m => m.StartEditable, event.StartEditable)
        //         .SetProperty(m => m.DurationEditable, event.DurationEditable)
        //         .SetProperty(m => m.ResourceEditable, event.ResourceEditable)
        //         .SetProperty(m => m.Url, event.Url)
        //         .SetProperty(m => m.BorderColor, event.BorderColor)
        //         .SetProperty(m => m.BackgroundColor, event.BackgroundColor)
        // );

        //     return affected == 1 ? TypedResults.Ok() : TypedResults.NotFound();
        // })
        // .WithName("UpdateEvent")
        // .WithOpenApi();

    //     group.MapPost("/", async (Event event, API db) =>
    //     {
    //         db.Event.Add(event);
    //         await db.SaveChangesAsync();
    //         return TypedResults.Created($"/api/Event/{event.Id}",event);
    //     })
    //     .WithName("CreateEvent")
    //     .WithOpenApi();

    //     group.MapDelete("/{id}", async Task<Results<Ok, NotFound>> (System.Guid id, API db) =>
    //     {
    //         var affected = await db.Event
    //             .Where(model => model.Id == id)
    //             .ExecuteDeleteAsync();

    //         return affected == 1 ? TypedResults.Ok() : TypedResults.NotFound();
    //     })
    //     .WithName("DeleteEvent")
    //     .WithOpenApi();
    }
}
