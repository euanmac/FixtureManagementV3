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
        => services.AddFixtureManagementApi(_ => serverAddress);

    /// <summary>
    /// As above, but the server address is looked up each time an HttpClient is created (e.g. from a user setting),
    /// so a changed address applies to services resolved afterwards without restarting the app.
    /// </summary>
    public static IServiceCollection AddFixtureManagementApi(this IServiceCollection services, Func<IServiceProvider, Uri> serverAddress)
    {
        void Configure(IServiceProvider provider, HttpClient client) => client.BaseAddress = WithTrailingSlash(serverAddress(provider));

        services.AddSingleton<ApiSession>();
        services.AddHttpClient<ApiAuthClient>(Configure);   // no bearer handler: it gets the tokens
        services.AddTransient<BearerTokenHandler>();

        services.AddHttpClient<ITeamService, ApiTeamService>(Configure).AddHttpMessageHandler<BearerTokenHandler>();
        services.AddHttpClient<IPitchService, ApiPitchService>(Configure).AddHttpMessageHandler<BearerTokenHandler>();
        services.AddHttpClient<IBookingService, ApiBookingService>(Configure).AddHttpMessageHandler<BearerTokenHandler>();
        services.AddHttpClient<IPersonService, ApiPersonService>(Configure).AddHttpMessageHandler<BearerTokenHandler>();
        services.AddHttpClient<ITeamContactService, ApiTeamContactService>(Configure).AddHttpMessageHandler<BearerTokenHandler>();
        services.AddHttpClient<IFixtureService, ApiFixtureService>(Configure).AddHttpMessageHandler<BearerTokenHandler>();
        return services;
    }

    /// <summary>Relative URLs ("api/teams") need the base address to end in "/".</summary>
    public static Uri WithTrailingSlash(Uri address)
        => address.AbsoluteUri.EndsWith('/') ? address : new Uri(address.AbsoluteUri + "/");
}
