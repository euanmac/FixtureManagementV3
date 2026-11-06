using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace FixtureManagement.Shared;

public static class EnumExtensions
{
    /// <summary>The enum value's [Display(Name = ...)], or its name if it has none.</summary>
    public static string DisplayName(this Enum value) =>
        value.GetType().GetMember(value.ToString()).FirstOrDefault()
            ?.GetCustomAttribute<DisplayAttribute>()?.Name
        ?? value.ToString();
}
