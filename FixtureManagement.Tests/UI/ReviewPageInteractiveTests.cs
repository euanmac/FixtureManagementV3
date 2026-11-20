using Bunit;
using FixtureManagement.Shared.Reconciliation;
using FixtureManagement.UI.Pages.Fixtures;
using NSubstitute;

namespace FixtureManagement.Tests.UI;

/// <summary>/fixtures/review: Review FullTime, grouped by team.</summary>
public class ReviewPageInteractiveTests : InteractivePageTestBase
{
    private static readonly DateOnly Sat = DateOnly.FromDateTime(DateTime.Today).AddDays(7);
    private readonly TeamDto lions = new() { Id = Guid.NewGuid(), Name = "Lions", AgeGroup = AgeGroup.U13, League = League.WycAndSouthBucksMinor, FullTimeLeagueId = "1", FullTimeTeamId = "2" };
    private readonly TeamDto tigers = new() { Id = Guid.NewGuid(), Name = "Tigers", AgeGroup = AgeGroup.U14, League = League.WycAndSouthBucksMinor, FullTimeLeagueId = "1", FullTimeTeamId = "3" };
    private readonly TeamDto minis = new() { Id = Guid.NewGuid(), Name = "Minis", AgeGroup = AgeGroup.U7, League = League.SouthBucksMini };
    private readonly FixtureDto lionsLeague;
    private readonly FixtureDto lionsDropped;
    private readonly FixtureDto tigersGame;
    private readonly FixtureDto minisGame;
    private readonly ReconciliationItem missing;

    public ReviewPageInteractiveTests()
    {
        lionsLeague = Fixture(lions, Sat, "Same FC");
        lionsDropped = Fixture(lions, Sat, "Dropped FC");            // same day: Gameweek-style grouping
        tigersGame = Fixture(tigers, Sat, "Tigers' Rivals");
        minisGame = Fixture(minis, Sat, "Minis Rivals");
        missing = new ReconciliationItem
        {
            Kind = DifferenceKind.OnlyInSource, Team = lions, Suggested = Resolution.UseSource,
            Source = new SourceFixture { TeamId = lions.Id, Date = Sat.AddDays(7), Opponent = "New FC", FixtureType = FixtureType.Cup },
        };

        Teams.GetTeamsAsync(Arg.Any<CancellationToken>()).Returns([lions, tigers, minis]);
        Fixtures.GetFixturesAsync(Arg.Any<FixtureQuery?>(), Arg.Any<CancellationToken>()).Returns([lionsLeague, lionsDropped, tigersGame, minisGame]);
        Reconciliation.GetWeekAsync("FullTime", Arg.Any<DateOnly>(), DateOnly.MaxValue, Arg.Any<CancellationToken>()).Returns(new ReconciliationWeekDto
        {
            Upload = new SourceUploadSummaryDto { Source = "FullTime", SourceName = "FullTime", UploadedBy = "me@example.com" },
            CoveredTeamIds = [lions.Id, tigers.Id],
            CoveredFrom = DateOnly.FromDateTime(DateTime.Today),
            Items =
            [
                new ReconciliationItem { Kind = DifferenceKind.Same, Team = lions, App = lionsLeague, Source = Src(lions, "Same FC") },
                new ReconciliationItem { Kind = DifferenceKind.OnlyInApp, Team = lions, App = lionsDropped, Suggested = Resolution.UseSource },
                missing,
                new ReconciliationItem { Kind = DifferenceKind.Same, Team = tigers, App = tigersGame, Source = Src(tigers, "Tigers' Rivals") },
            ],
        });
        Reconciliation.ApplyAsync("FullTime", Arg.Any<IReadOnlyDictionary<string, Resolution>>(), Arg.Any<CancellationToken>())
            .Returns(new ReconciliationApplyResultDto { Added = 1 });
        GoTo("fixtures/review");
    }

    private static FixtureDto Fixture(TeamDto team, DateOnly date, string opponent) =>
        new() { Id = Guid.NewGuid(), TeamId = team.Id, Date = date, Opponent = opponent, IsHome = true, FixtureType = FixtureType.League };

    private static SourceFixture Src(TeamDto team, string opponent) =>
        new() { TeamId = team.Id, Date = Sat, Opponent = opponent, IsHome = true, FixtureType = FixtureType.League };

    private static AngleSharp.Dom.IElement Section(IRenderedComponent<Review> page, TeamDto team) =>
        page.FindAll("details.team-section").Single(d => d.QuerySelector(".team-name")!.TextContent == team.DisplayName);

    [Fact]
    public void Every_team_has_a_closed_section_whose_header_says_what_needs_resolving()
    {
        var page = Render<Review>();

        Assert.Equal(3, page.FindAll("details.team-section").Count);
        Assert.All(page.FindAll("details.team-section"), d => Assert.False(d.HasAttribute("open")));
        Assert.Contains("2 to resolve", Section(page, lions).QuerySelector("summary")!.TextContent);
        Assert.Contains("3 fixtures", Section(page, lions).QuerySelector("summary")!.TextContent);
        Assert.Contains("All match", Section(page, tigers).QuerySelector("summary")!.TextContent);
        Assert.Contains("Not on FullTime", Section(page, minis).QuerySelector("summary")!.TextContent);
    }

