using System.ComponentModel.DataAnnotations;

namespace FixtureManagement.Shared.Validation;

/// <summary>
/// Fails when a Guid is Guid.Empty. [Required] can't catch that, because a Guid is never null.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class NotEmptyGuidAttribute : ValidationAttribute
{
    public NotEmptyGuidAttribute() : base("The {0} field is required.") { }

    public override bool IsValid(object? value) => value is not Guid guid || guid != Guid.Empty;
}
