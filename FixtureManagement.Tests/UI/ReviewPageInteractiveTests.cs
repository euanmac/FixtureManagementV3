using Bunit;
using FixtureManagement.Shared.Reconciliation;
using FixtureManagement.UI.Pages.Fixtures;
using NSubstitute;

namespace FixtureManagement.Tests.UI;

public class ReviewPageInteractiveTests : InteractivePageTestBase
{
    private static readonly DateOnly Dec6 = new(2026, 12, 6);
    private readonly TeamDto lions = new() { Id = Guid.NewGuid(), Name = "Thame United Lions", AgeGroup = AgeGroup.U13, Gender = Gender.Male, League = League.WycAndSouthBucksMinor };
    private readonly TeamDto girls = new() { Id = Guid.NewGuid(), Name = "Thame United", AgeGroup = AgeGroup.U14, Gender = Gender.Female, League = League.OxGirls };

    private ReconciliationItem GirlsDropped() => new()
    {
        Kind = DifferenceKind.OnlyInApp, Team = girls, Suggested = Resolution.UseSource,
        App = new FixtureDto { Id = Guid.NewGuid(), TeamId = girls.Id, Date = Dec6, Opponent = "Summertown", IsHome = true, FixtureType = FixtureType.League },
    };

    private ReconciliationItem Moved(bool allocated = false) => new()
    {
        Kind = DifferenceKind.DateMoved, Team = lions, Changes = FixtureChanges.Date, Suggested = Resolution.UseSource,
        App = new FixtureDto
        {
            Id = Guid.NewGuid(), TeamId = lions.Id, Date = Dec6.AddDays(7), Opponent = "Rivals", IsHome = true, FixtureType = FixtureType.League,
            Allocation = allocated ? new FixtureAllocationDto { PitchId = Guid.NewGuid(), Start = new TimeOnly(10, 0), Duration = TimeSpan.FromHours(1) } : null,
        },
        Source = new SourceFixture { TeamId = lions.Id, Date = Dec6, Opponent = "Rivals", IsHome = true, FixtureType = FixtureType.League, SourceFixtureId = "31201701" },
    };

    private ReconciliationItem NewOnFullTime() => new()
    {
        Kind = DifferenceKind.OnlyInSource, Team = lions, Suggested = Resolution.UseSource,
        Source = new SourceFixture { TeamId = lions.Id, Date = Dec6.AddDays(14), Opponent = "Newcomers", IsHome = false, FixtureType = FixtureType.Cup },
    };

    /// <summary>The page has a filter form (GET) and the apply form; this is the latter.</summary>
    private static AngleSharp.Dom.IElement ApplyForm(IRenderedComponent<Review> page) =>
        page.FindAll("form").Single(f => f.GetAttribute("method") != "get");

    private void Returns(params ReconciliationItem[] items) =>
        Reconciliation.GetReviewAsync("FullTime", Arg.Any<CancellationToken>()).Returns(new ReconciliationReviewDto
        {
            Upload = new SourceUploadSummaryDto { Source = "FullTime", SourceName = "FullTime", UploadedAt = new DateTime(2026, 11, 20, 9, 0, 0, DateTimeKind.Utc), UploadedBy = "me@example.com", TeamCount = 36, FixtureCount = 312 },
            Items = items, SameCount = 250, KeptCount = 2, From = new DateOnly(2026, 11, 20), To = DateOnly.MaxValue,
        });

    [Fact]
    public void Shows_each_difference_with_the_suggested_choice_selected()
    {
        var moved = Moved(allocated: true);
        Returns(moved, NewOnFullTime());

        var page = Render<Review>();

        Assert.Contains("250 match", page.Markup);
        Assert.Contains("2 to review", page.Markup);
        Assert.Contains("Brought forward", page.Markup);
        Assert.Contains("removes its pitch allocation", page.Markup);
        var use = Side(page, moved.Id, Resolution.UseSource);
        var keep = Side(page, moved.Id, Resolution.KeepApp);
        Assert.Equal($"Choices[{moved.Id}]", use.GetAttribute("name"));         // what the static site posts
        Assert.True(use.HasAttribute("checked"));                               // the suggestion is chosen to start with
        Assert.False(keep.HasAttribute("checked"));
        Assert.Equal("label", use.ParentElement!.LocalName);                    // the whole cell is its label
        Assert.Equal("Use", use.ParentElement.QuerySelector(".choice-action")!.TextContent.Trim());
        Assert.NotNull(use.ParentElement.QuerySelector("svg"));                 // with an icon
        Assert.Equal(moved.Id, page.Find("tbody tr button[name=RowAction]").GetAttribute("value"));   // the row's Apply
        var teamLink = page.Find("tbody tr td:nth-child(2) a");
        Assert.Equal((lions.DisplayName, $"teams/details?id={lions.Id}"), (teamLink.TextContent, teamLink.GetAttribute("href")));
    }

