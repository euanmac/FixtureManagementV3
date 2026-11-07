using FixtureManagement.ApiClient;
using Microsoft.Extensions.DependencyInjection;

namespace FixtureManagement.Tests.ApiClient;

public class ApiClientRegistrationTests
{
    [Fact]
    public void AddFixtureManagementApi_registers_every_service_against_the_server_address()
    {
        var services = new ServiceCollection();
        services.AddFixtureManagementApi(new Uri("https://fixtures.example.org"));   // no trailing slash on purpose
        using var provider = services.BuildServiceProvider();

        Assert.IsType<ApiTeamService>(provider.GetRequiredService<ITeamService>());
        Assert.IsType<ApiPitchService>(provider.GetRequiredService<IPitchService>());
        Assert.IsType<ApiBookingService>(provider.GetRequiredService<IBookingService>());
        Assert.IsType<ApiPersonService>(provider.GetRequiredService<IPersonService>());
        Assert.IsType<ApiTeamContactService>(provider.GetRequiredService<ITeamContactService>());
        Assert.IsType<ApiFixtureService>(provider.GetRequiredService<IFixtureService>());
        Assert.NotNull(provider.GetRequiredService<ApiAuthClient>());
        Assert.Same(provider.GetRequiredService<ApiSession>(), provider.GetRequiredService<ApiSession>());   // one session per app

        // The services' HttpClient points at the server root with a trailing slash, so "api/teams" resolves correctly.
        var http = provider.GetRequiredService<IHttpClientFactory>().CreateClient(typeof(ITeamService).Name);
        Assert.Equal("https://fixtures.example.org/", http.BaseAddress?.ToString());
    }
}
