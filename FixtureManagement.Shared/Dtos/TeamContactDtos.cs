using System.ComponentModel.DataAnnotations;
using FixtureManagement.Shared.Models;
using FixtureManagement.Shared.Validation;

namespace FixtureManagement.Shared.Dtos;

/// <summary>
/// A team contact with its team, as returned by ITeamContactService / GET /api/teamcontacts.
/// Same fields as TeamContactSummaryDto (used inside TeamDto) plus the team.
/// </summary>
public record TeamContactDto : TeamContactSummaryDto
{
    public TeamSummaryDto Team { get; init; } = new();
}

/// <summary>Links a person to a team. Contacts can't be edited, only added and removed.</summary>
public class TeamContactCreateDto
{
    [Display(Name = "Team")]
    [NotEmptyGuid]
    public Guid TeamId { get; set; }
    [Display(Name = "Person")]
    [NotEmptyGuid]
    public Guid PersonId { get; set; }
    [Display(Name = "Contact Type")]
    public ContactType ContactType { get; set; } = ContactType.Manager;
}
