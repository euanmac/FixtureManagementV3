using System;
using System.ComponentModel.DataAnnotations;
using CsvHelper.Configuration.Attributes;
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
        public required Person Person { get; set; }
        [Ignore]
        public required Team Team { get; set; }

    }

    public enum ContactType
    {
        Manager = 0b_0000_0000,  // 0
        Coach = 0b_0000_0001,  // 1
        Admin = 0b_0000_0010,  // 2
        Other = 0b_0000_0011,  // 3
    }
}
