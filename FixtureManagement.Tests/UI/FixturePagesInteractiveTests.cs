using System.Runtime.CompilerServices;
using Bunit;
using NSubstitute;
using Allocate = FixtureManagement.UI.Pages.Fixtures.Allocate;
using FixturesIndex = FixtureManagement.UI.Pages.Fixtures.Index;
using Gameweek = FixtureManagement.UI.Pages.Fixtures.Gameweek;

namespace FixtureManagement.Tests.UI;

public class AllocateInteractiveTests : InteractivePageTestBase
{
    private readonly PitchDto pitch = new() { Id = Guid.NewGuid(), Name = "Main", DisplayOrder = 5 };
    private readonly FixtureDto fixture;

    public AllocateInteractiveTests()
    {
        fixture = new FixtureDto
        {
            Id = Guid.NewGuid(), TeamId = Guid.NewGuid(), Opponent = "Rivals", IsHome = true, Date = new DateOnly(2026, 11, 8),
            Team = new TeamSummaryDto { Name = "Lions" },
            Allocation = new FixtureAllocationDto { Id = Guid.NewGuid(), PitchId = pitch.Id, PitchName = "Main", Start = new TimeOnly(9, 30), Duration = TimeSpan.FromMinutes(90) },
        };
        Pitches.GetPitchesAsync(Arg.Any<CancellationToken>()).Returns([pitch]);
        Fixtures.GetFixtureAsync(fixture.Id, Arg.Any<CancellationToken>()).Returns(fixture);
        Fixtures.SetAllocationAsync(fixture.Id, Arg.Any<FixtureAllocationDto>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<FixtureAllocationDto>.Success(fixture.Allocation!));
        GoTo($"fixtures/allocate?id={fixture.Id}");
    }

