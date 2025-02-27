using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FixtureManagementV3.Models
{
    public class Booking
    {
        public Guid Id { get; set; }
        [Required]
        public string Title { get; set;} = "";
        [Required]
        public DateTime Start { get; set; }
        [Required]
        public DateTime End { get; set; }
        public Guid PitchId { get; set; }
        [Display(Name = "Recurring")]
        public bool IsRecurring { get; set; }
        public bool IsPast
        {
            get
            {
                return End < DateTime.Now.Date;
            }
        }

        //Navigation
        public Pitch? Pitch { get; set; }

        public static List<Booking> Bookings
        {
            get
            {
                return new List<Booking>
                    {
                        new Booking { Id = Guid.NewGuid(), Title= "U7, U10, U12 Training",Start=DateTime.Parse("2021-08-28T08:45"), End = DateTime.Parse("2022-05-30T10:15"), PitchId=Guid.Parse("9E365BFE-DDC2-4296-83DF-B278A846207F"), IsRecurring=true },
                        new Booking { Id = Guid.NewGuid(), Title = "U8, U9 Training", Start = DateTime.Parse("2021-08-28T10:30"), End = DateTime.Parse("2022-05-30T11:45"), PitchId = Guid.Parse("9E365BFE-DDC2-4296-83DF-B278A846207F"), IsRecurring=true },
                        new Booking { Id = Guid.NewGuid(), Title = "U8, U9 Training", Start = DateTime.Parse("2021-08-28T10:30"), End = DateTime.Parse("2022-05-30T11:45"), PitchId = Guid.Parse("FF4B08C8-DC9A-4F4A-9EC1-BADA343145B9"), IsRecurring=true },
                        new Booking { Id = Guid.NewGuid(), Title= "Over 60s",Start=DateTime.Parse("2021-11-21T14:00"), End = DateTime.Parse("2021-11-21T16:00"), PitchId=Guid.Parse("9E365BFE-DDC2-4296-83DF-B278A846207F"), IsRecurring=false }
                    };

            }
        }
    }

}
