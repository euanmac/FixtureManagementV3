namespace FixtureManagement.Shared.Models;

/// <summary>
/// Team display and league rules, shared by the server's Team entity and TeamDto
/// so the website and the desktop client show the same values.
/// </summary>
public static class TeamRules
{
    public static string GroupDescription(AgeGroup ageGroup, Gender gender)
    {
        switch (ageGroup)
        {
            case AgeGroup.Adult:
                return gender == Gender.Mixed ? "" : (gender == Gender.Male ? "Men" : "Women");
            case AgeGroup.U18:
            case AgeGroup.External:
            case AgeGroup.O50:
                return $"{ageGroup} " + (gender == Gender.Mixed ? "" : (gender == Gender.Male ? "Men" : "Women"));
            default:
                return $"{ageGroup} " + (gender == Gender.Mixed ? "" : (gender == Gender.Male ? "Boys" : "Girls"));
        }
    }

    public static string DisplayName(string name, AgeGroup ageGroup, Gender gender) =>
        $"{GroupDescription(ageGroup, gender)} - {name}";

    public static string DisplayShortName(string? name, AgeGroup ageGroup, Gender gender) =>
        name is null ? "" : $"{GroupDescription(ageGroup, gender)} - {name.Replace("Thame United", "")}";

    public static bool IsOnFullTime(string? fullTimeLeagueId, string? fullTimeTeamId) =>
        // Kept identical to the original Team.IsOnFullTime (the second check tests the team id twice).
        !((fullTimeLeagueId is null || fullTimeTeamId is null)
          || (fullTimeTeamId.Length == 0 && fullTimeTeamId.Length == 0));

    public static TeamSize TeamSize(AgeGroup ageGroup) => ageGroup switch
    {
        AgeGroup.U6 or AgeGroup.U7 or AgeGroup.U8 => Models.TeamSize._5v5,
        AgeGroup.U9 or AgeGroup.U10 => Models.TeamSize._7v7,
        AgeGroup.U11 or AgeGroup.U12 => Models.TeamSize._9v9,
        _ => Models.TeamSize._11v11
    };

    public static (int R, int G, int B) ColourRGB(League league) => league switch
    {
        League.BucksGirls => (220, 20, 60),
        League.HighWycSunComb => (105, 105, 105),
        League.OxGirls => (0, 128, 128),
        League.OxMailYouth => (255, 215, 0),
        League.OxOver50 => (192, 192, 192),
        League.SouthBucksMini => (255, 69, 0),
        League.TVCWFL => (60, 179, 113),
        League.WycAndSouthBucksMinor => (70, 130, 180),
        League.JPL => (25, 25, 112),
        League.UHL => (105, 105, 105),
        League.ADL => (105, 105, 105),
        _ => (70, 130, 180)
    };

    public static string Colour(League league) => league switch
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

    public static DayOfWeek MatchDay(League league) => league switch
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

    public static string FixtureURL(string? fullTimeLeagueId, string? fullTimeTeamId) =>
        string.IsNullOrEmpty(fullTimeLeagueId) || string.IsNullOrEmpty(fullTimeTeamId)
            ? ""
            : $"https://fulltime.thefa.com/displayTeam.html?divisionseason={fullTimeLeagueId}&teamID={fullTimeTeamId}";
}
