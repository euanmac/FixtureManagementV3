using System;
using System.ComponentModel.DataAnnotations;
using CsvHelper.Configuration.Attributes;
namespace FixtureManagement.Server.Models
{

    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    sealed public class CustomAttributeNoGuidEmpty : ValidationAttribute
    {
        
        public override bool IsValid(Object? value)
        {
            bool result = true;

            if ((Guid)value! == Guid.Empty)
                result = false;

            return result;
        }
    }

    public class Fixture
    {

        [Key]
        public Guid Id { get; set; }
        // [Required (ErrorMessage = "Team is required")]
        // [CustomAttributeNoGuidEmpty(ErrorMessage = "Team is required")]
        public Guid TeamId { get; set; }
        [DataType(DataType.Date)]
        public DateOnly Date { get; set; }
        [Display(Name = "Home")]
        public bool IsHome { get; set; }
        [Required]
        public string Opponent { get; set; } = "";
        [Display(Name = "Type")]
        public FixtureType FixtureType { get; set; }

        public bool CanAllocate => FixtureRules.CanAllocate(IsHome, FixtureType);

        public bool IsAllocated => FixtureAllocation is not null
            && FixtureRules.IsAllocated(true, FixtureAllocation.PitchId, FixtureAllocation.Start);

        //Navigation properties
        [Ignore]
        // [Required (ErrorMessage = "Team is required")]
        public Team? Team{ get; set; }
        [Ignore]
        public FixtureAllocation? FixtureAllocation { get; set; }
    }


}