    [Theory]
    [InlineData(DifferenceKind.OnlyInSource, "Don't add", "Add")]
    [InlineData(DifferenceKind.OnlyInApp, "Keep", "Delete")]
    [InlineData(DifferenceKind.DateMoved, "Keep", "Use")]
    public void The_buttons_say_what_they_do_for_each_kind(DifferenceKind kind, string fixtureManagerButton, string fullTimeButton)
    {
        var item = kind switch
        {
            DifferenceKind.OnlyInSource => NewOnFullTime(),
            DifferenceKind.OnlyInApp => GirlsDropped(),
            _ => Moved(),
        };
        Returns(item);

        var page = Render<Review>();

        var cells = page.Find("tbody tr").QuerySelectorAll("td");
        Assert.Equal(fixtureManagerButton, cells[2].QuerySelector(".choice-action")!.TextContent.Trim());   // In Fixture Manager column
        Assert.Equal(fullTimeButton, cells[3].QuerySelector(".choice-action")!.TextContent.Trim());         // On FullTime column
    }

    [Fact]
    public void Groups_the_differences_by_kind_most_urgent_first_with_their_source()
    {
        Returns(NewOnFullTime(), Moved());      // the service gives date order; the page groups them

        var page = Render<Review>();

        Assert.Equal(["Date moved 1", "Not in Fixture Manager 1"], page.FindAll("summary .section-title").Select(h => h.TextContent.Trim()));
        var tables = page.FindAll("table");
        Assert.Equal(2, tables.Count);
        var link = tables[0].QuerySelector("a[href*='fulltime.thefa.com']")!;
        Assert.Equal("FullTime", link.TextContent);
        Assert.Equal("https://fulltime.thefa.com/displayFixture.html?id=31201701", link.GetAttribute("href"));
        Assert.Null(tables[1].QuerySelector("a[href*='fulltime.thefa.com']"));   // no FullTime id: the name without a link
        var sourceCell = tables[1].QuerySelectorAll("td")[4];                          // Source, then the row's Apply
        Assert.Contains("FullTime", sourceCell.TextContent);
        Assert.Contains("me@example.com", sourceCell.TextContent);                       // who uploaded it…
        Assert.Contains(new DateTime(2026, 11, 20, 9, 0, 0, DateTimeKind.Utc).ToLocalTime().ToString("dd/MM HH:mm"), sourceCell.TextContent);   // …and when
        Assert.NotNull(tables[1].QuerySelectorAll("td")[5].QuerySelector("button[name=RowAction]"));        // Apply is last
    }

    [Fact]
    public void A_row_button_applies_just_that_row_with_its_choice()
    {
        var moved = Moved();
        var added = NewOnFullTime();
        Returns(moved, added);
        Reconciliation.ApplyAsync("FullTime", Arg.Any<IReadOnlyDictionary<string, Resolution>>(), Arg.Any<CancellationToken>())
            .Returns(new ReconciliationApplyResultDto { Updated = 1, Kept = 1 });
        var page = Render<Review>();

        Side(page, added.Id, Resolution.KeepApp).Change(true);                    // choose Fixture Manager's side…
        page.Find($"button[name=RowAction][value='{added.Id}']").Click();        // …press the row's Apply (interactive: recorded)…
        ApplyForm(page).Submit();                                                 // …and the form submits

        Reconciliation.Received(1).ApplyAsync("FullTime",
            Arg.Is<IReadOnlyDictionary<string, Resolution>>(c => c.Count == 1 && c[added.Id] == Resolution.KeepApp),
            Arg.Any<CancellationToken>());
        Assert.Contains("updated 1", page.Markup);
        Assert.Contains("kept as they are 1", page.Markup);
    }

