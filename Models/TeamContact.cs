using System;
using System.ComponentModel.DataAnnotations;
using CsvHelper.Configuration.Attributes;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using FixtureManagement.Shared.Models;
namespace FixtureManagementV3.Models
{
    public class TeamContact
    {
        public Guid Id { get; set; }
        public Guid PersonId { get; set; }
        public Guid TeamId { get; set; }
        [Display(Name = "Contact Type")]
        public ContactType ContactType {get; set;}

        //Navigation
        [Ignore]
        public Person? Person { get; set; }
        [Ignore]
        public Team? Team { get; set; }

    }
}
