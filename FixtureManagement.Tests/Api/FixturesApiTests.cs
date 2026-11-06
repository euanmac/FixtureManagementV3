using System.Net;
using System.Net.Http.Json;
using FixtureManagement.Server.Models;
using Microsoft.AspNetCore.Mvc;

namespace FixtureManagement.Tests.Api;

public class FixturesApiTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private async Task<(HttpClient Client, Guid TeamId, Guid PitchId)> SetUpAsync()
    {
        var team = new Team { Id = Guid.NewGuid(), Name = "Lions", AgeGroup = AgeGroup.U10, Gender = Gender.Male };
        var pitch = new Pitch { Id = Guid.NewGuid(), Name = "Main Pitch", DisplayOrder = 1 };
        await factory.SeedAsync(team, pitch);
        return (await factory.CreateAuthenticatedClientAsync(), team.Id, pitch.Id);
    }

    private static FixtureDto NewFixture(Guid teamId, DateOnly date, string opponent = "Rivals") => new()
    {
        TeamId = teamId, Date = date, IsHome = true, Opponent = opponent, FixtureType = FixtureType.League,
    };

    private static readonly DateOnly Nov7 = new(2026, 11, 7);

    [Fact]
    public async Task Create_get_update_delete_round_trip()
    {
        var (client, teamId, _) = await SetUpAsync();

        var create = await client.PostAsJsonAsync("/api/fixtures", NewFixture(teamId, Nov7));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<FixtureDto>();
        Assert.Equal("U10 Boys - Lions", created!.Team.DisplayName);
        Assert.Equal($"/api/fixtures/{created.Id}", create.Headers.Location?.OriginalString);

        var update = NewFixture(teamId, Nov7.AddDays(1), "Renamed");
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/api/fixtures/{created.Id}", update)).StatusCode);
        var fetched = await client.GetFromJsonAsync<FixtureDto>($"/api/fixtures/{created.Id}");
        Assert.Equal("Renamed", fetched!.Opponent);
        Assert.Equal(Nov7.AddDays(1), fetched.Date);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/fixtures/{created.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/fixtures/{created.Id}")).StatusCode);
    }

    [Fact]
    public async Task A_fetched_fixture_can_be_edited_and_sent_straight_back()
    {
        // The point of one DTO per area: GET it, change a field, PUT the same object.
        var (client, teamId, _) = await SetUpAsync();
        var id = (await (await client.PostAsJsonAsync("/api/fixtures", NewFixture(teamId, Nov7))).Content.ReadFromJsonAsync<FixtureDto>())!.Id;

        var fixture = await client.GetFromJsonAsync<FixtureDto>($"/api/fixtures/{id}");
        Assert.Equal(teamId, fixture!.TeamId);
        fixture.Opponent = "Edited";
        fixture.FixtureType = FixtureType.Cup;

        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/api/fixtures/{id}", fixture)).StatusCode);
        var saved = await client.GetFromJsonAsync<FixtureDto>($"/api/fixtures/{id}");
        Assert.Equal("Edited", saved!.Opponent);
        Assert.Equal(FixtureType.Cup, saved.FixtureType);
    }

    [Fact]
    public async Task List_filters_by_query_string()
    {
        var (client, teamId, _) = await SetUpAsync();
        await client.PostAsJsonAsync("/api/fixtures/batch", new[]
        {
            NewFixture(teamId, new DateOnly(2027, 1, 9), "In range"),
            NewFixture(teamId, new DateOnly(2027, 1, 30), "Out of range"),
        });

        var list = await client.GetFromJsonAsync<List<FixtureDto>>($"/api/fixtures?from=2027-01-01&to=2027-01-15&teamId={teamId}");

        Assert.Equal(["In range"], list!.Select(f => f.Opponent));
    }

    [Fact]
    public async Task Unknown_team_is_a_validation_problem()
    {
        var (client, _, _) = await SetUpAsync();

        var response = await client.PostAsJsonAsync("/api/fixtures", NewFixture(Guid.NewGuid(), Nov7));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains("TeamId", problem!.Errors.Keys);
    }

    [Fact]
    public async Task Batch_validation_errors_name_the_row()
    {
        var (client, teamId, _) = await SetUpAsync();

        var response = await client.PostAsJsonAsync("/api/fixtures/batch", new[]
        {
            NewFixture(teamId, Nov7, "Fine"),
            NewFixture(teamId, Nov7, ""),
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains("[1].Opponent", problem!.Errors.Keys);
    }

    [Fact]
    public async Task Allocation_can_be_set_and_removed()
    {
        var (client, teamId, pitchId) = await SetUpAsync();
        var fixture = await (await client.PostAsJsonAsync("/api/fixtures", NewFixture(teamId, Nov7))).Content.ReadFromJsonAsync<FixtureDto>();
        var allocation = new FixtureAllocationDto { PitchId = pitchId, Start = new TimeOnly(9, 30), Duration = TimeSpan.FromMinutes(90), IsConfirmed = true };

        var set = await client.PutAsJsonAsync($"/api/fixtures/{fixture!.Id}/allocation", allocation);
        Assert.Equal(HttpStatusCode.OK, set.StatusCode);
        var saved = await set.Content.ReadFromJsonAsync<FixtureAllocationDto>();
        Assert.Equal("Main Pitch", saved!.PitchName);
        Assert.Equal(new TimeOnly(11, 0), saved.End);

        var withAllocation = await client.GetFromJsonAsync<FixtureDto>($"/api/fixtures/{fixture.Id}");
        Assert.True(withAllocation!.IsAllocated);
        Assert.True(withAllocation.Allocation!.IsConfirmed);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/fixtures/{fixture.Id}/allocation")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/fixtures/{fixture.Id}/allocation")).StatusCode);
        Assert.Null((await client.GetFromJsonAsync<FixtureDto>($"/api/fixtures/{fixture.Id}"))!.Allocation);
    }

    [Fact]
    public async Task Allocation_errors()
    {
        var (client, teamId, pitchId) = await SetUpAsync();
        var fixture = await (await client.PostAsJsonAsync("/api/fixtures", NewFixture(teamId, Nov7))).Content.ReadFromJsonAsync<FixtureDto>();

        var noDuration = await client.PutAsJsonAsync($"/api/fixtures/{fixture!.Id}/allocation",
            new FixtureAllocationDto { PitchId = pitchId, Start = new TimeOnly(9, 30) });
        var noPitch = await client.PutAsJsonAsync($"/api/fixtures/{fixture.Id}/allocation",
            new FixtureAllocationDto { PitchId = Guid.NewGuid(), Start = new TimeOnly(9, 30), Duration = TimeSpan.FromHours(1) });
        var noFixture = await client.PutAsJsonAsync($"/api/fixtures/{Guid.NewGuid()}/allocation",
            new FixtureAllocationDto { PitchId = pitchId, Start = new TimeOnly(9, 30), Duration = TimeSpan.FromHours(1) });

        Assert.Equal(HttpStatusCode.BadRequest, noDuration.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noPitch.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, noFixture.StatusCode);
    }

    [Fact]
    public async Task Bulk_delete_needs_a_filter_and_reports_the_count()
    {
        var (client, teamId, _) = await SetUpAsync();
        await client.PostAsJsonAsync("/api/fixtures/batch", new[] { NewFixture(teamId, Nov7, "A"), NewFixture(teamId, Nov7, "B") });

        Assert.Equal(HttpStatusCode.BadRequest, (await client.DeleteAsync("/api/fixtures")).StatusCode);

        var response = await client.DeleteAsync($"/api/fixtures?teamId={teamId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, (await response.Content.ReadFromJsonAsync<DeleteCount>())!.Deleted);
    }

    private record DeleteCount(int Deleted);
}
