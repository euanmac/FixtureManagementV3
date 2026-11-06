using System.ComponentModel.DataAnnotations;
using FixtureManagement.Shared.Models;
using FixtureManagement.Shared.Validation;

namespace FixtureManagement.Shared.Dtos;

/// <summary>
/// A fixture: returned by IFixtureService / GET /api/fixtures and sent back to create or update one.
/// Mutable so EditForm can bind to it. On writes Id comes from the URL, and Team and Allocation are
/// ignored (set the pitch via PUT /api/fixtures/{id}/allocation).
/// </summary>
public class FixtureDto
{
    public Guid Id { get; set; }
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

    /// <summary>Output only.</summary>
    public TeamSummaryDto Team { get; set; } = new();
    /// <summary>Output only. Null until a pitch and time have been set.</summary>
    public FixtureAllocationDto? Allocation { get; set; }

    public string FixtureTypeShortName => FixtureType.FixtureTypeShortName();
    public bool CanAllocate => FixtureRules.CanAllocate(IsHome, FixtureType);
    public bool IsAllocated => Allocation is not null
        && FixtureRules.IsAllocated(true, Allocation.PitchId, Allocation.Start);
}

/// <summary>
/// When and where a home fixture is played: returned inside FixtureDto and by
/// PUT /api/fixtures/{id}/allocation, and sent to that endpoint to set it. On writes Id and PitchName are ignored.
/// </summary>
public class FixtureAllocationDto : IValidatableObject
{
    public Guid Id { get; set; }
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

    /// <summary>Output only. Empty if no pitch is set.</summary>
    public string PitchName { get; set; } = "";

    [DisplayFormat(DataFormatString = "{0:HH\\:mm}")]
    public TimeOnly End => FixtureRules.AllocationEnd(Start, Duration);
    public bool IsComplete => FixtureRules.IsAllocationComplete(PitchId, Duration);

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