    [Fact]
    public void Each_fixture_shows_its_status_and_games_on_the_same_day_share_the_date()
    {
        var page = Render<Review>();

        var rows = Section(page, lions).QuerySelectorAll("tbody tr");
        var texts = rows.Select(r => r.QuerySelectorAll("td")[1].TextContent).ToList();
        Assert.Contains(texts, t => t.Contains("Matches FullTime"));
        Assert.Contains(texts, t => t.Contains("Not on FullTime"));
        Assert.Contains(texts, t => t.Contains("Missing from Fixture Manager"));
        var dates = rows.Select(r => r.QuerySelectorAll("td")[0].TextContent.Trim()).ToList();
        Assert.Equal(1, dates.Count(d => d == Sat.ToString("ddd dd/MM/yy")));        // the two games on Sat: date shown once
        Assert.Equal("", dates[1]);
        Assert.Empty(rows[0].QuerySelectorAll("button"));                             // a match: nothing to do
    }

    [Fact]
    public void An_icon_button_applies_its_choice_and_keeps_the_team_open()
    {
        var page = Render<Review>();
        var add = Section(page, lions).QuerySelector($"button[value='{lions.Id}|{missing.Id}|UseSource']")!;
        Assert.Equal("Add this FullTime fixture to Fixture Manager", add.GetAttribute("title"));
        Assert.Contains("bi-cloud-arrow-down", add.QuerySelector("svg")!.ClassName);    // Gameweek's download-from-FullTime icon

        add.Click();
        page.Find("form").Submit();

        Reconciliation.Received(1).ApplyAsync("FullTime",
            Arg.Is<IReadOnlyDictionary<string, Resolution>>(c => c.Count == 1 && c[missing.Id] == Resolution.UseSource),
            Arg.Any<CancellationToken>());
        Assert.DoesNotContain("Added 1", page.Markup);                                 // no result banner
        Assert.Empty(page.FindAll(".alert"));
        Assert.True(Section(page, lions).HasAttribute("open"));
    }

    [Fact]
    public void The_FullTime_filter_shows_only_teams_on_or_off_FullTime()
    {
        GoTo("fixtures/review?ft=off");

        var page = Render<Review>();

        Assert.Equal([minis.DisplayName], page.FindAll(".team-name").Select(n => n.TextContent));
        var ftRow = page.FindAll(".filter-label").Single(l => l.TextContent == "FullTime").ParentElement!;
        Assert.Equal(["On FullTime 2", "Not on FullTime 1"],
            ftRow.QuerySelectorAll("a").Select(a => System.Text.RegularExpressions.Regex.Replace(a.TextContent, @"\s+", " ").Trim()));
    }

    [Fact]
    public void An_accepted_fixture_is_not_a_match_and_keeps_its_way_back()
    {
        var notAdded = missing with { Kept = true, KeptBy = "me@example.com", KeptAt = DateTime.UtcNow };
        Reconciliation.GetWeekAsync("FullTime", Arg.Any<DateOnly>(), DateOnly.MaxValue, Arg.Any<CancellationToken>()).Returns(new ReconciliationWeekDto
        {
            Upload = new SourceUploadSummaryDto { Source = "FullTime", SourceName = "FullTime" },
            CoveredTeamIds = [lions.Id],
            CoveredFrom = DateOnly.FromDateTime(DateTime.Today),
            Items = [new ReconciliationItem { Kind = DifferenceKind.Same, Team = lions, App = lionsLeague, Source = Src(lions, "Same FC") }, notAdded],
        });
        Fixtures.GetFixturesAsync(Arg.Any<FixtureQuery?>(), Arg.Any<CancellationToken>()).Returns([lionsLeague]);

        var page = Render<Review>();

        var row = Section(page, lions).QuerySelectorAll("tbody tr").Single(r => r.TextContent.Contains("New FC"));
        Assert.Contains("Accepted", row.TextContent);
        Assert.Contains("Not added – kept by me@example.com", row.TextContent);
        var buttons = row.QuerySelectorAll("button");
        Assert.Equal("Add this FullTime fixture to Fixture Manager", Assert.Single(buttons).GetAttribute("title"));   // no "Don't add" again
        Assert.DoesNotContain("to resolve", Section(page, lions).QuerySelector("summary")!.TextContent);         // decided
        Assert.Contains("1 accepted", Section(page, lions).QuerySelector("summary")!.TextContent);
        Assert.DoesNotContain("All match", Section(page, lions).QuerySelector("summary")!.TextContent);
    }

    [Fact]
    public void A_FullTime_team_missing_from_the_upload_says_not_fetched_rather_than_all_match()
    {
        Reconciliation.GetWeekAsync("FullTime", Arg.Any<DateOnly>(), DateOnly.MaxValue, Arg.Any<CancellationToken>()).Returns(new ReconciliationWeekDto
        {
            Upload = new SourceUploadSummaryDto { Source = "FullTime", SourceName = "FullTime" },
            CoveredTeamIds = [lions.Id],                // tigers weren't fetched
            CoveredFrom = DateOnly.FromDateTime(DateTime.Today),
            Items = [new ReconciliationItem { Kind = DifferenceKind.Same, Team = lions, App = lionsLeague, Source = Src(lions, "Same FC") }],
        });
        Fixtures.GetFixturesAsync(Arg.Any<FixtureQuery?>(), Arg.Any<CancellationToken>()).Returns([lionsLeague, tigersGame]);

        var page = Render<Review>();

        var tigersHeader = Section(page, tigers).QuerySelector("summary")!.TextContent;
        Assert.Contains("Not fetched from FullTime", tigersHeader);
        Assert.DoesNotContain("All match", tigersHeader);
        Assert.Contains("All match", Section(page, lions).QuerySelector("summary")!.TextContent);
    }
}