    [Fact]
    public async Task Delete_removes_the_allocation_rather_than_saving_it()
    {
        var page = Render<Allocate>();

        await page.Find("button.btn-danger").ClickAsync(new());

        await Fixtures.Received(1).RemoveAllocationAsync(fixture.Id, Arg.Any<CancellationToken>());
        await Fixtures.DidNotReceive().SetAllocationAsync(Arg.Any<Guid>(), Arg.Any<FixtureAllocationDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Save_saves_the_edited_allocation()
    {
        var page = Render<Allocate>();
        page.Find("#isconfirmed").Change(true);

        await page.Find("button.btn-primary").ClickAsync(new());

        await Fixtures.Received(1).SetAllocationAsync(fixture.Id, Arg.Is<FixtureAllocationDto>(a => a.IsConfirmed && a.PitchId == pitch.Id), Arg.Any<CancellationToken>());
        await Fixtures.DidNotReceive().RemoveAllocationAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}

public class FixturesIndexInteractiveTests : InteractivePageTestBase
{
    [Fact]
    public async Task Filtering_reloads_the_list_with_the_new_dates()
    {
        var page = Render<FixturesIndex>();
        page.Find("#startDate").Change("2027-01-01");
        page.Find("#endDate").Change("2027-01-31");

        await page.Find("form").SubmitAsync();

        await Fixtures.Received(1).GetFixturesAsync(
            Arg.Is<FixtureQuery?>(q => q!.From == new DateOnly(2027, 1, 1) && q.To == new DateOnly(2027, 1, 31)), Arg.Any<CancellationToken>());
    }
}

public class GameweekInteractiveTests : InteractivePageTestBase
{
    private static readonly DateOnly Sunday = new(2026, 11, 8);
    private readonly TeamDto lions = new() { Id = Guid.NewGuid(), Name = "Lions", AgeGroup = AgeGroup.U10, League = League.SouthBucksMini };
    private readonly Guid allocatedFixtureId = Guid.NewGuid();
    private readonly Guid unmatchedLocalId = Guid.NewGuid();

    public GameweekInteractiveTests()
    {
        Teams.GetTeamsAsync(Arg.Any<CancellationToken>()).Returns([lions]);
        FullTime.Reconcile(Arg.Any<IReadOnlyList<TeamDto>>(), Arg.Any<IReadOnlyList<FixtureDto>>(), Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(_ => Rows());
        Fixtures.CreateFixturesAsync(Arg.Any<IReadOnlyList<FixtureDto>>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<IReadOnlyList<FixtureDto>>.Success([]));
        Fixtures.CreateFixtureAsync(Arg.Any<FixtureDto>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<FixtureDto>.Success(new FixtureDto()));
        GoTo($"gameweek?matchday={Sunday:dd-MMM-yyyy}");
    }

    private async IAsyncEnumerable<List<TeamReconiliationRow>> Rows([EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.Yield();
        yield return [
            new TeamReconiliationRow { Team = lions, Id = allocatedFixtureId, MatchDate = Sunday, Opponent = "Allocated FC", Venue = "H",
                RecStatus = FixtureRecMatchType.fullTimematched, IsAllocated = true, IsConfirmed = false, CanAllocate = true, Pitch = "Main" },
            new TeamReconiliationRow { Team = lions, Id = unmatchedLocalId, MatchDate = Sunday, Opponent = "Only Local FC", Venue = "A",
                RecStatus = FixtureRecMatchType.localFixtureUnmatched },
            new TeamReconiliationRow { Team = lions, Id = Guid.NewGuid(), MatchDate = Sunday, Opponent = "Only On FullTime FC", Venue = "A",
                RecStatus = FixtureRecMatchType.fullTimeUnmatched, FixtureType = FixtureType.Cup },
        ];
    }

    private async Task<IRenderedComponent<Gameweek>> RenderLoadedAsync()
    {
        var page = Render<Gameweek>();
        await page.WaitForStateAsync(() => !page.Find("button[value='Update']").HasAttribute("disabled"), TimeSpan.FromSeconds(5));
        return page;
    }

    [Fact]
    public async Task Confirm_confirms_the_allocated_unconfirmed_fixtures()
    {
        var page = await RenderLoadedAsync();

        await page.Find("button[value='Confirm']").ClickAsync(new());

        await Fixtures.Received(1).ConfirmAllocationsAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.SequenceEqual(new[] { allocatedFixtureId })), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Download_adds_the_FullTime_only_fixtures()
    {
        var page = await RenderLoadedAsync();

        await page.Find("button[value='Download']").ClickAsync(new());

        await Fixtures.Received(1).CreateFixturesAsync(
            Arg.Is<IReadOnlyList<FixtureDto>>(list => list.Count == 1 && list[0].Opponent == "Only On FullTime FC" && list[0].TeamId == lions.Id
                                                      && !list[0].IsHome && list[0].FixtureType == FixtureType.Cup && list[0].Date == Sunday),
            Arg.Any<CancellationToken>());
        FullTime.DidNotReceive().ClearCache();   // was the old behaviour: the button posted no action, so "Update" ran
    }

    [Fact]
    public async Task Delete_unmatched_deletes_only_the_unmatched_local_fixture()
    {
        var page = await RenderLoadedAsync();

        await page.Find("button[value='Delete']").ClickAsync(new());

        await Fixtures.Received(1).DeleteFixtureAsync(unmatchedLocalId, Arg.Any<CancellationToken>());
        await Fixtures.DidNotReceive().DeleteFixtureAsync(allocatedFixtureId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_clears_the_FullTime_cache_and_reloads()
    {
        var page = await RenderLoadedAsync();

        await page.Find("button[value='Update']").ClickAsync(new());

        FullTime.Received(1).ClearCache();
        await page.WaitForStateAsync(() => FullTime.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(IFullTimeService.Reconcile)) == 2, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task The_row_download_button_adds_that_one_fixture()
    {
        var page = await RenderLoadedAsync();

        await page.Find("td button.btn-link").ClickAsync(new());

        await Fixtures.Received(1).CreateFixtureAsync(
            Arg.Is<FixtureDto>(f => f.Opponent == "Only On FullTime FC" && f.TeamId == lions.Id && f.Date == Sunday), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Week_links_are_real_links_not_script()
    {
        var page = await RenderLoadedAsync();

        Assert.NotNull(page.Find("a[aria-label='prevSat'][href*='matchday=26-Oct-2026']"));
        Assert.NotNull(page.Find("a[aria-label='nextSat'][href*='matchday=09-Nov-2026']"));
        Assert.DoesNotContain("location.href", page.Markup);
    }
}
