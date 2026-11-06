using System.Net;
using System.Net.Http.Json;

namespace FixtureManagement.Tests.Api;

/// <summary>Every /api route except the calendar feed and /api/auth must reject anonymous callers with 401.</summary>
public class ApiAuthorizationTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private const string Id = "6f9619ff-8b86-d011-b42d-00cf4fc964ff";

    [Theory]
    [InlineData("GET", "/api/pitches")]
    [InlineData("GET", $"/api/pitches/{Id}")]
    [InlineData("GET", "/api/bookings")]
    [InlineData("GET", $"/api/bookings/{Id}")]
    [InlineData("POST", "/api/bookings")]
    [InlineData("PUT", $"/api/bookings/{Id}")]
    [InlineData("DELETE", $"/api/bookings/{Id}")]
    [InlineData("GET", "/api/people")]
    [InlineData("GET", $"/api/people/{Id}")]
    [InlineData("POST", "/api/people")]
    [InlineData("PUT", $"/api/people/{Id}")]
    [InlineData("DELETE", $"/api/people/{Id}")]
    [InlineData("GET", "/api/teamcontacts")]
    [InlineData("GET", $"/api/teamcontacts/{Id}")]
    [InlineData("POST", "/api/teamcontacts")]
    [InlineData("DELETE", $"/api/teamcontacts/{Id}")]
    [InlineData("GET", "/api/fixtures")]
    [InlineData("GET", $"/api/fixtures/{Id}")]
    [InlineData("POST", "/api/fixtures")]
    [InlineData("POST", "/api/fixtures/batch")]
    [InlineData("PUT", $"/api/fixtures/{Id}")]
    [InlineData("DELETE", $"/api/fixtures/{Id}")]
    [InlineData("DELETE", $"/api/fixtures?teamId={Id}")]
    [InlineData("PUT", $"/api/fixtures/{Id}/allocation")]
    [InlineData("DELETE", $"/api/fixtures/{Id}/allocation")]
    public async Task Returns_401_without_a_token(string method, string url)
    {
        var client = factory.CreateClient(new() { AllowAutoRedirect = false });
        var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (method is "POST" or "PUT")
            request.Content = JsonContent.Create(new { });

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task The_old_entity_based_fixture_api_is_gone()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/Fixture")).StatusCode);
    }

    [Fact]
    public async Task The_calendar_feed_stays_public()
    {
        var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/Event")).StatusCode);
    }
}
