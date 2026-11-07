using FixtureManagement.ApiClient;
using FixtureManagement.Server.Models;
using FixtureManagement.Tests.Api;

namespace FixtureManagement.Tests.ApiClient;

public class ApiTeamServiceTests(TestWebApplicationFactory factory) : ApiClientTestBase(factory)
{
    [Fact]
    public async Task Create_get_list_update_delete()
    {
        var teams = new ApiTeamService(Api());

        var created = await teams.CreateTeamAsync(new TeamDto { Name = "Client Lions", AgeGroup = AgeGroup.U11, Gender = Gender.Female, League = League.BucksGirls, RefRequired = true });
        var loaded = await teams.GetTeamAsync(created.Id);

        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.Equal("U11 Girls - Client Lions", loaded!.DisplayName);
        Assert.True(loaded.RefRequired);
        Assert.Contains(await teams.GetTeamsAsync(), t => t.Id == created.Id);

        loaded.Name = "Client Tigers";
        Assert.True(await teams.UpdateTeamAsync(created.Id, loaded));
        Assert.Equal("Client Tigers", (await teams.GetTeamAsync(created.Id))!.Name);

        Assert.True(await teams.DeleteTeamAsync(created.Id));
        Assert.Null(await teams.GetTeamAsync(created.Id));
    }

    [Fact]
    public async Task Unknown_ids_give_null_and_false()
    {
        var teams = new ApiTeamService(Api());
        var id = Guid.NewGuid();

        Assert.Null(await teams.GetTeamAsync(id));
        Assert.False(await teams.UpdateTeamAsync(id, new TeamDto { Name = "X" }));
        Assert.False(await teams.DeleteTeamAsync(id));
    }

    [Fact]
    public async Task Server_validation_errors_surface_as_ApiValidationException()
    {
        var teams = new ApiTeamService(Api());

        var error = await Assert.ThrowsAsync<ApiValidationException>(() => teams.CreateTeamAsync(new TeamDto { Name = "" }));

        Assert.Contains("Name", error.Errors.Keys);
    }
}

