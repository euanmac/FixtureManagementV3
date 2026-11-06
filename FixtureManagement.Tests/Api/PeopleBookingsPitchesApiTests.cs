using System.Net;
using System.Net.Http.Json;
using FixtureManagement.Server.Models;
using Microsoft.AspNetCore.Mvc;

namespace FixtureManagement.Tests.Api;

public class PeopleBookingsPitchesApiTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public async Task People_round_trip_and_validation()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var invalid = await client.PostAsJsonAsync("/api/people", new PersonDto { FirstName = "Only" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Contains("LastName", (await invalid.Content.ReadFromJsonAsync<ValidationProblemDetails>())!.Errors.Keys);

        var create = await client.PostAsJsonAsync("/api/people", new PersonDto { FirstName = "Sam", LastName = "Smith", Email = "sam@example.com" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var person = await create.Content.ReadFromJsonAsync<PersonDto>();

        var update = new PersonDto { FirstName = "Sam", LastName = "Jones", IsRef = true };
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/api/people/{person!.Id}", update)).StatusCode);
        var fetched = await client.GetFromJsonAsync<PersonDto>($"/api/people/{person.Id}");
        Assert.Equal("Sam Jones", fetched!.FullName);
        Assert.True(fetched.IsRef);
        Assert.Contains(await client.GetFromJsonAsync<List<PersonDto>>("/api/people") ?? [], p => p.Id == person.Id);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/people/{person.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/people/{person.Id}")).StatusCode);
    }

    [Fact]
    public async Task Team_contacts_link_people_to_teams()
    {
        var team = new Team { Id = Guid.NewGuid(), Name = "Lions", AgeGroup = AgeGroup.U9, Gender = Gender.Female };
        await factory.SeedAsync(team);
        var client = await factory.CreateAuthenticatedClientAsync();
        var person = await (await client.PostAsJsonAsync("/api/people", new PersonDto { FirstName = "Mia", LastName = "Manager" }))
            .Content.ReadFromJsonAsync<PersonDto>();
        var link = new TeamContactDto { TeamId = team.Id, PersonId = person!.Id, ContactType = ContactType.Manager };

        var create = await client.PostAsJsonAsync("/api/teamcontacts", link);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var contact = await create.Content.ReadFromJsonAsync<TeamContactDto>();
        Assert.Equal("U9 Girls - Lions", contact!.Team.DisplayName);

        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/teamcontacts", link)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsJsonAsync("/api/teamcontacts", new TeamContactDto { TeamId = Guid.NewGuid(), PersonId = person.Id })).StatusCode);

        var forTeam = await client.GetFromJsonAsync<List<TeamContactDto>>($"/api/teamcontacts?teamId={team.Id}");
        Assert.Equal([contact.Id], forTeam!.Select(c => c.Id));
        var personWithTeam = await client.GetFromJsonAsync<PersonDto>($"/api/people/{person.Id}");
        Assert.Equal("U9 Girls - Lions", personWithTeam!.Teams.Single().Team.DisplayName);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/teamcontacts/{contact.Id}")).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<PersonDto>($"/api/people/{person.Id}"))!.Teams);
    }

    [Fact]
    public async Task Bookings_round_trip_and_validation()
    {
        var pitch = new Pitch { Id = Guid.NewGuid(), Name = "Main Pitch", DisplayOrder = 1 };
        await factory.SeedAsync(pitch);
        var client = await factory.CreateAuthenticatedClientAsync();
        var start = new DateTime(2026, 11, 7, 9, 0, 0);

        var backwards = await client.PostAsJsonAsync("/api/bookings",
            new BookingDto { Title = "Training", PitchId = pitch.Id, Start = start, End = start.AddHours(-1) });
        Assert.Equal(HttpStatusCode.BadRequest, backwards.StatusCode);
        Assert.Contains("End", (await backwards.Content.ReadFromJsonAsync<ValidationProblemDetails>())!.Errors.Keys);

        var unknownPitch = await client.PostAsJsonAsync("/api/bookings",
            new BookingDto { Title = "Training", PitchId = Guid.NewGuid(), Start = start, End = start.AddHours(1) });
        Assert.Equal(HttpStatusCode.BadRequest, unknownPitch.StatusCode);

        var create = await client.PostAsJsonAsync("/api/bookings",
            new BookingDto { Title = "Training", PitchId = pitch.Id, Start = start, End = start.AddHours(1), IsRecurring = true });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var booking = await create.Content.ReadFromJsonAsync<BookingDto>();
        Assert.Equal("Main Pitch", booking!.PitchName);
        Assert.Equal(start, booking.Start);

        var update = new BookingDto { Title = "Moved", PitchId = pitch.Id, Start = start.AddDays(1), End = start.AddDays(1).AddHours(1) };
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/api/bookings/{booking.Id}", update)).StatusCode);
        Assert.Equal("Moved", (await client.GetFromJsonAsync<BookingDto>($"/api/bookings/{booking.Id}"))!.Title);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/bookings/{booking.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/bookings/{booking.Id}")).StatusCode);
    }

    [Fact]
    public async Task Pitches_are_listed_in_display_order()
    {
        var second = new Pitch { Id = Guid.NewGuid(), Name = "Zz Second", DisplayOrder = 902 };
        var first = new Pitch { Id = Guid.NewGuid(), Name = "Zz First", DisplayOrder = 901 };
        await factory.SeedAsync(second, first);
        var client = await factory.CreateAuthenticatedClientAsync();

        var pitches = await client.GetFromJsonAsync<List<PitchDto>>("/api/pitches");
        var mine = pitches!.Where(p => p.Name.StartsWith("Zz")).Select(p => p.Name);

        Assert.Equal(["Zz First", "Zz Second"], mine);
        Assert.Equal("Zz First", (await client.GetFromJsonAsync<PitchDto>($"/api/pitches/{first.Id}"))!.Name);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/pitches/{Guid.NewGuid()}")).StatusCode);
    }
}
