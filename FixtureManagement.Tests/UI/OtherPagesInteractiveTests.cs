using Bunit;
using NSubstitute;
using BookingsIndex = FixtureManagement.UI.Pages.Bookings.Index;
using TeamContactCreate = FixtureManagement.UI.Pages.TeamContacts.Create;

namespace FixtureManagement.Tests.UI;

public class BookingsIndexInteractiveTests : InteractivePageTestBase
{
    public BookingsIndexInteractiveTests()
    {
        Bookings.GetBookingsAsync(Arg.Any<CancellationToken>()).Returns([
            new BookingDto { Id = Guid.NewGuid(), Title = "Old Training", Start = DateTime.Today.AddDays(-30), End = DateTime.Today.AddDays(-30).AddHours(1) },
            new BookingDto { Id = Guid.NewGuid(), Title = "Next Training", Start = DateTime.Today.AddDays(3), End = DateTime.Today.AddDays(3).AddHours(1) },
        ]);
    }

    [Fact]
    public void Past_bookings_are_hidden_until_Show_Past_is_ticked()
    {
        var page = Render<BookingsIndex>();
        Assert.Contains("Next Training", page.Markup);
        Assert.DoesNotContain("Old Training", page.Markup);
        Assert.DoesNotContain("<script", page.Markup);     // scripts don't run interactively, so none is rendered

        page.Find("#show-past").Change(true);

        Assert.Contains("Old Training", page.Markup);
    }
}

public class TeamContactCreateInteractiveTests : InteractivePageTestBase
{
    [Fact]
    public async Task Arriving_from_a_team_uses_that_team_without_a_posted_field()
    {
        var team = new TeamDto { Id = Guid.NewGuid(), Name = "Lions", AgeGroup = AgeGroup.U10 };
        var person = new PersonDto { Id = Guid.NewGuid(), FirstName = "Mia", LastName = "Manager" };
        Teams.GetTeamsAsync(Arg.Any<CancellationToken>()).Returns([team]);
        People.GetPeopleAsync(Arg.Any<CancellationToken>()).Returns([person]);
        TeamContacts.CreateTeamContactAsync(Arg.Any<TeamContactDto>(), Arg.Any<CancellationToken>())
            .Returns(ServiceResult<TeamContactDto>.Success(new TeamContactDto()));
        GoTo($"teamcontacts/create?teamid={team.Id}");

        var page = Render<TeamContactCreate>();
        page.Find("#personid").Change(person.Id.ToString());
        await page.Find("form").SubmitAsync();

        await TeamContacts.Received(1).CreateTeamContactAsync(
            Arg.Is<TeamContactDto>(c => c.TeamId == team.Id && c.PersonId == person.Id), Arg.Any<CancellationToken>());
    }
}
