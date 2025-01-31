using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using FixtureManagementV3.Models;


public static class EventsAPI
{
    public static void MapEventEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/Event").WithTags(nameof(Event));

        group.MapGet("/", async (AppDBContext db) =>
        {
            //Get fixtures
            IQueryable<Fixture> eventFixture = db.Fixture
                .Include(f => f.Team)
                .Include(f => f.FixtureAllocation)
                    .ThenInclude(fa => fa!.Pitch)
                .Include(f => f.FixtureAllocation)
                .Where(f => (f.FixtureAllocation != null && f.FixtureAllocation.Pitch != null) 
                        && (f.FixtureAllocation.IsConfirmed || true /*User.Identity.IsAuthenticated*/));
                        
            // if (eventId != null)
            // {
            //     eventFixture = eventFixture.Where(f => f.Id == eventId);
            // }
            // if (resourceId != null)
            // {
            //     eventFixture = eventFixture.Where(e => e.FixtureAllocation.PitchId == resourceId);
            // }

            List<Event> events = await eventFixture.Select(f => new Event(f, true /*User.Identity.IsAuthenticated*/)).ToListAsync();

            //Get single event bookings
            // IQueryable<Booking> eventBookingSingle = _context.Booking
            //     .Where(b => !b.IsRecurring);

            // if (eventId != null)
            // {
            //     eventBookingSingle = eventBookingSingle.Where(b => b.Id == eventId);
            // }
            // if (resourceId != null)
            // {
            //     eventBookingSingle = eventBookingSingle.Where(b => b.PitchId == resourceId);
            // }
            // events.AddRange(await eventBookingSingle
            //     .Select(b => new RecurringEvent(b))
            //     .ToListAsync());

            // //Get recurring bookings
            // IQueryable<Booking> eventBookingRecurring = _context.Booking
            //     .Where(b => b.IsRecurring);

            // if (eventId != null)
            // {
            //     eventBookingRecurring = eventBookingRecurring.Where(b => b.Id == eventId);
            // }
            // if (resourceId != null)
            // {
            //     eventBookingRecurring = eventBookingRecurring.Where(b => b.PitchId == resourceId);
            // }
            // events.AddRange(await eventBookingRecurring
            //     .Select(b => new RecurringEvent(b))
            //     .ToListAsync());


            return events;
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
