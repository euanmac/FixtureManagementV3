
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FixtureManagementV3.Models
{
    public class Event
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = "";
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public Guid ResourceId { get; set; }
        [JsonIgnore]
        public (int R, int G, int B) RGBColor {get; set;}
        public bool StartEditable { get; set; }
        public bool DurationEditable { get; set; }
        public bool ResourceEditable { get; set; }
        private bool isConfirmed = false;
        private string eventURL = "";

        public string Url
        {
            get
            {
                return eventURL;
            }
        }        

        public string BorderColor
        {
            get {
                return $"rgba({RGBColor.R}, {RGBColor.G}, {RGBColor.B}, 1)";
            }
        }
        public string BackgroundColor
        {
            get
            {
                return $"rgba({RGBColor.R}, {RGBColor.G}, {RGBColor.B}, {(isConfirmed ? 100 : 100)})";
            }
        }

        public Event() { }

        public Event(Fixture fixture, bool Editable)
        {
            this.Id = fixture.Id;
            this.Title = $"{fixture.Team!.DisplayName} v {fixture.Opponent}";
            this.Start = fixture.Date.ToDateTime(fixture.FixtureAllocation!.Start);
            this.End = fixture.Date.ToDateTime(fixture.FixtureAllocation.End);
            this.ResourceId = fixture.FixtureAllocation!.PitchId;
            this.RGBColor = fixture.Team.ColourRGB;
            this.isConfirmed = fixture.FixtureAllocation.IsConfirmed;
            this.StartEditable = Editable;
            this.DurationEditable = false;
            this.ResourceEditable = Editable;
            this.eventURL = $"fixtures/allocate?id={fixture.Id}";
        }

        public Event(Fixture fixture) : this (fixture, false)
        {

        }

        public Event(Booking booking)
        {
            this.Id = booking.Id;
            this.Title = booking.Title;
            this.Start = booking.Start;
            this.End = booking.End;
            this.ResourceId = booking.PitchId;
            this.isConfirmed = true;
            this.RGBColor = (0, 0, 0);
            this.StartEditable = false; 
            this.DurationEditable = false;
            this.ResourceEditable = false;
            this.eventURL = "";
        }
    }

    public class RecurringEvent : Event
    {
        public RecurringEvent(Fixture fixture) : base(fixture)
        {
            this.StartEditable = false;
            this.StartEditable = false;
            this.StartEditable = false;
        }

        public RecurringEvent(Booking booking) : base(booking)
        {
        }

        public RecurringEvent() : base()
        {
        }

        public List<int> DaysOfWeek
        {
            get
            {
                return new List<int> { (int)Start.DayOfWeek};
            }
        }

        public string StartTime
        {
            get
            {
                return (DaysOfWeek.Count > 0) ? Start.ToString("HH:mm") : "";
            }
        }

        public string EndTime
        {
            get
            {
                return (DaysOfWeek.Count > 0) ? End.ToString("HH:mm") : "";
            }
        }



        public DateTime StartRecur
        {
            get
            {
                return Start;
            }
        }

        public DateTime EndRecur
        {
            get
            {
                return End;
            }
        }

        public static List<RecurringEvent> Recurring
        {
            get
            {
                return new List<RecurringEvent>
                    {
                        new RecurringEvent { Id = Guid.NewGuid(), Title= "U7, U10, U12 Training",Start=DateTime.Parse("2021-08-28T08:45"), End = DateTime.Parse("2022-05-30T10:15"), RGBColor=(0, 0, 0), ResourceId=Guid.Parse("9E365BFE-DDC2-4296-83DF-B278A846207F") , DaysOfWeek = {6 } },
                        new RecurringEvent { Id = Guid.NewGuid(), Title= "U8, U9 Training",Start=DateTime.Parse("2021-08-28T10:30"), End = DateTime.Parse("2022-05-30T11:45"), RGBColor=(0, 0, 0), ResourceId=Guid.Parse("9E365BFE-DDC2-4296-83DF-B278A846207F") , DaysOfWeek = {6 } },
                        new RecurringEvent { Id = Guid.NewGuid(), Title= "U8, U9 Training",Start=DateTime.Parse("2021-08-28T10:30"), End = DateTime.Parse("2022-05-30T11:45"), RGBColor=(0, 0, 0), ResourceId=Guid.Parse("FF4B08C8-DC9A-4F4A-9EC1-BADA343145B9") , DaysOfWeek = {6 } }

                    };

            }
        }

        public static List<Event> Booking
        {
            get
            {
                return new List<Event>
                    {
                        new Event { Id = Guid.NewGuid(), Title= "Over 60s",Start=DateTime.Parse("2021-11-21T14:00"), End = DateTime.Parse("2021-11-21T16:00"), RGBColor=(0, 0, 0), ResourceId=Guid.Parse("9E365BFE-DDC2-4296-83DF-B278A846207F"), DurationEditable=false, ResourceEditable=false, StartEditable=false }
                    };

            }
        }
    }
}