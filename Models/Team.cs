using System;
using System.Globalization;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using HtmlAgilityPack;
using System.Linq;

namespace FixtureManagementV3.Models
{


    public class Team
    {
        public Guid Id { get; set; }
        [Required]
        public string Name { get; set; } = "";
        [Display(Name = "Age Group")]
        [Required]
        public AgeGroup AgeGroup { get; set; }
        public Gender Gender { get; set; }
        public League League { get; set; }
        public Division Division { get; set; }
        [Display(Name = "FullTime Team Id")]
        public String? FullTimeTeamId { get; set; } = "";
        [Display(Name = "FullTime League Id")]
        public String? FullTimeLeagueId { get; set; } = "";
        [Display(Name = "Club Ref Required")]
        public bool RefRequired { get; set; }

        public IList<DownloadFixture> DownloadFixture { get; set; } = [];

        public void RollAgeGroup()
        {
                AgeGroup++;
        }

        [Display(Name = "Age Group")]
        public string GroupDescription
        {
            get
            { 
                switch (AgeGroup)
                {
                    case AgeGroup.Adult:
                        return (Gender == Gender.Mixed ? "" : (Gender == Gender.Male ? "Men" : "Women"));
                    case AgeGroup.U18:
                    case AgeGroup.External:
                    case AgeGroup.O50:

                        return $"{AgeGroup} " + (Gender == Gender.Mixed ? "" : (Gender == Gender.Male ? "Men" : "Women"));
                    default:
                        return $"{AgeGroup} " + (Gender == Gender.Mixed ? "" : (Gender == Gender.Male ? "Boys" : "Girls"));
                }
            }
        }

        public bool IsOnFullTime
        {
            get
            {
                return ! ((FullTimeLeagueId is null || FullTimeTeamId is null)
                || (FullTimeTeamId.Length == 0 && FullTimeTeamId.Length == 0));
            }
        }

        [Display(Name = "Team Name")]
        public string DisplayName
        {
            get
            {
                return $"{GroupDescription} - {Name}";
            }
        }

        [Display(Name = "Team Name")]
        public string DisplayShortName
        {
            get
            {
                if (Name != null)
                {
                    string shortName = Name.Replace("Thame United", "");
                    return $"{GroupDescription} - {shortName}";
                }
                else
                {
                    return "";
                }
            }
        }


        [Display(Name = "Team Size")]
        public TeamSize TeamSize{
            get {
                switch (AgeGroup)
                {
                    case AgeGroup.U6:
                    case AgeGroup.U7:
                    case AgeGroup.U8:
                        return TeamSize._5v5;
                    case AgeGroup.U9:
                    case AgeGroup.U10:
                        return TeamSize._7v7;
                    case AgeGroup.U11:
                    case AgeGroup.U12:
                        return TeamSize._9v9;
                    default:
                        return TeamSize._11v11;
                }
           }
        }

        public (int R, int G, int B) ColourRGB
        {
            get
            {
                return League switch
                {
                    League.BucksGirls => (220, 20, 60),
                    League.HighWycSunComb => (105, 105, 105),
                    League.OxGirls => (0, 128, 128),
                    League.OxMailYouth => (255,215,0),                   
                    League.OxOver50 => (192, 192, 192),
                    League.SouthBucksMini => (255, 69, 0),
                    League.TVCWFL => (60, 179, 113),
                    League.WycAndSouthBucksMinor => (70, 130, 180),
                    League.JPL => (25, 25, 112),
                    League.UHL => (105, 105, 105),
                    League.ADL => (105, 105, 105),
                    _ => (70, 130, 180)
                };
            }
        }

        public string Colour
        {
            get
            {
                return League switch
                {
                    League.BucksGirls => "Crimson",
                    League.HighWycSunComb => "DimGray",
                    League.OxGirls => "Teal",
                    League.OxMailYouth => "Gold",
                    League.OxOver50 => "Silver",
                    League.SouthBucksMini => "OrangeRed",
                    League.TVCWFL => "MediumSeaGreen",
                    League.WycAndSouthBucksMinor => "SteelBlue",
                    League.JPL => "MidnightBlue",
                    League.UHL => "DimGray",
                    League.ADL => "DimGray",
                    _ => "SteelBlue"
                };
            }
        }


        public DayOfWeek MatchDay
        {
            get
            {
                return League switch
                {
                    League.BucksGirls => DayOfWeek.Saturday,
                    League.HighWycSunComb => DayOfWeek.Sunday,
                    League.OxGirls => DayOfWeek.Saturday,
                    League.OxMailYouth => DayOfWeek.Sunday,
                    League.OxOver50 => DayOfWeek.Sunday,
                    League.SouthBucksMini => DayOfWeek.Sunday,
                    League.TVCWFL => DayOfWeek.Sunday,
                    League.WycAndSouthBucksMinor => DayOfWeek.Sunday,
                    League.JPL => DayOfWeek.Saturday,
                    League.UHL => DayOfWeek.Saturday,
                    League.ADL => DayOfWeek.Saturday,
                    _ => DayOfWeek.Sunday
                };
            }
        }

        public string FixtureURL
        {
            get
            {
                if (String.IsNullOrEmpty(FullTimeLeagueId) || String.IsNullOrEmpty(FullTimeTeamId))
                {
                    return "";
                }
                else
                {
                    return $"https://fulltime.thefa.com/displayTeam.html?divisionseason={FullTimeLeagueId}&teamID={FullTimeTeamId}";
                }

            }
        }

        //Navigation
        // public List<Fixture> Fixtures { get; set; } = [];
        public List<TeamContact> Contacts { get; set; } = [];

