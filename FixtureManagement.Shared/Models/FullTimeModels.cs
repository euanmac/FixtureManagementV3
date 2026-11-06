using System.ComponentModel.DataAnnotations;
using FixtureManagement.Shared.Dtos;

namespace FixtureManagement.Shared.Models;

/// <summary>A fixture as listed on a team's FullTime page (not yet saved locally).</summary>
public record FullTimeFixture
{
    /// <summary>A new id per download, used to tell rows apart on the page (not a FullTime id).</summary>
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateOnly Date { get; init; }
    public bool IsHome { get; init; }
    public string Opponent { get; init; } = "";
    public FixtureType FixtureType { get; init; }
}

/// <summary>How a team's fixture for the week compares between the local fixtures and FullTime.</summary>
public enum FixtureRecMatchType
{
    /// <summary>FullTime couldn't be read for this team (e.g. the site was down or the page changed).</summary>
    [Display(Name = "Couldn't read FullTime")]
    unknownTeam,
    [Display(Name = "No Fixture")]
    noFixture,
    [Display(Name = "Fixture ok, matches not FullTime")]
    localFixtureOnly,
    [Display(Name = "Fixture not matched on FullTime")]
    localFixtureUnmatched,
    [Display(Name = "Only on FullTime, needs to be downloaded")]
    fullTimeUnmatched,
    [Display(Name = "Matched on FullTime")]
    fullTimematched,
    pending
}

/// <summary>One row of the Gameweek table: a local fixture, a FullTime-only fixture, or "no fixture" for a team.</summary>
public class TeamReconiliationRow
{
    public required TeamDto Team { get; set; }
    /// <summary>The local fixture id, or the FullTimeFixture id for a FullTime-only row.</summary>
    public Guid Id { get; set; }
    public FixtureRecMatchType RecStatus { get; set; }
    [DataType(DataType.Date)]
    public DateOnly MatchDate { get; set; }
    public bool IsAllocated { get; set; }
    public Guid AllocationId { get; set; }
    public bool CanAllocate { get; set; }
    public bool IsConfirmed { get; set; }
    public string Venue { get; set; } = "";
    public string Opponent { get; set; } = "";
    public string Pitch { get; set; } = "";
    [DataType(DataType.Time)]
    public TimeOnly Start { get; set; }
    public string FullTimeURL { get; set; } = "";
    public FixtureType FixtureType { get; set; }

    /// <summary>Bootstrap icon classes for the status column.</summary>
    public string GetRecIcon() => RecStatus switch
    {
        FixtureRecMatchType.noFixture => "bi bi-x-circle text-danger",
        FixtureRecMatchType.pending => "spinner-border spinner-border-sm text-dark",
        FixtureRecMatchType.localFixtureOnly when (!IsAllocated && CanAllocate) => "bi bi-journal-check text-warning",
        FixtureRecMatchType.localFixtureOnly => "bi bi-journal-check text-success",
        FixtureRecMatchType.localFixtureUnmatched => "bi bi-journal-x text-danger",
        FixtureRecMatchType.fullTimeUnmatched => "bi bi-cloud-slash text-danger",
        FixtureRecMatchType.fullTimematched when (!IsAllocated && CanAllocate) => "bi bi-cloud-check text-warning",
        FixtureRecMatchType.fullTimematched => "bi bi-cloud-check text-success",
        _ => "bi bi-question-circle-fill text-danger"
    };
}
