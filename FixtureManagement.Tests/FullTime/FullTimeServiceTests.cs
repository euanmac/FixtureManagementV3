using System.Net;
using FixtureManagement.FullTime;

namespace FixtureManagement.Tests.FullTime;

public class FullTimeServiceTests
{
    private static readonly DateOnly Monday = new(2026, 11, 2);
    private static readonly DateOnly Sunday = new(2026, 11, 8);

    private const string OneFixturePage = """
        <div class="fixtures-table"><table><tbody>
          <tr><td>L</td><td>08/11/26 10:00</td><td class="home-team">Thame United Lions</td><td>v</td><td class="road-team">Rivals</td></tr>
          <tr><td>L</td><td>15/11/26 10:00</td><td class="home-team">Thame United Lions</td><td>v</td><td class="road-team">Next Week FC</td></tr>
        </tbody></table></div>
        """;

    /// <summary>
    /// Stands in for HtmlWeb: answers every request with the given page (or fails), and records what was asked for.
    /// Responses are written as HttpResponseMessages for readability and turned into FullTimePages.
    /// </summary>
    private sealed class FakeFullTime(Func<HttpRequestMessage, HttpResponseMessage> respond) : IFullTimePageLoader
    {
        private readonly object gate = new();
        public List<HttpRequestMessage> Requests { get; } = [];

