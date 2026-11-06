using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using FixtureManagement.Shared.Models;

namespace FixtureManagement.Server.Models
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

        // Display and league rules live in FixtureManagement.Shared so TeamDto uses the same logic.

        [Display(Name = "Age Group")]
        public string GroupDescription => TeamRules.GroupDescription(AgeGroup, Gender);

        public bool IsOnFullTime => TeamRules.IsOnFullTime(FullTimeLeagueId, FullTimeTeamId);

        [Display(Name = "Team Name")]
        public string DisplayName => TeamRules.DisplayName(Name, AgeGroup, Gender);

        [Display(Name = "Team Name")]
        public string DisplayShortName => TeamRules.DisplayShortName(Name, AgeGroup, Gender);

        [Display(Name = "Team Size")]
        public TeamSize TeamSize => TeamRules.TeamSize(AgeGroup);

        public (int R, int G, int B) ColourRGB => TeamRules.ColourRGB(League);

        public string Colour => TeamRules.Colour(League);

        public DayOfWeek MatchDay => TeamRules.MatchDay(League);

        public string FixtureURL => TeamRules.FixtureURL(FullTimeLeagueId, FullTimeTeamId);

        //Navigation
        // public List<Fixture> Fixtures { get; set; } = [];
        public List<TeamContact> Contacts { get; set; } = [];
    }
}
