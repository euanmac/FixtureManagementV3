using System.ComponentModel.DataAnnotations;
using FixtureManagement.Shared.Models;

namespace FixtureManagement.Shared.Dtos;

/// <summary>
/// A person (contact or referee): returned by IPersonService / GET /api/people and sent back to save one.
/// Mutable so EditForm can bind to it. On writes Id comes from the URL and Teams is ignored
/// (manage those via ITeamContactService).
/// </summary>
public class PersonDto
{
    public Guid Id { get; set; }
    [Display(Name = "First Name")]
    [Required]
    public string FirstName { get; set; } = "";
    [Display(Name = "Last Name")]
    [Required]
    public string LastName { get; set; } = "";
    [Display(Name = "Telephone")]
    public string Tel { get; set; } = "";
    public string Email { get; set; } = "";
    [Display(Name = "Is Referee")]
    public bool IsRef { get; set; }

    /// <summary>Output only: the teams this person is a contact for, youngest age group first.</summary>
    public IReadOnlyList<PersonTeamDto> Teams { get; set; } = [];

    [Display(Name = "Full Name")]
    public string FullName => $"{FirstName} {LastName}";
}

/// <summary>One team a person is a contact for. Read-only.</summary>
public record PersonTeamDto
{
    public Guid TeamContactId { get; init; }
    public ContactType ContactType { get; init; }
    public TeamSummaryDto Team { get; init; } = new();
}
