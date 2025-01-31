using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;

namespace FixtureManagementV3.Models
{
    public class Person
    { 
        public Guid Id { get; set; }
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = "";
        [Display(Name = "Last Name")]
        public string LastName { get; set; } = "";
        [Display(Name = "Telephone")]
        public string Tel { get; set; } = "";
        public string Email { get; set; } = "";
        [Display(Name = "Is Referee")]
        public bool IsRef { get; set; }
        [Display(Name = "Full Name")]
        public string FullName
        {
            get
            {
               return $"{FirstName} {LastName}";
            }
        }
        //Navigation
        //No link to teams at the moment
        //No link to fixtures either
        //public List<TeamContact> ContactFor { get; set; } = new List<TeamContact>();
    }
    
}
