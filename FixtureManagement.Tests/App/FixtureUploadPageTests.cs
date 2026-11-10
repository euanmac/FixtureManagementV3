using System.Text;
using Bunit;
using FixtureManagement.App.Pages;
using FixtureManagement.FullTime;
using FixtureManagement.Tests.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FixtureManagement.Tests.App;

/// <summary>The desktop app's upload page (the website's Upload2 ported to interactive rendering).</summary>
public class FixtureUploadPageTests : InteractivePageTestBase
{
    private readonly TeamDto team = new()
    {
        Id = Guid.NewGuid(), Name = "Thame United Lions", League = League.SouthBucksMini, FullTimeLeagueId = "1", FullTimeTeamId = "2",
    };

    public FixtureUploadPageTests()
    {
        Teams.GetTeamAsync(team.Id, Arg.Any<CancellationToken>()).Returns(team);
        Fixtures.CreateFixturesAsync(Arg.Any<IReadOnlyList<FixtureDto>>(), Arg.Any<CancellationToken>())
            .Returns(call => ServiceResult<IReadOnlyList<FixtureDto>>.Success(call.Arg<IReadOnlyList<FixtureDto>>()));
        GoTo($"fixtures/upload2?teamId={team.Id}");
    }

    private string CurrentUrl
    {
        get
        {
            var navigation = Services.GetRequiredService<NavigationManager>();
            return navigation.ToBaseRelativePath(navigation.Uri);
        }
    }

    private static InputFileContent Csv(string text) => InputFileContent.CreateFromText(text, "fixtures.csv");

    private const string TwoFixtures = "Opponent,Venue,Date\nRivals FC,Home,2026-11-14\nNext Week FC,Away,2026-11-21";

    [Fact]
    public void Picking_a_CSV_lists_its_fixtures_all_ticked()
    {
        var page = Render<FixtureUpload>();

        page.FindComponent<InputFile>().UploadFiles(Csv(TwoFixtures));

        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll("tbody tr").Count));
        Assert.Contains("Rivals FC", page.Markup);
        Assert.All(page.FindAll("input.add-fixture"), box => Assert.True(box.HasAttribute("checked")));
        Assert.Contains("Create 2", page.FindAll("button").Single(b => b.TextContent.Contains("Create")).TextContent);
    }

    [Fact]
    public void Create_adds_only_the_ticked_fixtures_then_goes_back_to_the_team()
    {
        var page = Render<FixtureUpload>();
        page.FindComponent<InputFile>().UploadFiles(Csv(TwoFixtures));
        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll("input.add-fixture").Count));

        page.FindAll("input.add-fixture")[1].Change(false);   // untick "Next Week FC"
        page.FindAll("button").Single(b => b.TextContent.Contains("Create")).Click();

        page.WaitForAssertion(() => Assert.Equal($"teams/details?id={team.Id}", CurrentUrl));
        Fixtures.Received(1).CreateFixturesAsync(
            Arg.Is<IReadOnlyList<FixtureDto>>(list => list.Count == 1 && list[0].Opponent == "Rivals FC" && list[0].TeamId == team.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Select_all_unticks_and_reticks_everything()
    {
        var page = Render<FixtureUpload>();
        page.FindComponent<InputFile>().UploadFiles(Csv(TwoFixtures));
        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll("input.add-fixture").Count));

        page.Find("#select-all").Change(false);
        Assert.All(page.FindAll("input.add-fixture"), box => Assert.False(box.HasAttribute("checked")));
        Assert.True(page.FindAll("button").Single(b => b.TextContent.Contains("Create")).HasAttribute("disabled"));

        page.Find("#select-all").Change(true);
        Assert.All(page.FindAll("input.add-fixture"), box => Assert.True(box.HasAttribute("checked")));
    }

    [Fact]
    public void A_bad_file_says_what_is_wrong()
    {
        var page = Render<FixtureUpload>();

        page.FindComponent<InputFile>().UploadFiles(Csv("Team,Where,When\nRivals,Home,2026-11-14"));

        page.WaitForAssertion(() => Assert.Contains("Opponent, Venue and Date", page.Find(".alert-danger").TextContent));
        Assert.Empty(page.FindAll("tbody tr"));
    }

    [Fact]
    public void When_FullTime_refuses_it_suggests_uploading_a_CSV()
    {
        FullTime.GetFixturesAsync(team, Arg.Any<CancellationToken>()).ThrowsAsync(new FullTimeRefusedException("FullTime refused the request (403)."));
        var page = Render<FixtureUpload>();

        page.FindAll("button").Single(b => b.TextContent.Contains("Load from FullTime")).Click();

        page.WaitForAssertion(() => Assert.Contains("refused", page.Find(".alert-danger").TextContent));
        Assert.Contains("upload that instead", page.Find(".alert-danger").TextContent);
    }

    [Fact]
    public void Loading_from_FullTime_lists_its_fixtures()
    {
        FullTime.GetFixturesAsync(team, Arg.Any<CancellationToken>()).Returns(new List<FullTimeFixture>
        {
            new() { Opponent = "Rivals FC", IsHome = true, Date = new DateOnly(2026, 11, 14), FixtureType = FixtureType.Cup },
        });
        var page = Render<FixtureUpload>();

        page.FindAll("button").Single(b => b.TextContent.Contains("Load from FullTime")).Click();

        page.WaitForAssertion(() => Assert.Single(page.FindAll("tbody tr")));
        Assert.Contains("Cup", page.Markup);
    }

    [Fact]
    public void A_failed_create_keeps_the_list_and_shows_why()
    {
        Fixtures.CreateFixturesAsync(Arg.Any<IReadOnlyList<FixtureDto>>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<IReadOnlyList<FixtureDto>>.From(ServiceResult.Invalid("Opponent", "Opponent is required.")));
        var page = Render<FixtureUpload>();
        page.FindComponent<InputFile>().UploadFiles(Csv(TwoFixtures));
        page.WaitForAssertion(() => Assert.Equal(2, page.FindAll("tbody tr").Count));

        page.FindAll("button").Single(b => b.TextContent.Contains("Create")).Click();

        page.WaitForAssertion(() => Assert.Contains("Opponent is required.", page.Find(".alert-danger").TextContent));
        Assert.Equal(2, page.FindAll("tbody tr").Count);
        Assert.StartsWith("fixtures/upload2", CurrentUrl);
    }
}
