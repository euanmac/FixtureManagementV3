using System.ComponentModel.DataAnnotations;
using FixtureManagement.Shared.Validation;

namespace FixtureManagement.Tests.Shared;

public class FixtureRulesTests
{
    [Theory]
    [InlineData(true, FixtureType.League, true)]
    [InlineData(true, FixtureType.Friendly, true)]
    [InlineData(true, FixtureType.CountyCup, true)]
    [InlineData(true, FixtureType.Postponed, false)]
    [InlineData(true, FixtureType.Cancelled, false)]
    [InlineData(false, FixtureType.League, false)]
    public void CanAllocate_only_home_fixtures_that_are_going_ahead(bool isHome, FixtureType type, bool expected)
    {
        Assert.Equal(expected, FixtureRules.CanAllocate(isHome, type));
    }

    [Fact]
    public void IsAllocated_needs_an_allocation_a_pitch_and_a_time()
    {
        var pitch = Guid.NewGuid();
        Assert.True(FixtureRules.IsAllocated(true, pitch, new TimeOnly(9, 30)));
        Assert.True(FixtureRules.IsAllocated(true, pitch, new TimeOnly(0, 15)));
        Assert.False(FixtureRules.IsAllocated(false, pitch, new TimeOnly(9, 30)));
        Assert.False(FixtureRules.IsAllocated(true, Guid.Empty, new TimeOnly(9, 30)));
        Assert.False(FixtureRules.IsAllocated(true, pitch, new TimeOnly(0, 0))); // midnight = "no time set"
    }

    [Fact]
    public void AllocationEnd_adds_the_duration()
    {
        Assert.Equal(new TimeOnly(11, 0), FixtureRules.AllocationEnd(new TimeOnly(9, 30), TimeSpan.FromMinutes(90)));
    }

    [Fact]
    public void IsAllocationComplete_needs_a_pitch_and_a_duration()
    {
        Assert.True(FixtureRules.IsAllocationComplete(Guid.NewGuid(), TimeSpan.FromMinutes(60)));
        Assert.False(FixtureRules.IsAllocationComplete(Guid.Empty, TimeSpan.FromMinutes(60)));
        Assert.False(FixtureRules.IsAllocationComplete(Guid.NewGuid(), TimeSpan.Zero));
    }

    [Fact]
    public void IsBookingPast_compares_with_the_start_of_today()
    {
        var now = new DateTime(2026, 11, 6, 15, 0, 0);
        Assert.True(FixtureRules.IsBookingPast(new DateTime(2026, 11, 5, 23, 59, 0), now));
        Assert.False(FixtureRules.IsBookingPast(new DateTime(2026, 11, 6, 8, 0, 0), now)); // earlier today still counts as current
        Assert.False(FixtureRules.IsBookingPast(new DateTime(2026, 11, 7), now));
    }

    [Theory]
    [InlineData(FixtureType.Friendly, "F")]
    [InlineData(FixtureType.League, "L")]
    [InlineData(FixtureType.Cup, "C")]
    [InlineData(FixtureType.CountyCup, "CC")]
    [InlineData(FixtureType.Postponed, "P")]
    [InlineData(FixtureType.Cancelled, "X")]
    [InlineData(FixtureType.Other, "O")]
    public void FixtureTypeShortName_codes(FixtureType type, string expected)
    {
        Assert.Equal(expected, type.FixtureTypeShortName());
    }

    [Fact]
    public void FixtureType_keeps_its_database_values()
    {
        // Stored as integers in local.db, so these must never change.
        Assert.Equal(0, (int)FixtureType.Friendly);
        Assert.Equal(1, (int)FixtureType.League);
        Assert.Equal(2, (int)FixtureType.Cup);
        Assert.Equal(3, (int)FixtureType.Cancelled);
        Assert.Equal(4, (int)FixtureType.Postponed);
        Assert.Equal(5, (int)FixtureType.Other);
        Assert.Equal(6, (int)FixtureType.CountyCup);
    }
}

public class FixtureDtoTests
{
    [Fact]
    public void Derived_properties_follow_the_rules()
    {
        var dto = new FixtureDto
        {
            Team = new TeamSummaryDto { Id = Guid.NewGuid(), Name = "Lions", AgeGroup = AgeGroup.U9, Gender = Gender.Female },
            IsHome = true,
            FixtureType = FixtureType.CountyCup,
            Allocation = new FixtureAllocationDto { PitchId = Guid.NewGuid(), Start = new TimeOnly(10, 0), Duration = TimeSpan.FromMinutes(60) },
        };

        Assert.Equal("U9 Girls - Lions", dto.Team.DisplayName);
        Assert.Equal("CC", dto.FixtureTypeShortName);
        Assert.True(dto.CanAllocate);
        Assert.True(dto.IsAllocated);
        Assert.Equal(new TimeOnly(11, 0), dto.Allocation!.End);
        Assert.True(dto.Allocation.IsComplete);
    }