    [Fact]
    public void Says_when_nothing_has_been_fetched_yet()
    {
        Reconciliation.GetReviewAsync("FullTime", Arg.Any<CancellationToken>()).Returns(new ReconciliationReviewDto());

        Assert.Contains("Nothing has been fetched from FullTime yet", Render<Review>().Markup);
    }

    [Fact]
    public void Each_group_is_a_collapsible_section_open_by_default()
    {
        Returns(Moved(), NewOnFullTime());

        var sections = Render<Review>().FindAll("details");

        Assert.Equal(2, sections.Count);
        Assert.All(sections, d => Assert.True(d.HasAttribute("open")));
        Assert.StartsWith("Date moved", sections[0].QuerySelector("summary .section-title")!.TextContent.Trim());
    }

    [Fact]
    public void Filters_from_the_address_show_only_matching_rows()
    {
        Returns(Moved(), NewOnFullTime(), GirlsDropped());
        GoTo("fixtures/review?league=OxGirls");

        var page = Render<Review>();

        Assert.Equal(["Not on FullTime 1"], page.FindAll("summary .section-title").Select(h => h.TextContent.Trim()));
        Assert.Contains("Showing 1 of 3 differences", page.Markup);
        var active = page.FindAll("a[aria-current=true]").Select(a => a.TextContent.Trim()).ToList();
        Assert.Contains(active, a => a.StartsWith("Oxfordshire Girls Football League"));
    }

    [Fact]
    public void Each_filter_value_is_a_button_with_its_count_linking_to_the_filtered_page()
    {
        Returns(Moved(), NewOnFullTime(), GirlsDropped());
        GoTo("fixtures/review");

        var page = Render<Review>();

        Assert.DoesNotContain(page.FindAll(".filter-label"), l => l.TextContent == "Difference");   // the sections already split by difference
        var leagues = Row(page, "League");
        Assert.Equal(["Oxfordshire Girls Football League 1", "Wycombe and South Bucks Minor League 2"], leagues.Select(Text));
        Assert.EndsWith("fixtures/review?league=OxGirls", leagues[0].GetAttribute("href"));
        Assert.Empty(page.FindAll("a[aria-current=true]"));                                   // nothing selected = everything shown
        Assert.DoesNotContain(page.FindAll(".filter-label"), l => l.TextContent == "Team");     // until a league or age group is chosen
        Assert.DoesNotContain(page.FindAll("button"), b => b.TextContent == "Filter");
    }

    [Fact]
    public void Counts_allow_for_the_other_filters_and_changing_league_clears_the_team()
    {
        Returns(Moved(), NewOnFullTime(), GirlsDropped());
        GoTo($"fixtures/review?league=WycAndSouthBucksMinor&team={lions.Id}");

        var page = Render<Review>();

        Assert.Equal(["U13 2"], Row(page, "Age group").Select(Text));                          // counted within the chosen league
        var girlsLink = Row(page, "League").Single(a => Text(a).StartsWith("Oxfordshire")).GetAttribute("href")!;
        Assert.Contains("league=WycAndSouthBucksMinor", girlsLink);                           // adds to the leagues chosen
        Assert.Contains("league=OxGirls", girlsLink);
        Assert.Contains($"team={lions.Id}", girlsLink);                                       // lions still fit
        var wycombeLink = Row(page, "League").Single(a => Text(a).StartsWith("Wycombe")).GetAttribute("href")!;
        Assert.DoesNotContain("league=", wycombeLink);                                        // selected: clicking turns it off…
        Assert.DoesNotContain("team=", wycombeLink);                                          // …and drops lions, no longer in a chosen league
        Assert.Equal([$"{lions.DisplayName} 2"], Row(page, "Team").Select(Text));             // only the chosen leagues' teams
    }

