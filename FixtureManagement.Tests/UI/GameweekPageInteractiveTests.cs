using Bunit;
using FixtureManagement.Shared.Reconciliation;
using FixtureManagement.UI.Pages.Fixtures;
using NSubstitute;

namespace FixtureManagement.Tests.UI;

/// <summary>/gameweek: the week compared with the latest FullTime upload (the live-FullTime copy is GameweekLive).</summary>
public class GameweekPageInteractiveTests : InteractivePageTestBase
{
    private static readonly DateOnly Monday = new(2026, 12, 7);
    private static readonly DateOnly Sunday = new(2026, 12, 13);
    private readonly TeamDto lions = new() { Id = Guid.NewGuid(), Name = "Lions", AgeGroup = AgeGroup.U13, League = League.WycAndSouthBucksMinor, FullTimeLeagueId = "1", FullTimeTeamId = "2" };
    private readonly FixtureDto same;
    private readonly FixtureDto dropped;
    private readonly FixtureDto movedIn;
    private readonly FixtureDto droppedAllocated;

    public GameweekPageInteractiveTests()
    {
        same = new() { Id = Guid.NewGuid(), TeamId = lions.Id, Date = Sunday, Opponent = "Same FC", IsHome = true, FixtureType = FixtureType.League };
        dropped = new() { Id = Guid.NewGuid(), TeamId = lions.Id, Date = Sunday, Opponent = "Dropped FC", IsHome = false, FixtureType = FixtureType.League };
        movedIn = new() { Id = Guid.NewGuid(), TeamId = lions.Id, Date = Sunday.AddDays(-7), Opponent = "Moved FC", IsHome = true, FixtureType = FixtureType.League };
        droppedAllocated = new()
        {
            Id = Guid.NewGuid(), TeamId = lions.Id, Date = Sunday, Opponent = "Booked FC", IsHome = true, FixtureType = FixtureType.League,
            Allocation = new FixtureAllocationDto { Id = Guid.NewGuid(), PitchId = Guid.NewGuid(), PitchName = "Main", Start = new TimeOnly(10, 0), Duration = TimeSpan.FromHours(1) },
        };

        Teams.GetTeamsAsync(Arg.Any<CancellationToken>()).Returns([lions]);
        Fixtures.GetFixturesAsync(Arg.Any<FixtureQuery?>(), Arg.Any<CancellationToken>()).Returns([same, dropped, droppedAllocated]);
        Fixtures.CreateFixturesAsync(Arg.Any<IReadOnlyList<FixtureDto>>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<IReadOnlyList<FixtureDto>>.Success([]));
        Reconciliation.GetWeekAsync("FullTime", Monday, Sunday, Arg.Any<CancellationToken>()).Returns(new ReconciliationWeekDto
        {
            Upload = new SourceUploadSummaryDto { Source = "FullTime", SourceName = "FullTime", UploadedBy = "me@example.com", UploadedAt = new DateTime(2026, 12, 1, 9, 0, 0, DateTimeKind.Utc) },
            CoveredTeamIds = [lions.Id],
            CoveredFrom = new DateOnly(2026, 11, 20),
            Items =
            [
                new ReconciliationItem { Kind = DifferenceKind.Same, Team = lions, App = same, Source = Src("Same FC", true) },
                new ReconciliationItem { Kind = DifferenceKind.OnlyInApp, Team = lions, App = dropped },
                new ReconciliationItem { Kind = DifferenceKind.OnlyInApp, Team = lions, App = droppedAllocated },
                new ReconciliationItem { Kind = DifferenceKind.OnlyInSource, Team = lions, Source = Src("New FC", false, FixtureType.Cup) },
                new ReconciliationItem { Kind = DifferenceKind.DateMoved, Team = lions, App = movedIn, Source = Src("Moved FC", true) },
            ],
        });
        GoTo($"gameweek?matchday={Sunday:dd-MMM-yyyy}");
    }

    private SourceFixture Src(string opponent, bool home, FixtureType type = FixtureType.League) =>
        new() { TeamId = lions.Id, Date = Sunday, Opponent = opponent, IsHome = home, FixtureType = type };

    private async Task<IRenderedComponent<Gameweek>> RenderLoadedAsync()
    {
        var page = Render<Gameweek>();
        await page.WaitForStateAsync(() => !page.Find("button[value='Update']").HasAttribute("disabled"), TimeSpan.FromSeconds(5));
        return page;
    }