    [Fact]
    public void A_fixture_without_an_allocation_is_not_allocated()
    {
        Assert.False(new FixtureDto { IsHome = true }.IsAllocated);
    }
}

public class DtoValidationTests
{
    private static List<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void NotEmptyGuid_rejects_Guid_Empty_only()
    {
        var attribute = new NotEmptyGuidAttribute();
        Assert.False(attribute.IsValid(Guid.Empty));
        Assert.True(attribute.IsValid(Guid.NewGuid()));
        Assert.True(attribute.IsValid(null)); // leave "missing" to [Required]
    }

    [Fact]
    public void Booking_needs_a_title_and_a_pitch()
    {
        var booking = new BookingDto { Title = "", Start = new DateTime(2026, 11, 7, 9, 0, 0), End = new DateTime(2026, 11, 7, 10, 0, 0) };

        var members = Validate(booking).SelectMany(r => r.MemberNames).ToList();

        Assert.Contains(nameof(BookingDto.Title), members);
        Assert.Contains(nameof(BookingDto.PitchId), members);
    }

    // Note: .NET only runs IValidatableObject.Validate once all the attribute checks pass,
    // so cross-field rules are tested on an otherwise valid DTO.
    [Fact]
    public void Booking_end_must_be_after_start()
    {
        var booking = new BookingDto { Title = "Training", PitchId = Guid.NewGuid(), Start = new DateTime(2026, 11, 7, 10, 0, 0), End = new DateTime(2026, 11, 7, 10, 0, 0) };

        var result = Assert.Single(Validate(booking));
        Assert.Equal([nameof(BookingDto.End)], result.MemberNames);
    }

    [Fact]
    public void A_complete_booking_is_valid()
    {
        var booking = new BookingDto { Title = "Training", PitchId = Guid.NewGuid(), Start = new DateTime(2026, 11, 7, 9, 0, 0), End = new DateTime(2026, 11, 7, 10, 0, 0) };
        Assert.Empty(Validate(booking));
    }

    [Fact]
    public void Fixture_needs_a_team_and_an_opponent()
    {
        var members = Validate(new FixtureDto()).SelectMany(r => r.MemberNames).ToList();
        Assert.Contains(nameof(FixtureDto.TeamId), members);
        Assert.Contains(nameof(FixtureDto.Opponent), members);
    }

    [Fact]
    public void Allocation_needs_a_pitch()
    {
        var results = Validate(new FixtureAllocationDto { Start = new TimeOnly(9, 30), Duration = TimeSpan.FromHours(1) });
        var result = Assert.Single(results);
        Assert.Equal([nameof(FixtureAllocationDto.PitchId)], result.MemberNames);
        Assert.Equal("Pitch is required", result.ErrorMessage);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-30)]
    public void Allocation_duration_must_be_positive(int minutes)
    {
        var results = Validate(new FixtureAllocationDto { PitchId = Guid.NewGuid(), Start = new TimeOnly(9, 30), Duration = TimeSpan.FromMinutes(minutes) });
        var result = Assert.Single(results);
        Assert.Equal([nameof(FixtureAllocationDto.Duration)], result.MemberNames);
    }

    [Fact]
    public void Person_needs_first_and_last_name_but_not_contact_details()
    {
        var members = Validate(new PersonDto()).SelectMany(r => r.MemberNames).ToList();
        Assert.Equal([nameof(PersonDto.FirstName), nameof(PersonDto.LastName)], members.Order());
    }

    [Fact]
    public void TeamContact_needs_a_team_and_a_person()
    {
        var members = Validate(new TeamContactDto()).SelectMany(r => r.MemberNames).ToList();
        Assert.Contains(nameof(TeamContactDto.TeamId), members);
        Assert.Contains(nameof(TeamContactDto.PersonId), members);
    }
}

public class ServiceResultTests
{
    [Fact]
    public void From_copies_a_failure_into_a_typed_result()
    {
        var typed = ServiceResult<FixtureDto>.From(ServiceResult.Invalid("TeamId", "Team not found."));

        Assert.Equal(ServiceStatus.Invalid, typed.Status);
        Assert.False(typed.Succeeded);
        Assert.Null(typed.Value);
        Assert.Equal(["Team not found."], typed.Errors["TeamId"]);
    }

    [Fact]
    public void Success_carries_the_value()
    {
        var result = ServiceResult<string>.Success("ok");
        Assert.True(result.Succeeded);
        Assert.Equal("ok", result.Value);
    }
}