    [Fact]
    public void Apply_only_sends_the_rows_shown()
    {
        var moved = Moved();
        Returns(moved, NewOnFullTime(), GirlsDropped());
        Reconciliation.ApplyAsync("FullTime", Arg.Any<IReadOnlyDictionary<string, Resolution>>(), Arg.Any<CancellationToken>())
            .Returns(new ReconciliationApplyResultDto());
        GoTo("fixtures/review?kind=DateMoved");
        var page = Render<Review>();

        page.Find("button[name=RowAction][value=all]").Click();
        ApplyForm(page).Submit();

        Reconciliation.Received(1).ApplyAsync("FullTime",
            Arg.Is<IReadOnlyDictionary<string, Resolution>>(c => c.Count == 1 && c.ContainsKey(moved.Id)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Several_values_in_a_row_add_together()
    {
        Returns(Moved(), NewOnFullTime(), GirlsDropped());

        GoTo("fixtures/review?league=OxGirls");
        Assert.Contains("Showing 1 of 3 differences", Render<Review>().Markup);

        GoTo("fixtures/review?league=OxGirls&league=WycAndSouthBucksMinor");
        var page = Render<Review>();

        Assert.Equal(["Date moved 1", "Not in Fixture Manager 1", "Not on FullTime 1"],
            page.FindAll("summary .section-title").Select(h => h.TextContent.Trim()));
        Assert.Equal(2, Row(page, "League").Count(a => a.GetAttribute("aria-current") == "true"));
        Assert.Contains("Showing 3 of 3 differences", page.Markup);
    }

    [Fact]
    public void A_sections_Apply_all_applies_just_that_sections_rows_with_their_chosen_sides()
    {
        var moved = Moved();
        var added = NewOnFullTime();
        var dropped = GirlsDropped();
        Returns(moved, added, dropped);
        Reconciliation.ApplyAsync("FullTime", Arg.Any<IReadOnlyDictionary<string, Resolution>>(), Arg.Any<CancellationToken>())
            .Returns(new ReconciliationApplyResultDto());
        var page = Render<Review>();

        Side(page, added.Id, Resolution.KeepApp).Change(true);
        var sectionButton = page.FindAll("summary button").Single(b => b.GetAttribute("value") == "group:OnlyInSource");
        sectionButton.Click();
        ApplyForm(page).Submit();

        Reconciliation.Received(1).ApplyAsync("FullTime",
            Arg.Is<IReadOnlyDictionary<string, Resolution>>(c => c.Count == 1 && c[added.Id] == Resolution.KeepApp),
            Arg.Any<CancellationToken>());
        Assert.Equal(3, page.FindAll("summary button").Count);     // one per section
    }

    [Fact]
    public void Clashes_link_to_the_team_page()
    {
        Teams.GetTeamsAsync(Arg.Any<CancellationToken>()).Returns(new[] { lions });
        Reconciliation.GetReviewAsync("FullTime", Arg.Any<CancellationToken>()).Returns(new ReconciliationReviewDto
        {
            Upload = new SourceUploadSummaryDto { Source = "FullTime", SourceName = "FullTime", UploadedBy = "me@example.com" },
            Items = [Moved()],
            Clashes = [new FixtureClash(lions.Id, Dec6, InSource: true, 2)],
        });

        var link = Render<Review>().Find(".alert-warning li a");

        Assert.Equal((lions.DisplayName, $"teams/details?id={lions.Id}"), (link.TextContent, link.GetAttribute("href")));
    }

    [Fact]
    public void A_rows_Apply_button_carries_the_icon_of_each_side_for_the_page_to_show_the_chosen_one()
    {
        var added = NewOnFullTime();
        Returns(added);

        var button = Render<Review>().Find($"button[name=RowAction][value='{added.Id}']");

        Assert.Contains("bi-plus-circle", button.QuerySelector(".apply-use svg")!.ClassName);     // Add
        Assert.Contains("bi-check-circle", button.QuerySelector(".apply-keep svg")!.ClassName);   // Don't add
        Assert.Equal("Apply", button.QuerySelector(".visually-hidden")!.TextContent);            // still named for screen readers
    }

    /// <summary>The hidden radio behind one side's clickable cell.</summary>
    private static AngleSharp.Dom.IElement Side(IRenderedComponent<Review> page, string id, Resolution side) =>
        page.Find($"input[type=radio][name='Choices[{id}]'][value={side}]");

    private static List<AngleSharp.Dom.IElement> Row(IRenderedComponent<Review> page, string label) =>
        page.FindAll(".filter-label").Single(l => l.TextContent == label).ParentElement!.QuerySelectorAll("a").ToList();

    private static string Text(AngleSharp.Dom.IElement link) => System.Text.RegularExpressions.Regex.Replace(link.TextContent, @"\s+", " ").Trim();
}
