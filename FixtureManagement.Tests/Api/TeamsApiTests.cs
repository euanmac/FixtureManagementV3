using System.Net;
using System.Net.Http.Json;
using FixtureManagement.Shared.Dtos;
using FixtureManagement.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace FixtureManagement.Tests.Api;

public class TeamsApiTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    [Theory]
    [InlineData("GET", "/api/teams")]
    [InlineData("GET", "/api/teams/6f9619ff-8b86-d011-b42d-00cf4fc964ff")]
    [InlineData("POST", "/api/teams")]
    [InlineData("PUT", "/api/teams/6f9619ff-8b86-d011-b42d-00cf4fc964ff")]
    [InlineData("DELETE", "/api/teams/6f9619ff-8b86-d011-b42d-00cf4fc964ff")]
    public async Task Every_endpoint_returns_401_without_a_token(string method, string url)
    {
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (method is "POST" or "PUT")
            request.Content = JsonContent.Create(NewTeam("Lions"));

        var response = await client.SendAsync(request);

        // Not a 302 to the login page: API clients need a status code.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_with_a_wrong_password_is_rejected()
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { email = TestWebApplicationFactory.UserEmail, password = "wrong" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Create_get_update_delete_round_trip()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        // Create
        var createResponse = await client.PostAsJsonAsync("/api/teams", NewTeam("Round Trip"));
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<TeamDto>();
        Assert.NotNull(created);
        Assert.Equal($"/api/teams/{created.Id}", createResponse.Headers.Location?.OriginalString);

        // Get one, and it's in the list
        var fetched = await client.GetFromJsonAsync<TeamDto>($"/api/teams/{created.Id}");
        Assert.Equal("Round Trip", fetched?.Name);
        var all = await client.GetFromJsonAsync<List<TeamDto>>("/api/teams");
        Assert.Contains(all!, t => t.Id == created.Id);

        // Update
        var update = NewTeam("Renamed");
        update.Division = Division.Three;
        var putResponse = await client.PutAsJsonAsync($"/api/teams/{created.Id}", update);
        Assert.Equal(HttpStatusCode.NoContent, putResponse.StatusCode);
        var updated = await client.GetFromJsonAsync<TeamDto>($"/api/teams/{created.Id}");
        Assert.Equal("Renamed", updated?.Name);
        Assert.Equal(Division.Three, updated?.Division);

        // Delete
        var deleteResponse = await client.DeleteAsync($"/api/teams/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        var afterDelete = await client.GetAsync($"/api/teams/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);
    }

    [Fact]
    public async Task Unknown_ids_return_404()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var id = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/teams/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync($"/api/teams/{id}", NewTeam("X"))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/teams/{id}")).StatusCode);
    }

    [Fact]
    public async Task Create_without_a_name_returns_a_validation_problem()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/teams", NewTeam(""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        Assert.Contains("Name", problem!.Errors.Keys);
    }

    [Fact]
    public async Task Update_without_a_name_returns_a_validation_problem_and_changes_nothing()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var created = await (await client.PostAsJsonAsync("/api/teams", NewTeam("Keep Me"))).Content.ReadFromJsonAsync<TeamDto>();

        var response = await client.PutAsJsonAsync($"/api/teams/{created!.Id}", NewTeam(""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var unchanged = await client.GetFromJsonAsync<TeamDto>($"/api/teams/{created.Id}");
        Assert.Equal("Keep Me", unchanged?.Name);
    }

    private static TeamDto NewTeam(string name) => new()
    {
        Name = name,
        AgeGroup = AgeGroup.U10,
        Gender = Gender.Male,
        League = League.SouthBucksMini,
        Division = Division.One,
    };
}
