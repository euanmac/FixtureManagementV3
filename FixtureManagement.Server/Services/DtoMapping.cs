using FixtureManagement.Server.Models;
using FixtureManagement.Shared.Dtos;

namespace FixtureManagement.Server.Services;

/// <summary>Entity → DTO conversions shared by the EF services. Navigation properties must be loaded first.</summary>
internal static class DtoMapping
{
    public static TeamSummaryDto ToSummaryDto(this Team team) => new()
    {
        Id = team.Id,
        Name = team.Name,
        AgeGroup = team.AgeGroup,
        Gender = team.Gender,
        League = team.League,
        Division = team.Division,
        RefRequired = team.RefRequired,
    };

    public static PitchDto ToDto(this Pitch pitch) => new()
    {
        Id = pitch.Id,
        Name = pitch.Name ?? "",
        DisplayOrder = pitch.DisplayOrder,
    };

    public static BookingDto ToDto(this Booking booking) => booking.ToDto(booking.Pitch?.Name);

    /// <summary>For when the pitch name was looked up separately (it may be missing).</summary>
    public static BookingDto ToDto(this Booking booking, string? pitchName) => new()
    {
        Id = booking.Id,
        Title = booking.Title ?? "",
        Start = booking.Start,
        End = booking.End,
        PitchId = booking.PitchId,
        PitchName = pitchName ?? "",
        IsRecurring = booking.IsRecurring,
    };

    public static PersonDto ToDto(this Person person, IEnumerable<TeamContact> contacts) => new()
    {
        Id = person.Id,
        FirstName = person.FirstName ?? "",
        LastName = person.LastName ?? "",
        Tel = person.Tel ?? "",
        Email = person.Email ?? "",
        IsRef = person.IsRef,
        Teams = contacts
            .Where(c => c.PersonId == person.Id && c.Team is not null)
            .OrderBy(c => c.Team!.AgeGroup)
                .ThenBy(c => c.Team!.Gender)
                .ThenBy(c => c.ContactType)
            .Select(c => new PersonTeamDto
            {
                TeamContactId = c.Id,
                ContactType = c.ContactType,
                Team = c.Team!.ToSummaryDto(),
            })
            .ToList(),
    };

    public static TeamContactDto ToDto(this TeamContact contact) => new()
    {
        Id = contact.Id,
        TeamId = contact.TeamId,
        PersonId = contact.PersonId,
        ContactType = contact.ContactType,
        FirstName = contact.Person?.FirstName ?? "",
        LastName = contact.Person?.LastName ?? "",
        Email = contact.Person?.Email ?? "",
        Team = contact.Team?.ToSummaryDto() ?? new TeamSummaryDto { Id = contact.TeamId },
    };

    public static FixtureDto ToDto(this Fixture fixture) => new()
    {
        Id = fixture.Id,
        TeamId = fixture.TeamId,
        Team =fixture.Team?.ToSummaryDto() ?? new TeamSummaryDto { Id = fixture.TeamId },
        Date = fixture.Date,
        IsHome = fixture.IsHome,
        Opponent = fixture.Opponent ?? "",
        FixtureType = fixture.FixtureType,
        Allocation = fixture.FixtureAllocation?.ToDto(),
    };

    public static FixtureAllocationDto ToDto(this FixtureAllocation allocation) => new()
    {
        Id = allocation.Id,
        PitchId = allocation.PitchId,
        PitchName = allocation.Pitch?.Name ?? "",
        Start = allocation.Start,
        Duration = allocation.Duration,
        IsConfirmed = allocation.IsConfirmed,
    };
}
