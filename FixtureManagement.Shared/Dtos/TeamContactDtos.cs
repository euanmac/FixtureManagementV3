using System.ComponentModel.DataAnnotations;
using FixtureManagement.Shared.Models;
using FixtureManagement.Shared.Validation;

namespace FixtureManagement.Shared.Dtos;

/// <summary>
/// A link between a person and a team: returned by ITeamContactService / GET /api/teamcontacts,
/// and sent to POST to add one (only TeamId, PersonId and ContactType are used). Contacts can't be edited,
/// only added and removed.
/// </summary>
public class TeamContactDto
{
    public Guid Id { get; set; }
    [Display(Name = "Team")]
    [NotEmptyGuid]
    public Guid TeamId { get; set; }
    [Display(Name = "Person")]
    [NotEmptyGuid]
    public Guid PersonId { get; set; }
    [Display(Name = "Contact Type")]
    public ContactType ContactType { get; set; } = ContactType.Manager;

    // Output only.
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public TeamSummaryDto Team { get; set; } = new();

    public string FullName => $"{FirstName} {LastName}";
}
