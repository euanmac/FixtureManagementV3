using System.ComponentModel.DataAnnotations;
using FixtureManagement.Shared.Models;
using FixtureManagement.Shared.Validation;

namespace FixtureManagement.Shared.Dtos;

/// <summary>
/// A pitch booking (training, events): returned by IBookingService / GET /api/bookings and sent back to save one.
/// Mutable so EditForm can bind to it. On writes Id comes from the URL and PitchName is ignored.
/// </summary>
public class BookingDto : IValidatableObject
{
    public Guid Id { get; set; }
    [Required]
    public string Title { get; set; } = "";
    [Required]
    public DateTime Start { get; set; }
    [Required]
    public DateTime End { get; set; }
    [Display(Name = "Pitch")]
    [NotEmptyGuid]
    public Guid PitchId { get; set; }
    /// <summary>Repeats weekly on Start's weekday, between Start and End.</summary>
    [Display(Name = "Recurring")]
    public bool IsRecurring { get; set; }

    /// <summary>Output only. Empty if the pitch has been removed.</summary>
    public string PitchName { get; set; } = "";

    public bool IsPast => FixtureRules.IsBookingPast(End);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (End <= Start)
            yield return new ValidationResult("End must be after Start.", [nameof(End)]);
    }
}
