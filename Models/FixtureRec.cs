using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
namespace FixtureManagementV3.Models
{
    public class TeamReconiliationRow
    {
        public required Guid TeamId { get; set; }
        public Guid Id { get; set; }
        public FixtureRecMatchType RecStatus { get; set; }
        [DataType(DataType.Date)]
        public DateOnly MatchDate { get; set; }
        public bool IsAllocated { get; set; }
        public Guid AllocationId { get; set; }
        public bool CanAllocate { get; set; }
        public bool IsConfirmed { get; set; }
        public string Venue { get; set; } = "";
        public string Opponent { get; set; } = "";
        public  string Pitch { get; set; } = "";
        [DataType(DataType.Time)]
        public TimeOnly Start { get; set; }
        public string FullTimeURL { get; set; } = "";
        public FixtureType FixtureType { get; set; }

        public string GetRecIcon()
        {
            bool isHome = (Venue == "H");
            return RecStatus switch
            {
                FixtureRecMatchType.noFixture => "bi bi-x-circle text-danger",
                FixtureRecMatchType.pending => "spinner-border spinner-border-sm text-dark",
                FixtureRecMatchType.localFixtureOnly when (!IsAllocated && CanAllocate)  => "bi bi-journal-check text-warning",
                FixtureRecMatchType.localFixtureOnly => "bi bi-journal-check text-success",
                FixtureRecMatchType.localFixtureUnmatched => "bi bi-journal-x text-danger",
                FixtureRecMatchType.fullTimeUnmatched => "bi bi-cloud-slash text-danger",
                FixtureRecMatchType.fullTimematched when (!IsAllocated && CanAllocate) => "bi bi-cloud-check text-warning",
                FixtureRecMatchType.fullTimematched => "bi bi-cloud-check text-success",

                _ => "bi bi-question-circle-fill text-danger    "
            };

        }
    }

    public enum FixtureRecMatchType
    {
        unknownTeam,
        [Display(Name = "No Fixture")]
        noFixture,
        [Display(Name = "Fixture ok, matches not FullTime")]
        localFixtureOnly,
        [Display(Name = "Fixture not matched on FullTime")]
        localFixtureUnmatched,
        [Display(Name = "Only on FullTime, needs to be downloaded")]
        fullTimeUnmatched,
        [Display(Name = "Matched on FullTime")]
        fullTimematched,
        pending
    }
}
