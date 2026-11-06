using System.ComponentModel.DataAnnotations;
using FixtureManagement.Shared.Models;
using FixtureManagement.Shared.Validation;

namespace FixtureManagement.Shared.Dtos;

/// <summary>A fixture with its team and pitch allocation, as returned by IFixtureService / GET /api/fixtures.</summary>
public record FixtureDto
{
    public Guid Id { get; init; }
    public TeamSummaryDto Team { get; init; } = new();
    public DateOnly Date { get; init; }
    public bool IsHome { get; init; }
    public string Opponent { get; init; } = "";
    public FixtureType FixtureType { get; init; }
    /// <summary>Null until a pitch and time have been set.</summary>
    public FixtureAllocationDto? Allocation { get; init; }

    public Guid TeamId => Team.Id;
    public string FixtureTypeShortName => FixtureType.FixtureTypeShortName();
    public bool CanAllocate => FixtureRules.CanAllocate(IsHome, FixtureType);
    public bool IsAllocated => Allocation is not null
        && FixtureRules.IsAllocated(true, Allocation.PitchId, Allocation.Start);
}

/// <summary>When and where a home fixture is played.</summary>
public record FixtureAllocationDto
{
    public Guid Id { get; init; }
    public Guid PitchId { get; init; }
    /// <summary>Empty if no pitch is set.</summary>
    public string PitchName { get; init; } = "";
    public TimeOnly Start { get; init; }
    public TimeSpan Duration { get; init; }
    public bool IsConfirmed { get; init; }

    public TimeOnly End => FixtureRules.AllocationEnd(Start, Duration);
    public bool IsComplete => FixtureRules.IsAllocationComplete(PitchId, Duration);
}

/// <summary>Fields a user can set when creating or editing a fixture. Mutable so EditForm can bind to it.</summary>
public class FixtureEditDto
{
    [Display(Name = "Team")]
    [NotEmptyGuid]
    public Guid TeamId { get; set; }
    [DataType(DataType.Date)]
    public DateOnly Date { get; set; }
    [Display(Name = "Home")]
    public bool IsHome { get; set; }
    [Required]
    public string Opponent { get; set; } = "";
    [Display(Name = "Type")]
    public FixtureType FixtureType { get; set; }
}

/// <summary>Sets a fixture's pitch and kick-off. Mutable so EditForm can bind to it.</summary>
public class FixtureAllocationEditDto : IValidatableObject
{
    [Display(Name = "Pitch")]
    [NotEmptyGuid(ErrorMessage = "Pitch is required")]
    public Guid PitchId { get; set; }
    [Required]
    public TimeOnly Start { get; set; }
    [Required]
    [DisplayFormat(DataFormatString = "{0:HH\\:mm}")]
    public TimeSpan Duration { get; set; }
    [Display(Name = "Confirmed")]
    public bool IsConfirmed { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Duration <= TimeSpan.Zero)
            yield return new ValidationResult("Duration must be greater than zero.", [nameof(Duration)]);
    }
}

/// <summary>Filters for listing fixtures. All optional; dates are inclusive.</summary>
public record FixtureQuery
{
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public Guid? TeamId { get; init; }
}
