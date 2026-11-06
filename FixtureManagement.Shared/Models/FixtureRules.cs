namespace FixtureManagement.Shared.Models;

/// <summary>
/// Fixture, allocation and booking rules, shared by the server's entities and the DTOs
/// so the website and the desktop client always agree.
/// </summary>
public static class FixtureRules
{
    /// <summary>Only home fixtures that are still going ahead need a pitch.</summary>
    public static bool CanAllocate(bool isHome, FixtureType fixtureType) =>
        isHome && fixtureType != FixtureType.Postponed && fixtureType != FixtureType.Cancelled;

    /// <summary>
    /// A fixture counts as allocated when it has a pitch and a kick-off time.
    /// A start of exactly 00:00 is treated as "no time set" (kept from the original entity).
    /// </summary>
    public static bool IsAllocated(bool hasAllocation, Guid pitchId, TimeOnly start) =>
        hasAllocation && pitchId != Guid.Empty && (start.Minute != 0 || start.Hour != 0);

    public static TimeOnly AllocationEnd(TimeOnly start, TimeSpan duration) => start.Add(duration);

    public static bool IsAllocationComplete(Guid pitchId, TimeSpan duration) =>
        pitchId != Guid.Empty && duration != TimeSpan.Zero;

    /// <summary>A booking is past once its end date is before today.</summary>
    public static bool IsBookingPast(DateTime end, DateTime? now = null) =>
        end < (now ?? DateTime.Now).Date;
}

public static class FixtureTypeExtensions
{
    /// <summary>One- or two-letter code shown in the fixture grids.</summary>
    public static string FixtureTypeShortName(this FixtureType fixtureType) => fixtureType switch
    {
        FixtureType.Friendly => "F",
        FixtureType.League => "L",
        FixtureType.Cup => "C",
        FixtureType.Postponed => "P",
        FixtureType.Cancelled => "X",
        FixtureType.CountyCup => "CC",
        _ => "O"
    };

}
