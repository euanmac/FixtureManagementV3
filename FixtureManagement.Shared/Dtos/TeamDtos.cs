using System.ComponentModel.DataAnnotations;
using FixtureManagement.Shared.Models;

namespace FixtureManagement.Shared.Dtos;

/// <summary>
/// A team: returned by ITeamService / GET /api/teams, and sent back to create or update one.
/// Mutable so EditForm can bind to it. On writes the server only uses the editable fields;
/// Id comes from the URL and Contacts is ignored (manage those via ITeamContactService).
/// </summary>
public class TeamDto
{
    public Guid Id { get; set; }
    [Required]
    public string Name { get; set; } = "";
    [Display(Name = "Age Group")]
    [Required]
    public AgeGroup AgeGroup { get; set; }
    public Gender Gender { get; set; }
    public League League { get; set; }
    public Division Division { get; set; }
    [Display(Name = "FullTime Team Id")]
    public string? FullTimeTeamId { get; set; } = "";
    [Display(Name = "FullTime League Id")]
    public string? FullTimeLeagueId { get; set; } = "";
    [Display(Name = "Club Ref Required")]
    public bool RefRequired { get; set; }

    /// <summary>Output only.</summary>
    public IReadOnlyList<TeamContactSummaryDto> Contacts { get; set; } = [];

    // Derived values (computed locally, so they're never out of step with the fields above).
    public string GroupDescription => TeamRules.GroupDescription(AgeGroup, Gender);
    public string DisplayName => TeamRules.DisplayName(Name, AgeGroup, Gender);
    public string DisplayShortName => TeamRules.DisplayShortName(Name, AgeGroup, Gender);
    public string LeagueName => League.DisplayName();
    public TeamSize TeamSize => TeamRules.TeamSize(AgeGroup);
    public string Colour => TeamRules.Colour(League);
    public DayOfWeek MatchDay => TeamRules.MatchDay(League);
    public string FixtureURL => TeamRules.FixtureURL(FullTimeLeagueId, FullTimeTeamId);
    public bool IsOnFullTime => TeamRules.IsOnFullTime(FullTimeLeagueId, FullTimeTeamId);
}

/// <summary>
/// The team details other areas show (fixtures, contacts, people), without the team's own contacts.
/// Read-only: it's always output, never edited.
/// </summary>
public record TeamSummaryDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = "";
    public AgeGroup AgeGroup { get; init; }
    public Gender Gender { get; init; }
    public League League { get; init; }
    public Division Division { get; init; }
    public bool RefRequired { get; init; }

    public string DisplayName => TeamRules.DisplayName(Name, AgeGroup, Gender);
    public string DisplayShortName => TeamRules.DisplayShortName(Name, AgeGroup, Gender);
    public string Colour => TeamRules.Colour(League);
    public DayOfWeek MatchDay => TeamRules.MatchDay(League);
}

/// <summary>A team contact as listed inside TeamDto. Read-only.</summary>
public record TeamContactSummaryDto
{
    public Guid Id { get; init; }
    public Guid PersonId { get; init; }
    public ContactType ContactType { get; init; }
    public string FirstName { get; init; } = "";
    public string LastName { get; init; } = "";
    public string Email { get; init; } = "";
    public string FullName => $"{FirstName} {LastName}";
}
