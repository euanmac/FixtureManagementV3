using System.Net;
using FixtureManagement.FullTime;
using HtmlAgilityPack;
using Microsoft.Extensions.DependencyInjection;

namespace FixtureManagement.Tests.FullTime;

public class SelectablePageLoaderTests
{
    private sealed class NamedLoader(string name) : IFullTimePageLoader
    {
        public List<string> Urls { get; } = [];
        public string UserAgent => name;

        public Task<FullTimePage> LoadAsync(string url, CancellationToken cancellationToken = default)
        {
            Urls.Add(url);
            return Task.FromResult(new FullTimePage(HttpStatusCode.OK, new HtmlDocument()));
        }
    }

    [Fact]
    public async Task Follows_the_choice_on_every_request()
    {
        var choice = FullTimePageLoaderKind.HtmlWeb;
        var htmlWeb = new NamedLoader("htmlweb");
        var selenium = new NamedLoader("selenium");
        var loader = new SelectablePageLoader(() => choice, htmlWeb, selenium);

        await loader.LoadAsync("a");
        Assert.Equal("htmlweb", loader.UserAgent);
        choice = FullTimePageLoaderKind.Selenium;
        await loader.LoadAsync("b");
        Assert.Equal("selenium", loader.UserAgent);

        Assert.Equal(["a"], htmlWeb.Urls);
        Assert.Equal(["b"], selenium.Urls);
    }

    [Fact]
    public void The_app_registration_uses_the_selectable_loader()
    {
        var services = new ServiceCollection();
        services.AddFullTime(_ => () => FullTimePageLoaderKind.HtmlWeb);
        using var provider = services.BuildServiceProvider();

        var loader = provider.GetRequiredService<IFullTimePageLoader>();

        Assert.IsType<SelectablePageLoader>(loader);
        Assert.Equal(HtmlWebPageLoader.UserAgent, loader.UserAgent);
    }
}
