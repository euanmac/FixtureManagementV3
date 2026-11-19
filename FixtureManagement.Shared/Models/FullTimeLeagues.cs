namespace FixtureManagement.Shared.Models;

/// <summary>
/// FullTime details per league, hard-coded like the league colours and match days because the League list itself
/// is fixed in code.
/// </summary>
public static class FullTimeLeagues
{
    /// <summary>
    /// The league's own id on FullTime (the <c>league=</c> number in its fixtures page address), or null if it isn't
    /// on FullTime or hasn't been added yet. Not used for fetching at the moment – the FullTime plan fetches each
    /// team's page, which needs only the team's ids – but kept so a league-wide fetch or link is possible later.
    /// Not to be confused with Team.FullTimeLeagueId, which actually holds the team's divisionseason id (it changes
    /// every season; these don't).
    /// </summary>
    public static string? LeagueId(League league) => league switch
    {
        League.WycAndSouthBucksMinor => "882085998",
        League.OxMailYouth => "6306575",          // "Oxfordshire Youth Football League" on FullTime
        League.OxOver50 => "396136022",           // "Oxfordshire Veterans Football League" on FullTime
        League.OxGirls => "7702944",
        // Still to add: League.ADL, League.UHL, League.TVCWFL, League.JPL
        _ => null,
    };

    // Codes in the first column of FullTime's fixtures tables. Each league uses its own codes (e.g. a division code
    // like "50D1"), so they're looked up per league first, then in the codes every league uses.
    private static readonly Dictionary<string, FixtureType> CommonCodes = new(StringComparer.Ordinal)
    {
        ["L"] = FixtureType.League,
        ["Cup"] = FixtureType.Cup,
        ["CC"] = FixtureType.CountyCup,     // county cup rows are recognised by their class and given this code
        ["F"] = FixtureType.Friendly,
        // From the original, single list; which leagues use them isn't known yet, so they apply to all.
        // Move each to its league below once known.
        ["ONE"] = FixtureType.League,
        ["TWO"] = FixtureType.League,
        ["HL2N"] = FixtureType.League,
        ["D3N"] = FixtureType.League,
        ["VETP"] = FixtureType.League,
        ["N"] = FixtureType.League,
    };

    private static readonly Dictionary<string, FixtureType> NoCodes = new();

    // Codes only one league uses, from the first extension upload (19/11/26; cups confirmed by the user).
    private static readonly Dictionary<string, FixtureType> AylesburyCodes = new(StringComparer.Ordinal)
    {
        ["DIV1"] = FixtureType.League,
        ["DIV3"] = FixtureType.League,
        ["IGUB"] = FixtureType.Cup,
    };

    private static readonly Dictionary<string, FixtureType> OxGirlsCodes = new(StringComparer.Ordinal)
    {
        ["O"] = FixtureType.Other,          // the league's "Other Fixtures" groups
    };

    private static readonly Dictionary<string, FixtureType> OxOver50Codes = new(StringComparer.Ordinal)
    {
        ["50D1"] = FixtureType.League,
    };

    private static readonly Dictionary<string, FixtureType> HellenicCodes = new(StringComparer.Ordinal)
    {
        ["PREM"] = FixtureType.League,
        ["BTUR"] = FixtureType.Cup,
        ["HVC"] = FixtureType.Cup,
    };

    /// <summary>Codes only this league uses. Add them as uploads report unknown codes.</summary>
    private static IReadOnlyDictionary<string, FixtureType> LeagueCodes(League league) => league switch
    {
        League.ADL => AylesburyCodes,
        League.OxGirls => OxGirlsCodes,
        League.OxOver50 => OxOver50Codes,
        League.UHL => HellenicCodes,
        _ => NoCodes,
    };

    /// <summary>The fixture type a league's FullTime code means, or null if the code isn't known for that league.</summary>
    public static FixtureType? FixtureTypeFor(League league, string code)
    {
        if (LeagueCodes(league).TryGetValue(code, out var type)) return type;
        if (CommonCodes.TryGetValue(code, out type)) return type;
        return null;
    }
}
