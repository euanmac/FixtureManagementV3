using System;
using System.ComponentModel.DataAnnotations;
using CsvHelper.Configuration.Attributes;
namespace FixtureManagementV3.Models
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

        public bool CanAllocate
        {
            get
            {
                return IsHome && (FixtureType != FixtureType.Postponed && FixtureType != FixtureType.Cancelled);
            }
        }

        public bool IsAllocated
        {
            get
            {
                return (FixtureAllocation != null 
                    && FixtureAllocation.PitchId != Guid.Empty
                    && (FixtureAllocation.Start.Minute != 0
                    || FixtureAllocation.Start.Hour != 0));
            }
        }

        //Navigation properties
        [Ignore]
        // [Required (ErrorMessage = "Team is required")]
        public Team? Team{ get; set; }
        [Ignore]
        public FixtureAllocation? FixtureAllocation { get; set; }
    }


    public enum FixtureType
    {
        Friendly =0,
        League =1,
        Cup =2,
        [Display(Name = "County Cup")]
        CountyCup =6,
        Cancelled =3,
        Postponed =4,
        Other =5
    }

    // Define an extension method in a non-nested static class.
    public static class Extensions
    {
        public static string FixtureTypeShortName(this FixtureType fixtureType)
        {
            return fixtureType switch
            {
                FixtureType.Friendly => "F",
                FixtureType.League => "L",
                FixtureType.Cup => "C",
                FixtureType.Postponed => "P",
                FixtureType.Cancelled => "X",
                FixtureType.CountyCup => "CC",
                _ => "O"
            };
        }

        public static FixtureType FromFullTimeFixtureType(this League league, string FTType)
        {
            return FTType switch
            {
                "F" => FixtureType.Friendly,
                "Cup" => FixtureType.Cup,
                "C" => FixtureType.CountyCup,
                "L" => FixtureType.League,
                "ONE" => FixtureType.League,
                "TWO" => FixtureType.League,
                "HL2N" => FixtureType.League,
                _ => FixtureType.Other
            };

        }
    }



}


