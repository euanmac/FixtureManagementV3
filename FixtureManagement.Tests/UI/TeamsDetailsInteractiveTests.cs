using Bunit;
using NSubstitute;
using TeamDetails = FixtureManagement.UI.Pages.Teams.Details;

namespace FixtureManagement.Tests.UI;

public class TeamsDetailsInteractiveTests : InteractivePageTestBase
{
    private readonly TeamDto team;
    private readonly Guid managerId = Guid.NewGuid();
    private readonly Guid coachId = Guid.NewGuid();

    public TeamsDetailsInteractiveTests()
    {
        team = new TeamDto
        {
            Id = Guid.NewGuid(), Name = "Lions", AgeGroup = AgeGroup.U10, Gender = Gender.Male,
            Contacts = [
                new TeamContactSummaryDto { Id = managerId, ContactType = ContactType.Manager, FirstName = "Mia", LastName = "Manager", Email = "mia@example.com" },
                new TeamContactSummaryDto { Id = coachId, ContactType = ContactType.Coach, FirstName = "Cal", LastName = "Coach", Email = "cal@example.com" },
            ],
        };
        Teams.GetTeamAsync(team.Id, Arg.Any<CancellationToken>()).Returns(team);
        GoTo($"teams/details?id={team.Id}");
    }

    [Fact]
    public void Renders_the_team_and_its_contacts()
    {
        var page = Render<TeamDetails>();

        Assert.Contains("U10 Boys - Lions", page.Markup);
        Assert.Contains("mailto:mia@example.com", page.Markup);
        Assert.Contains("Cal Coach", page.Markup);
    }

    [Fact]
    public async Task Clicking_remove_deletes_that_contact_and_reloads()
    {
        var page = Render<TeamDetails>();

        // The second remove button belongs to the coach.
        await page.FindAll("button.btn-link")[1].ClickAsync(new());

        await TeamContacts.Received(1).DeleteTeamContactAsync(coachId, Arg.Any<CancellationToken>());
        await TeamContacts.DidNotReceive().DeleteTeamContactAsync(managerId, Arg.Any<CancellationToken>());
        await Teams.Received(2).GetTeamAsync(team.Id, Arg.Any<CancellationToken>());   // initial load + reload in place
    }

    [Fact]
    public async Task Delete_fixtures_deletes_this_teams_fixtures()
    {
        var page = Render<TeamDetails>();

        await page.Find("button.btn-danger").ClickAsync(new());

        await Fixtures.Received(1).DeleteFixturesAsync(team.Id, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Signed_out_visitors_get_no_remove_or_delete_buttons()
    {
        Auth.SetNotAuthorized();
        var page = Render<TeamDetails>();

        Assert.Empty(page.FindAll("button.btn-link"));
        Assert.Empty(page.FindAll("button.btn-danger"));
        Assert.DoesNotContain("mailto:", page.Markup);
    }
}
