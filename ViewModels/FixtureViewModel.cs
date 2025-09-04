   using FixtureManagementV3.Models;
    
   namespace FixtureManagementV3.ViewModels;

    public class FixtureViewModel 
    {
        public Guid Id {get;set;}
        public String Team {get;set;} = "";
        public Guid TeamId {get;set;}
        public String Opponent {get; set;} = "";
        public DateOnly Date {get; set;}
        public bool IsHome {get; set;}
        public bool IsConfirmed {get; set;}
        public String Pitch { get; set; } = "";
        public String Start {get; set;} = "";
        public String End {get; set;} = "";
        public string Type {get; set;} = "";
        public bool IsAllocated = false;

        public FixtureViewModel(Fixture fixture, bool IsAuthenticated) {
            this.Id = fixture.Id;
            this.Team = fixture.Team!.DisplayName;
            this.TeamId = fixture.TeamId;
            this.Opponent = fixture.Opponent;
            this.IsHome = fixture.IsHome;
            this.Type = fixture.FixtureType.FixtureTypeShortName();
            this.Date = fixture.Date;
            if (fixture.IsAllocated && (fixture.FixtureAllocation!.IsConfirmed || IsAuthenticated))
            {
                this.IsAllocated = true;
                this.Pitch = fixture.FixtureAllocation!.Pitch!.Name;
                this.Start = fixture.FixtureAllocation!.Start.ToString("hh:mm");
                this.End = fixture.FixtureAllocation!.End.ToString("hh:mm");
                this.IsConfirmed = fixture.FixtureAllocation!.IsConfirmed;    
            }
        }
        
    }