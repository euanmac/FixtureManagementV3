using FixtureManagement.Shared.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FixtureManagement.ApiClient;

public static class ApiClientServiceCollectionExtensions
{
    /// <summary>
    /// Registers the HttpClient-based service implementations, so the shared pages talk to the server's API
    /// instead of a database. <paramref name="serverAddress"/> is the site root, e.g. https://tufixturemanager.azurewebsites.net/.
    /// Also registers <see cref="ApiSession"/> (singleton) and <see cref="ApiAuthClient"/> for signing in.
    /// </summary>
    public static IServiceCollection AddFixtureManagementApi(this IServiceCollection services, Uri serverAddress)
    {
        // Relative URLs ("api/teams") need the base address to end in "/".
        var baseAddress = serverAddress.AbsoluteUri.EndsWith('/') ? serverAddress : new Uri(serverAddress.AbsoluteUri + "/");

        services.AddSingleton<ApiSession>();
        services.AddHttpClient<ApiAuthClient>(c => c.BaseAddress = baseAddress);   // no bearer handler: it gets the tokens
        services.AddTransient<BearerTokenHandler>();

        services.AddHttpClient<ITeamService, ApiTeamService>(c => c.BaseAddress = baseAddress).AddHttpMessageHandler<BearerTokenHandler>();
        services.AddHttpClient<IPitchService, ApiPitchService>(c => c.BaseAddress = baseAddress).AddHttpMessageHandler<BearerTokenHandler>();
        services.AddHttpClient<IBookingService, ApiBookingService>(c => c.BaseAddress = baseAddress).AddHttpMessageHandler<BearerTokenHandler>();
        services.AddHttpClient<IPersonService, ApiPersonService>(c => c.BaseAddress = baseAddress).AddHttpMessageHandler<BearerTokenHandler>();
        services.AddHttpClient<ITeamContactService, ApiTeamContactService>(c => c.BaseAddress = baseAddress).AddHttpMessageHandler<BearerTokenHandler>();
        services.AddHttpClient<IFixtureService, ApiFixtureService>(c => c.BaseAddress = baseAddress).AddHttpMessageHandler<BearerTokenHandler>();
        return services;
    }
}
