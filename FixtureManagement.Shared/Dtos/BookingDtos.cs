using System.ComponentModel.DataAnnotations;
using FixtureManagement.Shared.Models;
using FixtureManagement.Shared.Validation;

namespace FixtureManagement.Shared.Dtos;

/// <summary>A pitch booking (training, events) as returned by IBookingService / GET /api/bookings.</summary>
public record BookingDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = "";
    public DateTime Start { get; init; }
    public DateTime End { get; init; }
    public Guid PitchId { get; init; }
    /// <summary>Empty if the pitch has been removed.</summary>
    public string PitchName { get; init; } = "";
    /// <summary>Repeats weekly on Start's weekday, between Start and End.</summary>
    public bool IsRecurring { get; init; }

    public bool IsPast => FixtureRules.IsBookingPast(End);
}

/// <summary>Fields a user can set when creating or editing a booking. Mutable so EditForm can bind to it.</summary>
public class BookingEditDto : IValidatableObject
{
    [Required]
    public string Title { get; set; } = "";
    [Required]
    public DateTime Start { get; set; }
    [Required]
    public DateTime End { get; set; }
    [Display(Name = "Pitch")]
    [NotEmptyGuid]
    public Guid PitchId { get; set; }
    [Display(Name = "Recurring")]
    public bool IsRecurring { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (End <= Start)
            yield return new ValidationResult("End must be after Start.", [nameof(End)]);
    }
}
