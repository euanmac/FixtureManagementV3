using System.ComponentModel.DataAnnotations;
using FixtureManagement.Shared.Models;

namespace FixtureManagement.Shared.Dtos;

/// <summary>A team as returned by ITeamService / GET /api/teams.</summary>
public record TeamDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = "";
    public AgeGroup AgeGroup { get; init; }
    public Gender Gender { get; init; }
    public League League { get; init; }
    public Division Division { get; init; }
    public string? FullTimeTeamId { get; init; }
    public string? FullTimeLeagueId { get; init; }
    public bool RefRequired { get; init; }
    public IReadOnlyList<TeamContactSummaryDto> Contacts { get; init; } = [];

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

/// <summary>A team contact as shown on the team list and details pages.</summary>
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

/// <summary>Fields a user can set when creating or editing a team. Mutable so EditForm can bind to it.</summary>
public class TeamEditDto
{
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
}