        public async Task<FullTimePage> LoadAsync(string url, CancellationToken cancellationToken = default)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            lock (gate)
            {
                Requests.Add(request);
            }
            using var response = respond(request);
            return await ToPageAsync(response);
        }
    }

    private static async Task<FullTimePage> ToPageAsync(HttpResponseMessage response)
    {
        var document = new HtmlAgilityPack.HtmlDocument();
        document.LoadHtml(await response.Content.ReadAsStringAsync());
        return new FullTimePage(response.StatusCode, document);
    }

    private static HttpResponseMessage Ok(string html) => new(HttpStatusCode.OK) { Content = new StringContent(html) };

    private static TeamDto OnFullTime(string name = "Thame United Lions", string teamId = "2") =>
        new() { Id = Guid.NewGuid(), Name = name, League = League.SouthBucksMini, FullTimeLeagueId = "1", FullTimeTeamId = teamId };

    [Fact]
    public async Task GetFixtures_requests_the_team_page_once()
    {
        var fake = new FakeFullTime(_ => Ok(OneFixturePage));
        var service = new FullTimeService(fake);

        var fixtures = await service.GetFixturesAsync(OnFullTime());

        Assert.Equal(2, fixtures.Count);
        var request = Assert.Single(fake.Requests);
        Assert.Equal("https://fulltime.thefa.com/displayTeam.html?divisionseason=1&teamID=2", request.RequestUri!.ToString());
        Assert.Equal(OneFixturePage, service.LastHtml);
    }

    [Fact]
    public async Task Teams_without_FullTime_ids_make_no_request()
    {
        var fake = new FakeFullTime(_ => Ok(OneFixturePage));
        var service = new FullTimeService(fake);

        Assert.Empty(await service.GetFixturesAsync(new TeamDto { Name = "Not on FullTime", FullTimeLeagueId = "", FullTimeTeamId = "" }));
        Assert.Empty(fake.Requests);
    }

    [Fact]
    public async Task Reconcile_yields_one_result_per_team_limited_to_the_week()
    {
        var fake = new FakeFullTime(_ => Ok(OneFixturePage));
        var service = new FullTimeService(fake);
        var lions = OnFullTime();
        var notOnFullTime = new TeamDto { Id = Guid.NewGuid(), Name = "Local Only", League = League.SouthBucksMini };

        var results = new List<List<TeamReconiliationRow>>();
        await foreach (var rows in service.Reconcile([lions, notOnFullTime], [], Monday, Sunday))
        {
            results.Add(rows);
        }

        Assert.Equal(2, results.Count);
        var lionsRow = Assert.Single(results.Single(r => r[0].Team == lions));
        Assert.Equal(FixtureRecMatchType.fullTimeUnmatched, lionsRow.RecStatus);   // only the 08/11 fixture is in the week
        Assert.Equal("Rivals", lionsRow.Opponent);
        Assert.Equal(FixtureRecMatchType.noFixture, Assert.Single(results.Single(r => r[0].Team == notOnFullTime)).RecStatus);
        Assert.Single(fake.Requests);                                              // the local-only team isn't fetched
    }

    [Fact]
    public async Task FullTime_pages_are_cached_per_team_and_week_until_cleared()
    {
        var fake = new FakeFullTime(_ => Ok(OneFixturePage));
        var service = new FullTimeService(fake);
        var lions = OnFullTime();
        Assert.Equal(TimeSpan.Zero, service.CacheAge);

        await Drain(service.Reconcile([lions], [], Monday, Sunday));
        await Drain(service.Reconcile([lions], [], Monday, Sunday));
        Assert.Single(fake.Requests);
        await Task.Delay(20);
        Assert.True(service.CacheAge > TimeSpan.Zero);   // was always zero in the old service

        await Drain(service.Reconcile([lions], [], Monday.AddDays(7), Sunday.AddDays(7)));   // another week: new fetch
        Assert.Equal(2, fake.Requests.Count);

        service.ClearCache();
        Assert.Equal(TimeSpan.Zero, service.CacheAge);
        await Drain(service.Reconcile([lions], [], Monday, Sunday));
        Assert.Equal(3, fake.Requests.Count);
    }

    [Fact]
    public async Task A_team_whose_page_fails_gets_an_unknown_row_and_the_others_still_load()
    {
        var broken = OnFullTime("Thame United Broken", teamId: "999");
        var fake = new FakeFullTime(r => r.RequestUri!.Query.Contains("teamID=999")
            ? Ok("""<div class="fixtures-table"><table><tbody><tr><td>L</td><td>garbage</td></tr></tbody></table></div>""")
            : Ok(OneFixturePage));
        var service = new FullTimeService(fake);
        var lions = OnFullTime();

        var results = await Drain(service.Reconcile([broken, lions], [], Monday, Sunday));

        Assert.Equal(FixtureRecMatchType.unknownTeam, Assert.Single(results.Single(r => r[0].Team == broken)).RecStatus);
        Assert.Equal(FixtureRecMatchType.fullTimeUnmatched, Assert.Single(results.Single(r => r[0].Team == lions)).RecStatus);

        // The failure isn't cached, so the next load tries again.
        await Drain(service.Reconcile([broken], [], Monday, Sunday));
        Assert.Equal(2, fake.Requests.Count(r => r.RequestUri!.Query.Contains("teamID=999")));
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task When_FullTime_refuses_it_stops_asking_and_no_local_fixture_looks_unmatched(HttpStatusCode refusal)
    {
        // FullTime's block page: a big HTML page with no fixtures. Must not read as "this team has no fixtures".
        var fake = new FakeFullTime(_ => new HttpResponseMessage(refusal) { Content = new StringContent("<html>blocked</html>") });
        var service = new FullTimeService(fake, options: new FullTimeOptions { MaxConcurrentRequests = 1, DelayBetweenRequests = TimeSpan.Zero });
        var teams = Enumerable.Range(1, 5).Select(i => OnFullTime($"Team {i}", teamId: i.ToString())).ToList();
        var localFixture = new FixtureDto { Id = Guid.NewGuid(), TeamId = teams[0].Id, Date = Sunday, Opponent = "Rivals" };

        var results = await Drain(service.Reconcile(teams, [localFixture], Monday, Sunday));

        Assert.All(results, rows => Assert.Equal(FixtureRecMatchType.unknownTeam, Assert.Single(rows).RecStatus));
        Assert.Single(fake.Requests);   // after the first refusal the other four weren't requested
        await Assert.ThrowsAsync<FullTimeRefusedException>(() => service.GetFixturesAsync(teams[1]));
        Assert.Single(fake.Requests);   // still pausing
    }

    [Fact]
    public async Task A_server_error_is_a_failure_not_an_empty_fixture_list()
    {
        var fake = new FakeFullTime(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("oops") });
        var service = new FullTimeService(fake, options: new FullTimeOptions { DelayBetweenRequests = TimeSpan.Zero });

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => service.GetFixturesAsync(OnFullTime()));
        Assert.Equal(HttpStatusCode.InternalServerError, error.StatusCode);

        // Not a refusal, so the next request goes ahead.
        await Assert.ThrowsAsync<HttpRequestException>(() => service.GetFixturesAsync(OnFullTime()));
        Assert.Equal(2, fake.Requests.Count);
    }

    [Fact]
    public async Task A_gameweek_load_never_has_more_than_the_allowed_requests_in_flight()
    {
        var inFlight = 0;
        var maxInFlight = 0;
        var handler = new SlowFullTime(async () =>
        {
            var now = Interlocked.Increment(ref inFlight);
            InterlockedMax(ref maxInFlight, now);
            await Task.Delay(30);
            Interlocked.Decrement(ref inFlight);
            return Ok(OneFixturePage);
        });
        var service = new FullTimeService(handler, options: new FullTimeOptions { MaxConcurrentRequests = 2, DelayBetweenRequests = TimeSpan.FromMilliseconds(10) });
        var teams = Enumerable.Range(1, 8).Select(i => OnFullTime($"Team {i}", teamId: i.ToString())).ToList();

        var results = await Drain(service.Reconcile(teams, [], Monday, Sunday));

        Assert.Equal(8, results.Count);
        Assert.All(results, rows => Assert.Equal(FixtureRecMatchType.fullTimeUnmatched, Assert.Single(rows).RecStatus));
        Assert.Equal(8, handler.RequestCount);
        Assert.True(maxInFlight <= 2, $"{maxInFlight} requests were in flight at once");
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int current;
        while (value > (current = Volatile.Read(ref target)) && Interlocked.CompareExchange(ref target, value, current) != current) { }
    }

    /// <summary>A FullTime stand-in whose answers take a while, to see how many requests overlap.</summary>
    private sealed class SlowFullTime(Func<Task<HttpResponseMessage>> respond) : IFullTimePageLoader
    {
        private int requestCount;
        public int RequestCount => requestCount;

        public async Task<FullTimePage> LoadAsync(string url, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref requestCount);
            using var response = await respond();
            return await ToPageAsync(response);
        }
    }

    private static async Task<List<List<TeamReconiliationRow>>> Drain(IAsyncEnumerable<List<TeamReconiliationRow>> source)
    {
        var all = new List<List<TeamReconiliationRow>>();
        await foreach (var rows in source) all.Add(rows);
        return all;
    }
}