public class ApiPitchAndBookingServiceTests(TestWebApplicationFactory factory) : ApiClientTestBase(factory)
{
    [Fact]
    public async Task Pitches_list_in_display_order_and_get_by_id()
    {
        var b = new Pitch { Id = Guid.NewGuid(), Name = "ZClient B", DisplayOrder = 802 };
        var a = new Pitch { Id = Guid.NewGuid(), Name = "ZClient A", DisplayOrder = 801 };
        await Factory.SeedAsync(b, a);
        var pitches = new ApiPitchService(Api());

        var mine = (await pitches.GetPitchesAsync()).Where(p => p.Name.StartsWith("ZClient")).Select(p => p.Name);

        Assert.Equal(["ZClient A", "ZClient B"], mine);
        Assert.Equal(new PitchDto { Id = a.Id, Name = "ZClient A", DisplayOrder = 801 }, await pitches.GetPitchAsync(a.Id));
        Assert.Null(await pitches.GetPitchAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Bookings_create_validate_update_delete()
    {
        var pitch = new Pitch { Id = Guid.NewGuid(), Name = "Booking Pitch", DisplayOrder = 1 };
        await Factory.SeedAsync(pitch);
        var bookings = new ApiBookingService(Api());
        var start = new DateTime(2027, 3, 6, 9, 0, 0);

        var unknownPitch = await bookings.CreateBookingAsync(new BookingDto { Title = "X", PitchId = Guid.NewGuid(), Start = start, End = start.AddHours(1) });
        var backwards = await bookings.CreateBookingAsync(new BookingDto { Title = "X", PitchId = pitch.Id, Start = start, End = start.AddHours(-1) });
        var created = await bookings.CreateBookingAsync(new BookingDto { Title = "Training", PitchId = pitch.Id, Start = start, End = start.AddHours(1), IsRecurring = true });

        Assert.Equal(ServiceStatus.Invalid, unknownPitch.Status);
        Assert.Contains("PitchId", unknownPitch.Errors.Keys);
        Assert.Equal(ServiceStatus.Invalid, backwards.Status);
        Assert.Contains("End", backwards.Errors.Keys);
        Assert.True(created.Succeeded);
        Assert.Equal("Booking Pitch", created.Value!.PitchName);
        Assert.Equal(start, created.Value.Start);

        var edit = created.Value;
        edit.Title = "Moved";
        Assert.True((await bookings.UpdateBookingAsync(edit.Id, edit)).Succeeded);
        Assert.Equal("Moved", (await bookings.GetBookingAsync(edit.Id))!.Title);
        Assert.Equal(ServiceStatus.NotFound, (await bookings.UpdateBookingAsync(Guid.NewGuid(), edit)).Status);

        Assert.True(await bookings.DeleteBookingAsync(edit.Id));
        Assert.False(await bookings.DeleteBookingAsync(edit.Id));
    }
}

public class ApiPeopleAndContactServiceTests(TestWebApplicationFactory factory) : ApiClientTestBase(factory)
{
    [Fact]
    public async Task People_create_update_delete()
    {
        var people = new ApiPersonService(Api());

        var created = await people.CreatePersonAsync(new PersonDto { FirstName = "Cli", LastName = "Ent", Email = "client@example.com", IsRef = true });
        created.LastName = "Entwistle";
        Assert.True(await people.UpdatePersonAsync(created.Id, created));

        var loaded = await people.GetPersonAsync(created.Id);
        Assert.Equal("Cli Entwistle", loaded!.FullName);
        Assert.True(loaded.IsRef);
        Assert.Contains(await people.GetPeopleAsync(), p => p.Id == created.Id);

        Assert.True((await people.DeletePersonAsync(created.Id)).Succeeded);
        Assert.Equal(ServiceStatus.NotFound, (await people.DeletePersonAsync(created.Id)).Status);
        await Assert.ThrowsAsync<ApiValidationException>(() => people.CreatePersonAsync(new PersonDto { FirstName = "Only" }));
    }

    [Fact]
    public async Task Team_contacts_create_conflict_invalid_filter_delete()
    {
        var team = new Team { Id = Guid.NewGuid(), Name = "Contact Team", AgeGroup = AgeGroup.U9, Gender = Gender.Female };
        await Factory.SeedAsync(team);
        var person = await new ApiPersonService(Api()).CreatePersonAsync(new PersonDto { FirstName = "Mia", LastName = "Manager" });
        var contacts = new ApiTeamContactService(Api());
        var link = new TeamContactDto { TeamId = team.Id, PersonId = person.Id, ContactType = ContactType.Coach };

        var created = await contacts.CreateTeamContactAsync(link);
        var duplicate = await contacts.CreateTeamContactAsync(link);
        var noTeam = await contacts.CreateTeamContactAsync(new TeamContactDto { TeamId = Guid.NewGuid(), PersonId = person.Id });

        Assert.True(created.Succeeded);
        Assert.Equal("U9 Girls - Contact Team", created.Value!.Team.DisplayName);
        Assert.Equal(ServiceStatus.Conflict, duplicate.Status);
        Assert.Contains("already", duplicate.Message);
        Assert.Equal(ServiceStatus.Invalid, noTeam.Status);
        Assert.Contains("TeamId", noTeam.Errors.Keys);

        Assert.Equal([created.Value.Id], (await contacts.GetTeamContactsAsync(teamId: team.Id)).Select(c => c.Id));
        Assert.Equal([created.Value.Id], (await contacts.GetTeamContactsAsync(team.Id, person.Id)).Select(c => c.Id));
        Assert.Equal("Mia Manager", (await contacts.GetTeamContactAsync(created.Value.Id))!.FullName);

        Assert.True(await contacts.DeleteTeamContactAsync(created.Value.Id));
        Assert.False(await contacts.DeleteTeamContactAsync(created.Value.Id));
    }
}

public class ApiFixtureServiceTests(TestWebApplicationFactory factory) : ApiClientTestBase(factory)
{
    private static readonly DateOnly Nov7 = new(2026, 11, 7);

    private async Task<(ApiFixtureService Fixtures, Guid TeamId, Guid PitchId)> SetUpAsync()
    {
        var team = new Team { Id = Guid.NewGuid(), Name = "Fixture Lions", AgeGroup = AgeGroup.U10, Gender = Gender.Male };
        var pitch = new Pitch { Id = Guid.NewGuid(), Name = "Fixture Pitch", DisplayOrder = 1 };
        await Factory.SeedAsync(team, pitch);
        return (new ApiFixtureService(Api()), team.Id, pitch.Id);
    }

    private static FixtureDto New(Guid teamId, DateOnly date, string opponent) =>
        new() { TeamId = teamId, Date = date, Opponent = opponent, IsHome = true, FixtureType = FixtureType.League };

    [Fact]
    public async Task Create_get_query_update_delete()
    {
        var (fixtures, teamId, _) = await SetUpAsync();

        var created = await fixtures.CreateFixtureAsync(New(teamId, Nov7, "Client Rovers"));
        Assert.True(created.Succeeded);
        Assert.Equal(teamId, created.Value!.TeamId);
        Assert.Equal("U10 Boys - Fixture Lions", created.Value.Team.DisplayName);

        var inRange = await fixtures.GetFixturesAsync(new FixtureQuery { From = Nov7, To = Nov7, TeamId = teamId });
        var outOfRange = await fixtures.GetFixturesAsync(new FixtureQuery { From = Nov7.AddDays(1), TeamId = teamId });
        Assert.Equal(["Client Rovers"], inRange.Select(f => f.Opponent));
        Assert.Empty(outOfRange);

        var edit = (await fixtures.GetFixtureAsync(created.Value.Id))!;
        edit.Opponent = "Client Edited";
        Assert.True((await fixtures.UpdateFixtureAsync(edit.Id, edit)).Succeeded);
        Assert.Equal("Client Edited", (await fixtures.GetFixtureAsync(edit.Id))!.Opponent);

        Assert.True(await fixtures.DeleteFixtureAsync(edit.Id));
        Assert.Null(await fixtures.GetFixtureAsync(edit.Id));
        Assert.False(await fixtures.DeleteFixtureAsync(edit.Id));
    }

    [Fact]
    public async Task Errors_come_back_as_service_results()
    {
        var (fixtures, teamId, _) = await SetUpAsync();

        var unknownTeam = await fixtures.CreateFixtureAsync(New(Guid.NewGuid(), Nov7, "X"));
        var badBatch = await fixtures.CreateFixturesAsync([New(teamId, Nov7, "Fine"), New(teamId, Nov7, "")]);
        var missing = await fixtures.UpdateFixtureAsync(Guid.NewGuid(), New(teamId, Nov7, "X"));

        Assert.Equal(ServiceStatus.Invalid, unknownTeam.Status);
        Assert.Contains("TeamId", unknownTeam.Errors.Keys);
        Assert.Equal(ServiceStatus.Invalid, badBatch.Status);
        Assert.Contains("[1].Opponent", badBatch.Errors.Keys);
        Assert.Equal(ServiceStatus.NotFound, missing.Status);
    }

    [Fact]
    public async Task Batch_allocation_confirm_and_bulk_delete()
    {
        var (fixtures, teamId, pitchId) = await SetUpAsync();

        var batch = await fixtures.CreateFixturesAsync([New(teamId, Nov7, "A"), New(teamId, Nov7.AddDays(7), "B")]);
        Assert.Equal(2, batch.Value!.Count);
        var a = batch.Value[0].Id;

        var allocation = await fixtures.SetAllocationAsync(a, new FixtureAllocationDto { PitchId = pitchId, Start = new TimeOnly(9, 30), Duration = TimeSpan.FromMinutes(90) });
        Assert.True(allocation.Succeeded);
        Assert.Equal("Fixture Pitch", allocation.Value!.PitchName);
        Assert.Equal(new TimeOnly(11, 0), allocation.Value.End);
        Assert.Equal(ServiceStatus.Invalid, (await fixtures.SetAllocationAsync(a, new FixtureAllocationDto { PitchId = Guid.NewGuid(), Start = new TimeOnly(9, 30), Duration = TimeSpan.FromHours(1) })).Status);
        Assert.Equal(ServiceStatus.NotFound, (await fixtures.SetAllocationAsync(Guid.NewGuid(), new FixtureAllocationDto { PitchId = pitchId, Start = new TimeOnly(9, 30), Duration = TimeSpan.FromHours(1) })).Status);

        Assert.Equal(1, await fixtures.ConfirmAllocationsAsync([a]));
        Assert.True((await fixtures.GetFixtureAsync(a))!.Allocation!.IsConfirmed);
        Assert.Equal(0, await fixtures.ConfirmAllocationsAsync([]));

        Assert.True(await fixtures.RemoveAllocationAsync(a));
        Assert.False(await fixtures.RemoveAllocationAsync(a));

        Assert.Equal(1, await fixtures.DeleteFixturesAsync(teamId, after: Nov7));   // strictly after: only "B"
        Assert.Equal(1, await fixtures.DeleteFixturesAsync(teamId, after: null));
        await Assert.ThrowsAsync<ArgumentException>(() => fixtures.DeleteFixturesAsync(null, null));
    }
}

public class ApiClientAuthTests(TestWebApplicationFactory factory) : ApiClientTestBase(factory)
{
    [Fact]
    public async Task Wrong_password_is_refused_and_stays_signed_out()
    {
        var session = new ApiSession();

        Assert.False(await new ApiAuthClient(Factory.CreateClient(), session).LoginAsync(TestWebApplicationFactory.UserEmail, "wrong"));
        Assert.False(session.IsSignedIn);
    }

    [Fact]
    public async Task Not_signed_in_throws_ApiAuthenticationException()
    {
        var signedOut = new ApiSession();
        var teams = new ApiTeamService(Factory.CreateDefaultClient(new BearerTokenHandler(signedOut, new ApiAuthClient(Factory.CreateClient(), signedOut))));

        await Assert.ThrowsAsync<ApiAuthenticationException>(() => teams.GetTeamsAsync());
    }

    [Fact]
    public async Task An_expired_access_token_is_refreshed_automatically()
    {
        Assert.True(Session.IsSignedIn);
        var refreshToken = Session.RefreshToken!;

        // Throw away the access token: the handler must refresh using the refresh token, then succeed.
        Session.Restore(TestWebApplicationFactory.UserEmail, refreshToken);
        var teams = await new ApiTeamService(Api()).GetTeamsAsync();

        Assert.NotNull(teams);
        Assert.NotNull(Session.AccessToken);
        Assert.NotEqual(refreshToken, Session.RefreshToken);   // refresh tokens rotate
    }

    [Fact]
    public async Task A_rejected_token_is_refreshed_once_and_the_request_retried()
    {
        // A token that looks current but the server won't accept: the handler gets a 401, refreshes, retries.
        var http = Factory.CreateDefaultClient(new BearerTokenHandler(Session, Auth()));
        typeof(ApiSession).GetProperty(nameof(ApiSession.AccessToken))!.SetValue(Session, "not-a-real-token");

        var created = await new ApiPersonService(http).CreatePersonAsync(new PersonDto { FirstName = "Re", LastName = "Tried" });

        Assert.Equal("Re Tried", created.FullName);
        Assert.NotEqual("not-a-real-token", Session.AccessToken);
    }

    [Fact]
    public async Task An_unusable_refresh_token_signs_the_user_out()
    {
        Session.Restore(TestWebApplicationFactory.UserEmail, "not-a-real-refresh-token");

        await Assert.ThrowsAsync<ApiAuthenticationException>(() => new ApiTeamService(Api()).GetTeamsAsync());

        Assert.False(Session.IsSignedIn);
    }
}
