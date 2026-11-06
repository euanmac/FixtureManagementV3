using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace FixtureManagement.Tests.UI;

/// <summary>
/// Renders UI pages the way the desktop app (Blazor Hybrid) will: interactively, with no form posts,
/// so values must come from clicks/bindings rather than [SupplyParameterFromForm]. Services are fakes.
/// </summary>
public abstract class InteractivePageTestBase : BunitContext
{
    protected ITeamService Teams { get; } = Substitute.For<ITeamService>();
    protected IFixtureService Fixtures { get; } = Substitute.For<IFixtureService>();
    protected ITeamContactService TeamContacts { get; } = Substitute.For<ITeamContactService>();
    protected IPersonService People { get; } = Substitute.For<IPersonService>();
    protected IPitchService Pitches { get; } = Substitute.For<IPitchService>();
    protected IBookingService Bookings { get; } = Substitute.For<IBookingService>();
    protected IFullTimeService FullTime { get; } = Substitute.For<IFullTimeService>();

    /// <summary>Signed in by default; call Auth.SetNotAuthorized() to test the signed-out view.</summary>
    protected BunitAuthorizationContext Auth { get; }

    protected InteractivePageTestBase()
    {
        // Register the fakes first: bUnit freezes the service list once anything (even the settings below) resolves one.
        Services.AddSingleton(Teams);
        Services.AddSingleton(Fixtures);
        Services.AddSingleton(TeamContacts);
        Services.AddSingleton(People);
        Services.AddSingleton(Pitches);
        Services.AddSingleton(Bookings);
        Services.AddSingleton(FullTime);

        // Order matters: AddAuthorization registers services; the two settings after it resolve some.
        Auth = AddAuthorization();
        Auth.SetAuthorized("tester@example.com");
        SetRendererInfo(new RendererInfo("Interactive", isInteractive: true));
        JSInterop.Mode = JSRuntimeMode.Loose;   // QuickGrid and friends call JS; not under test here

        // Sensible empty defaults so pages render; individual tests override what they need.
        Teams.GetTeamsAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<TeamDto>());
        People.GetPeopleAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<PersonDto>());
        Pitches.GetPitchesAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<PitchDto>());
        Fixtures.GetFixturesAsync(Arg.Any<FixtureQuery?>(), Arg.Any<CancellationToken>()).Returns(Array.Empty<FixtureDto>());
        Bookings.GetBookingsAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<BookingDto>());
    }

    /// <summary>Sets the page's URL (and so its [SupplyParameterFromQuery] values) before rendering.</summary>
    protected void GoTo(string relativeUrl) => Services.GetRequiredService<NavigationManager>().NavigateTo(relativeUrl);
}
