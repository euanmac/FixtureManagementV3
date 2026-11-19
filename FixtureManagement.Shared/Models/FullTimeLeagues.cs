namespace FixtureManagement.Shared.Models;

/// <summary>
/// Each league's own id on FullTime (the <c>league=</c> number in its fixtures page address), used to fetch the
/// league's whole fixture list. Not to be confused with Team.FullTimeLeagueId, which actually holds the team's
/// divisionseason id (it changes every season; these don't).
/// Hard-coded like the league colours and match days, because the League list itself is fixed in code.
/// </summary>
public static class FullTimeLeagues
{
    /// <summary>The league's FullTime id, or null if it isn't on FullTime or the id hasn't been added yet.</summary>
    public static string? LeagueId(League league) => league switch
    {
        League.WycAndSouthBucksMinor => "882085998",
        League.OxMailYouth => "6306575",          // "Oxfordshire Youth Football League" on FullTime
        League.OxOver50 => "396136022",           // "Oxfordshire Veterans Football League" on FullTime
        League.OxGirls => "7702944",
        // Still to add (teams in these leagues are left out of the FullTime plan until then):
        // League.ADL, League.UHL, League.TVCWFL, League.JPL
        _ => null,
    };
}
