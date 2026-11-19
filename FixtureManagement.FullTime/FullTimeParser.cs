using System.Globalization;
using FixtureManagement.Shared.Dtos;
using FixtureManagement.Shared.Models;
using System.Text.RegularExpressions;
using FixtureManagement.Shared.Reconciliation;
using HtmlAgilityPack;

namespace FixtureManagement.FullTime;

/// <summary>
/// Reads fixtures out of a FullTime "displayTeam" page. Pure (no network), so it can be tested with saved HTML.
/// Moved unchanged from the website's FullTimeReconciliationService.
/// </summary>
public static partial class FullTimeParser
{
    public const string FullTimeUrl = "https://fulltime.thefa.com/displayTeam.html?divisionseason={0}&teamID={1}";
    private const string FixturesTable = "fixtures-table";
    private const string ResultsTable = "results-table";
    private const string CountyCupRow = "fa-county-cup-fixture-row";
    private const string HomeTeam = "home-team";
    private const string AwayTeam = "road-team";
    private const string Score = "score";
    private static readonly CultureInfo UkCulture = new("en-GB");

    public static string TeamPageUrl(TeamDto team) => string.Format(FullTimeUrl, team.FullTimeLeagueId, team.FullTimeTeamId);

    /// <summary>
    /// Upcoming fixtures from the fixtures table, plus postponed games from the results table, for Gameweek.
    /// "Home" means a Thame team is the home side; when both sides are Thame teams, the last word of
    /// <paramref name="team"/>'s name (e.g. "Black") decides which one we are. Fixtures with no date yet (TBC)
    /// are left out, since Gameweek shows a single week.
    /// </summary>
    public static List<FullTimeFixture> ParseFixtures(string html, TeamDto team)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        return ParseFixtures(doc, team);
    }

    /// <summary>As above, for a page HtmlWeb has already loaded and parsed.</summary>
    public static List<FullTimeFixture> ParseFixtures(HtmlDocument doc, TeamDto team) =>
        ReadRows(doc, team)
            .Where(r => r.Date is not null)
            .Select(r => new FullTimeFixture
            {
                Date = r.Date.GetValueOrDefault(),
                IsHome = r.IsHome,
                Opponent = r.Opponent,
                FixtureType = r.FixtureType,
            })
            .ToList();

    /// <summary>
    /// A team's FullTime page as fixtures for an upload (reconciliation): the same rows as ParseFixtures, plus
    /// fixtures with no date yet (Date null), FullTime's fixture id, its type code and a "Postponed" status where
    /// it applies.
    /// </summary>
    public static List<SourceFixture> ParseTeamPage(string html, TeamDto team)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        return ReadRows(doc, team)
            .Select(r => new SourceFixture
            {
                TeamId = team.Id,
                SourceTeamName = r.OurName,
                Date = r.Date,
                IsHome = r.IsHome,
                Opponent = r.Opponent,
                FixtureType = r.FixtureType,
                SourceFixtureId = r.FixtureId,
                Status = r.FixtureType == FixtureType.Postponed ? "Postponed" : "",
                TypeCode = r.TypeCode,
            })
            .ToList();
    }

    private sealed record Row(DateOnly? Date, bool IsHome, string OurName, string Opponent, FixtureType FixtureType, string TypeCode, string? FixtureId);

    private static List<Row> ReadRows(HtmlDocument doc, TeamDto team)
    {
        var rows = new List<Row>();
        try
        {
            var fixtureRows = doc.DocumentNode.SelectNodes($"//div[contains(@class,'{FixturesTable}')]/table/tbody/tr");
            if (fixtureRows != null)
            {
                foreach (var row in fixtureRows)
                {
                    // County cup rows are marked by a class rather than a type code.
                    string type = row.GetAttributeValue("class", "") != CountyCupRow
                        ? row.SelectSingleNode("td[1]").InnerText.Trim()
                        : "CC";
                    rows.Add(ReadRow(row, team, MapFixtureType(team.League, type), type));
                }
            }

            // Postponed games appear in the results table with a "P" in the score column.
            var resultRows = doc.DocumentNode.SelectNodes($"//div[contains(@class,'{ResultsTable}')]/table/tbody/tr");
            if (resultRows != null)
            {
                foreach (var row in resultRows)
                {
                    var scoreNode = row.SelectSingleNode($"td[contains(@class,'{Score}')]");
                    bool isPostponed = scoreNode?.SelectSingleNode("a[contains(text(),'P')]") != null;
                    if (isPostponed) rows.Add(ReadRow(row, team, FixtureType.Postponed, ""));
                }
            }
        }
        catch (Exception e)
        {
            throw new FormatException($"Couldn't read the FullTime page for team {team.Id}: {e.Message}", e);
        }
        return MergeDuplicates(rows);
    }

    /// <summary>
    /// FullTime can list one tie twice on a team's page (a county cup tie appears both as a county cup row and under
    /// the league's own name for the competition). Same date, venue and opponent = one fixture: keep a recognised
    /// type over "Other", and FullTime's fixture id from whichever row has it. TBC rows are left as they are.
    /// </summary>
    private static List<Row> MergeDuplicates(List<Row> rows)
    {
        var merged = new List<Row>();
        foreach (var row in rows)
        {
            int existing = row.Date is null ? -1
                : merged.FindIndex(m => m.Date == row.Date && m.IsHome == row.IsHome && m.Opponent == row.Opponent);
            if (existing < 0)
            {
                merged.Add(row);
                continue;
            }
            var kept = merged[existing];
            var typed = kept.FixtureType == FixtureType.Other && row.FixtureType != FixtureType.Other ? row : kept;
            merged[existing] = typed with { FixtureId = kept.FixtureId ?? row.FixtureId };
        }
        return merged;
    }

    private static Row ReadRow(HtmlNode row, TeamDto team, FixtureType fixtureType, string typeCode)
    {
        DateOnly? date = ReadDate(row.SelectSingleNode("./td[2]").InnerText);
        string home = HtmlEntity.DeEntitize(row.SelectSingleNode($"td[contains(@class,'{HomeTeam}')]").InnerText.Trim());
        string away = HtmlEntity.DeEntitize(row.SelectSingleNode($"td[contains(@class,'{AwayTeam}')]").InnerText.Trim());

        bool isHome = home.ToLower().IndexOf("thame ") >= 0;
        bool isAway = away.ToLower().IndexOf("thame ") >= 0;

        // Thame on both sides: tell them apart by the last word of our team's name.
        if (isHome && isAway)
        {
            string[] names = team.Name.Split(' ');
            string name = names[names.Length - 1].ToLower();
            isHome = home.ToLower().IndexOf(name) >= 0;
        }

        return new Row(date, isHome, isHome ? home : away, isHome ? away : home, fixtureType, typeCode, ReadFixtureId(row));
    }

    /// <summary>"08/11/26 10:00" → 8 Nov 2026; "TBC" → null. Anything else is a page we don't understand.</summary>
    private static DateOnly? ReadDate(string text)
    {
        var trimmed = HtmlEntity.DeEntitize(text).Trim();
        if (trimmed.StartsWith("TBC", StringComparison.OrdinalIgnoreCase)) return null;
        var datePart = trimmed.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        if (DateOnly.TryParseExact(datePart, "dd/MM/yy", UkCulture, DateTimeStyles.None, out var date)) return date;
        throw new FormatException($"Unexpected date \"{trimmed}\".");
    }

    /// <summary>FullTime's id for the fixture, from the row's link to displayFixture/displayCountyFixture, if any.</summary>
    private static string? ReadFixtureId(HtmlNode row)
    {
        var links = row.SelectNodes(".//a[@href]");
        if (links is null) return null;
        foreach (var link in links)
        {
            var match = FixtureLink().Match(HtmlEntity.DeEntitize(link.GetAttributeValue("href", "")));
            if (match.Success) return match.Groups[1].Value;
        }
        return null;
    }

    [GeneratedRegex(@"display(?:County)?Fixture\.html\?(?:.*&)?id=(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex FixtureLink();

    /// <summary>
    /// A FullTime fixture-type code (first column of the fixtures table) for the team's league. Codes differ between
    /// leagues; the table is FullTimeLeagues.FixtureTypeFor. Unknown codes are Other.
    /// </summary>
    public static FixtureType MapFixtureType(League league, string code) =>
        FullTimeLeagues.FixtureTypeFor(league, code) ?? FixtureType.Other;
}
