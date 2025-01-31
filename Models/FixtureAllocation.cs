using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using CsvHelper.Configuration.Attributes;

namespace FixtureManagementV3.Models
{
    public class FixtureAllocation
    {
        public Guid Id { get; set; }
        public Guid FixtureId { get; set; }
        
        [Required (ErrorMessage = "Pitch is required")]
        [CustomAttributeNoGuidEmpty(ErrorMessage = "Pitch is required")]
        public Guid PitchId { get; set; }
      
        [Required]
        public TimeOnly Start { get; set; }
        [Required]
        [DisplayFormat(DataFormatString = "{0:HH\\:mm}")]
        public TimeSpan Duration { get; set; }
        [Display(Name = "Confirmed")]
        [Required]
        public bool IsConfirmed {get; set;} = false;


        // [NotMapped]
        // public TimeOnly StartTimeOnly { get; set; }

        //Navigation Properties
        [Ignore]
        public Fixture? Fixture { get; set; }
        [Ignore]
        public Pitch? Pitch { get; set; }
        // public Person? Referee { get; set; }

        //Calculated Properties
        public bool IsComplete
        {
            get
            {
                return (PitchId != Guid.Empty && Duration != TimeSpan.Zero);
            }
        }

        [DisplayFormat(DataFormatString = "{0:HH\\:mm}")]
        public TimeOnly End
        {
            get
            {
                return Start.Add(Duration);
            }

        }
    }
}
