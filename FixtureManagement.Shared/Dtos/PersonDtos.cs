using System.ComponentModel.DataAnnotations;
using FixtureManagement.Shared.Models;

namespace FixtureManagement.Shared.Dtos;

/// <summary>A person (contact or referee) as returned by IPersonService / GET /api/people.</summary>
public record PersonDto
{
    public Guid Id { get; init; }
    public string FirstName { get; init; } = "";
    public string LastName { get; init; } = "";
    public string Tel { get; init; } = "";
    public string Email { get; init; } = "";
    public bool IsRef { get; init; }
    /// <summary>The teams this person is a contact for, youngest age group first.</summary>
    public IReadOnlyList<PersonTeamDto> Teams { get; init; } = [];

    public string FullName => $"{FirstName} {LastName}";
}

/// <summary>One team a person is a contact for.</summary>
public record PersonTeamDto
{
    public Guid TeamContactId { get; init; }
    public ContactType ContactType { get; init; }
    public TeamSummaryDto Team { get; init; } = new();
}

/// <summary>Fields a user can set when creating or editing a person. Mutable so EditForm can bind to it.</summary>
public class PersonEditDto
{
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
}