    [Fact]
    public async Task Compares_the_week_with_the_latest_upload_and_says_when_it_was()
    {
        var page = await RenderLoadedAsync();

        Assert.Contains("Compared with the FullTime fetch of", page.Markup);
        Assert.Contains("me@example.com", page.Markup);
        Assert.NotNull(page.Find("a[href='fixtures/review']"));
        Assert.Empty(FullTime.ReceivedCalls());                         // no live FullTime fetching
    }

    [Fact]
    public async Task A_fixture_that_moved_into_the_week_is_flagged_without_edit_links()
    {
        var page = await RenderLoadedAsync();

        var row = page.FindAll("tbody tr").Single(r => r.TextContent.Contains("Moved FC"));
        Assert.Contains("In Fixture Manager on Sun 06/12", row.TextContent);
        Assert.Contains("Differs from FullTime", row.QuerySelector("i.bi-exclamation-triangle")!.GetAttribute("title"));
        Assert.DoesNotContain(row.QuerySelectorAll("a"), a => (a.GetAttribute("href") ?? "").StartsWith("fixtures/edit"));
    }

    [Fact]
    public async Task A_fixture_not_on_FullTime_is_a_warning()
    {
        var page = await RenderLoadedAsync();

        var row = page.FindAll("tbody tr").Single(r => r.TextContent.Contains("Dropped FC"));
        Assert.Contains("Not on FullTime", row.TextContent);
        Assert.NotNull(row.QuerySelector("i.bi-exclamation-triangle.text-warning"));
    }

    [Fact]
    public async Task Download_adds_the_FullTime_only_fixture_from_the_upload()
    {
        var page = await RenderLoadedAsync();

        await page.Find("button[value='Download']").ClickAsync(new());

        await Fixtures.Received(1).CreateFixturesAsync(
            Arg.Is<IReadOnlyList<FixtureDto>>(list => list.Count == 1 && list[0].Opponent == "New FC" && list[0].TeamId == lions.Id
                                                      && !list[0].IsHome && list[0].FixtureType == FixtureType.Cup && list[0].Date == Sunday),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_unmatched_counts_and_deletes_only_unallocated_fixtures_not_on_FullTime()
    {
        var page = await RenderLoadedAsync();
        Assert.Equal("Delete Unmatched 1", page.Find("button[value='Delete']").TextContent.Trim());   // not the allocated one

        await page.Find("button[value='Delete']").ClickAsync(new());

        await Fixtures.Received(1).DeleteFixtureAsync(dropped.Id, Arg.Any<CancellationToken>());
        await Fixtures.DidNotReceive().DeleteFixtureAsync(droppedAllocated.Id, Arg.Any<CancellationToken>());   // has a pitch booked
        await Fixtures.DidNotReceive().DeleteFixtureAsync(same.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task With_no_upload_it_says_so_and_shows_Fixture_Managers_fixtures()
    {
        Reconciliation.GetWeekAsync("FullTime", Monday, Sunday, Arg.Any<CancellationToken>()).Returns(new ReconciliationWeekDto());

        var page = await RenderLoadedAsync();

        Assert.Contains("Nothing has been fetched from FullTime yet", page.Markup);
        Assert.Contains("Same FC", page.Markup);
    }

    [Fact]
    public async Task A_FullTime_fixture_someone_chose_not_to_add_is_accepted_and_can_still_be_downloaded()
    {
        Reconciliation.GetWeekAsync("FullTime", Monday, Sunday, Arg.Any<CancellationToken>()).Returns(new ReconciliationWeekDto
        {
            Upload = new SourceUploadSummaryDto { Source = "FullTime", SourceName = "FullTime" },
            CoveredTeamIds = [lions.Id],
            CoveredFrom = new DateOnly(2026, 11, 20),
            Items = [new ReconciliationItem { Kind = DifferenceKind.OnlyInSource, Team = lions, Source = Src("Not Wanted FC", false), Kept = true, KeptBy = "me@example.com" }],
        });
        Fixtures.GetFixturesAsync(Arg.Any<FixtureQuery?>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<FixtureDto>());

        var page = await RenderLoadedAsync();

        var row = page.FindAll("tbody tr").Single(r => r.TextContent.Contains("Not Wanted FC"));
        Assert.Contains("Accepted", row.QuerySelector("i")!.GetAttribute("title"));
        Assert.NotNull(row.QuerySelector("i.bi-cloud-arrow-down"));                                           // per-row download
        Assert.DoesNotContain(row.QuerySelectorAll("a"), a => (a.GetAttribute("href") ?? "").StartsWith("fixtures/edit"));
        Assert.Equal("Download 0", page.Find("button[value='Download']").TextContent.Trim());                 // bulk download respects the decision
    }
}