        public async Task<List<TeamReconiliationRow>> ReconcileAsync(DateOnly start, DateOnly end, IList<Fixture> fixtures)
        {

            //Download fixtures if team is on fulltime, filter so only within start and end date
            List<DownloadFixture> downloadFixtures = !this.IsOnFullTime ? new List<DownloadFixture>() :
                (await FixtureDownloader.FromFullTimeAsync(this))
                .Where(f => (f.Date >= start && f.Date <= end))
                .ToList();

            //Get list of DB fixtures for date range
            List<Fixture> filteredFixtures = fixtures
                .Where(f => (f.Date >= start && f.Date <= end && f.TeamId == this.Id))
                .ToList();

 
            //If no local or fulltime fixtures then return rec row
            //Check for rows, if none, create empty row for team to represent no fixture
            if (downloadFixtures.Count == 0 && filteredFixtures.Count == 0)
            {
                int offset = this.MatchDay == DayOfWeek.Sunday ? 6 : (int)this.MatchDay - 1;
                return new List<TeamReconiliationRow> { new TeamReconiliationRow { Team = this, MatchDate = start.AddDays(offset), RecStatus = FixtureRecMatchType.noFixture } };
            }

            //Find fixtures for dates
            //If fixtures exists then try to match
            IList<TeamReconiliationRow> recRows = filteredFixtures.Select(f =>
                {
                    //Default row for local fixture
                    TeamReconiliationRow recRow = new TeamReconiliationRow {
                        Team = this,
                        Id = f.Id,
                        MatchDate = f.Date,
                        Opponent = f.Opponent,
                        Venue = (f.IsHome ? "H" : "A"),
                        FixtureType = f.FixtureType,
                        CanAllocate = f.CanAllocate,
                        IsAllocated = f.IsAllocated,
                        AllocationId = f.IsAllocated ? f.FixtureAllocation!.Id : Guid.Empty,
                        Pitch = f.IsAllocated ? f.FixtureAllocation!.Pitch!.Name : "",
                        Start = f.IsAllocated ? f.FixtureAllocation!.Start : TimeOnly.MinValue,
                        IsConfirmed = f.IsAllocated ? f.FixtureAllocation!.IsConfirmed : false

                    };

                    //Check for downloaded fixture that matches
                    DownloadFixture? downloaded = downloadFixtures.FirstOrDefault(df =>
                        (f.Date) == df.Date &&
                        f.Opponent == df.Opponent &&
                        f.IsHome == df.IsHome &&
                        f.FixtureType == df.FixtureType
                        );

                    //Found download so check whether it matches
                    if (downloaded != null)
                    {
                        recRow.RecStatus = FixtureRecMatchType.fullTimematched;
                        //Need to remove downloaded from list!
                        downloadFixtures.Remove(downloaded);   
                    }
                    //No match so just set whether there should be a full time match or not if team not set up 
                    else
                    {
                        recRow.RecStatus = this.IsOnFullTime ? FixtureRecMatchType.localFixtureUnmatched : FixtureRecMatchType.localFixtureOnly;
                    }
                    return recRow;
                 }).ToList();

            //Now need to find any downloaded fixtures that dont have a matching local fixture - any matching should already have been removed above
            IList<TeamReconiliationRow> downloadRecs = downloadFixtures
                .Select(df =>
                {
                    return new TeamReconiliationRow
                    {
                        Id = df.Id,
                        Team = this,
                        MatchDate = df.Date,
                        Opponent = df.Opponent,
                        Venue = (df.IsHome ? "H" : "A"),
                        RecStatus = FixtureRecMatchType.fullTimeUnmatched,
                        FixtureType = df.FixtureType

                    };
                }).ToList();

            return recRows.Concat(downloadRecs).ToList();

        }

     }


    public enum AgeGroup
    {
        U6, U7, U8, U9, U10, U11, U12, U13, U14, U15, U16, U17, U18, O50, Adult, External
    }

    public enum League
    {
        [Display(Name = "South Bucks Mini Soccer Conference")]
        SouthBucksMini,
        [Display(Name = "Bucks Girls Football League")]
        BucksGirls,
        [Display(Name = "Wycombe and South Bucks Minor League")]
        WycAndSouthBucksMinor,
        [Display(Name = "Oxford Mail Youth Football League")]
        OxMailYouth,
        [Display(Name = "Oxfordshire Over 50s League")]
        OxOver50,
        [Display(Name = "High Wycombe Sunday Football Combination")]
        HighWycSunComb,
        [Display(Name = "Thames Valley Counties Women's Football League")]
        TVCWFL,
        [Display(Name = "Oxfordshire Girls Football League")]
        OxGirls,
        [Display(Name = "Junior Premier League")]
        JPL,
        [Display(Name = "Uhlsport Hellenic League")]
        UHL,
        [Display(Name = "Aylesbury and District Football League")]
        ADL,
        [Display(Name = "Spartan South Midlands")]
        SSM,
        [Display(Name = "Allied Counties League")]
        AlliedCounties
    }

    public enum TeamSize
    {
        [Display(Name = "5 v 5")]
        _5v5,
        [Display(Name = "7 v 7")]
        _7v7,
        [Display(Name = "9 v 9")]
        _9v9,
        [Display(Name = "11 v 11")]
        _11v11
    }

    public enum Gender
    {
       Male, Female, Mixed
    }
    public enum Division
    {
        One=1, Two=2, Three=3, Four=4, Five=5, Six=6, Seven=7, Eight =8, Nine=9, Ten=10, Red=20, White=21, Blue=22, Black=23, Green=24, Yellow=25, West = 70, South = 71, North = 72, East = 73, Championship = 80, Premier = 81, Other =100
    }


   
}

