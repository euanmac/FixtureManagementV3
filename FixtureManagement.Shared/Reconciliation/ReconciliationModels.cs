using System.ComponentModel.DataAnnotations;
using FixtureManagement.Shared.Dtos;
using FixtureManagement.Shared.Models;

namespace FixtureManagement.Shared.Reconciliation;

/// <summary>
/// A fixture as a source (FullTime, a CSV upload, a league grid …) lists it, already linked to one of our teams.
/// Fixtures are seen from our team's side: IsHome means our team is at home.
/// </summary>
public record SourceFixture
{
    /// <summary>Our team, or Guid.Empty if the source names a club team the app has no team for.</summary>
    public Guid TeamId { get; init; }
    /// <summary>Our team's name as the source gives it (shown when it isn't linked to a team).</summary>
    public string SourceTeamName { get; init; } = "";
    /// <summary>Null when the source says the date is still to be confirmed (TBC).</summary>
    public DateOnly? Date { get; init; }
    public bool IsHome { get; init; }
    public string Opponent { get; init; } = "";
    public FixtureType FixtureType { get; init; } = FixtureType.League;
    /// <summary>Division or competition name, e.g. "Under 13 Division 2".</summary>
    public string Competition { get; init; } = "";
    /// <summary>The source's own id for the fixture (e.g. FullTime's fixture id), if it has one.</summary>
    public string? SourceFixtureId { get; init; }
    /// <summary>The source's status note, e.g. "Postponed". Empty if none.</summary>
    public string Status { get; init; } = "";
    /// <summary>The source's own code for the fixture's type (e.g. FullTime's "L" or "50D1"). Not saved; used to report codes we don't know.</summary>
    public string TypeCode { get; init; } = "";
}

/// <summary>How an app fixture and a source fixture compare.</summary>
public enum DifferenceKind
{
    /// <summary>Both agree.</summary>
    Same,
    /// <summary>Same team and date, but the opponent, venue or type differs.</summary>
    Different,
    /// <summary>Same team, opponent, venue and type on a different date (the fixture was rearranged).</summary>
    [Display(Name = "Date moved")]
    DateMoved,
    /// <summary>In the source but not in the app.</summary>
    [Display(Name = "Missing from the app")]
    OnlyInSource,
    /// <summary>In the app but not in the source.</summary>
    [Display(Name = "Not in the source")]
    OnlyInApp,
    /// <summary>In the source for a club team that isn't linked to any of our teams.</summary>
    [Display(Name = "Not linked to a team")]
    NotLinked,
}

/// <summary>What differs between a paired app fixture and source fixture.</summary>
[Flags]
public enum FixtureChanges
{
    None = 0,
    Date = 1,
    Opponent = 2,
    Venue = 4,
    Type = 8,
}

/// <summary>Which side to keep for one difference.</summary>
public enum Resolution
{
    /// <summary>Nothing to decide (they agree, or there's nothing the app can do).</summary>
    None,
    /// <summary>Change the app to match the source (add, update or delete the app fixture).</summary>
    UseSource,
    /// <summary>Leave the app as it is.</summary>
    KeepApp,
}

/// <summary>One line of a reconciliation: an app fixture, a source fixture, or a pair of them.</summary>
public sealed record ReconciliationItem
{
    public DifferenceKind Kind { get; init; }
    /// <summary>Our team. Null only for NotLinked items.</summary>
    public TeamDto? Team { get; init; }
    public FixtureDto? App { get; init; }
    public SourceFixture? Source { get; init; }
    public FixtureChanges Changes { get; init; }
    /// <summary>What the reconciler suggests; the user can override it.</summary>
    public Resolution Suggested { get; init; }

    /// <summary>
    /// Identifies this exact difference, to remember a "keep the app version" decision about it. It includes
    /// both sides' details, so if either side changes again the key changes and the difference is raised afresh.
    /// </summary>
    public string Key => string.Join('|',
        Kind,
        Team?.Id.ToString() ?? "",
        App is null ? "" : $"{App.Id},{App.Date:yyyy-MM-dd},{(App.IsHome ? "H" : "A")},{App.FixtureType},{App.Opponent}",
        Source is null ? "" : $"{Source.SourceFixtureId},{Source.Date:yyyy-MM-dd},{(Source.IsHome ? "H" : "A")},{Source.FixtureType},{Source.Opponent}");

    /// <summary>
    /// A short id for this exact difference (a hash of Key), for forms and the apply request. Choices are matched
    /// to differences by it, so if the data changes between showing the page and applying, a stale choice simply
    /// matches nothing rather than being applied to a different fixture.
    /// </summary>
    public string Id => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(Key)))[..16].ToLowerInvariant();

    /// <summary>The date to sort and filter by: the source's date if it has one, else the app's.</summary>
    public DateOnly? Date => Source?.Date ?? App?.Date;

    /// <summary>The source moves the fixture earlier than the app has it – urgent.</summary>
    public bool BroughtForward => Kind == DifferenceKind.DateMoved
        && App is not null && Source?.Date is not null && Source.Date.Value < App.Date;

    /// <summary>
    /// Using the source would undo the app fixture's pitch allocation: it moves, is deleted, or stops being a home game.
    /// </summary>
    public bool AffectsAllocation => App is not null && App.IsAllocated
        && (Kind is DifferenceKind.DateMoved or DifferenceKind.OnlyInApp
            || Changes.HasFlag(FixtureChanges.Date) || Changes.HasFlag(FixtureChanges.Venue));
}

/// <summary>A team with more than one fixture on the same day, on one side.</summary>
public sealed record FixtureClash(Guid TeamId, DateOnly Date, bool InSource, int Count);

public sealed record ReconciliationResult(IReadOnlyList<ReconciliationItem> Items, IReadOnlyList<FixtureClash> Clashes)
{
    /// <summary>Everything that isn't Same.</summary>
    public IEnumerable<ReconciliationItem> Differences => Items.Where(i => i.Kind != DifferenceKind.Same);
}
