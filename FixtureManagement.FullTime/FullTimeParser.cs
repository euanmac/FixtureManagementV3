using System.Globalization;
using FixtureManagement.Shared.Dtos;
using FixtureManagement.Shared.Models;
using HtmlAgilityPack;

namespace FixtureManagement.FullTime;

/// <summary>
/// Reads fixtures out of a FullTime "displayTeam" page. Pure (no network), so it can be tested with saved HTML.
/// Moved unchanged from the website's FullTimeReconciliationService.
/// </summary>
public static class FullTimeParser
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
    /// Upcoming fixtures from the fixtures table, plus postponed games from the results table.
    /// "Home" means a Thame team is the home side; when both sides are Thame teams, the last word of
    /// <paramref name="team"/>'s name (e.g. "Black") decides which one we are.
    /// </summary>
    public static List<FullTimeFixture> ParseFixtures(string html, TeamDto team)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var fixtures = new List<FullTimeFixture>();

        try
        {
            var rows = doc.DocumentNode.SelectNodes($"//div[contains(@class,'{FixturesTable}')]/table/tbody/tr");
            if (rows != null)
            {
                foreach (var row in rows)
                {
                    // County cup rows are marked by a class rather than a type code.
                    string type = row.GetAttributeValue("class", "") != CountyCupRow
                        ? row.SelectSingleNode("td[1]").InnerText.Trim()
                        : "CC";

                    string date = row.SelectSingleNode("./td[2]").InnerText.Trim().Substring(0, 8);
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

                    fixtures.Add(new FullTimeFixture
                    {
                        Date = DateOnly.Parse(date, UkCulture),
                        IsHome = isHome,
                        Opponent = isHome ? away : home,
                        FixtureType = MapFixtureType(type),
                    });
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
                    if (!isPostponed) continue;

                    string date = row.SelectSingleNode("./td[2]").InnerText.Trim().Substring(0, 8);
                    string home = row.SelectSingleNode($"td[contains(@class,'{HomeTeam}')]").InnerText.Trim();
                    string away = row.SelectSingleNode($"td[contains(@class,'{AwayTeam}')]").InnerText.Trim();
                    bool isHome = home.ToLower().IndexOf("thame ") >= 0;

                    fixtures.Add(new FullTimeFixture
                    {
                        Date = DateOnly.Parse(date, UkCulture),
                        IsHome = isHome,
                        Opponent = isHome ? away : home,
                        FixtureType = FixtureType.Postponed,
                    });
                }
            }
        }
        catch (Exception e)
        {
            throw new FormatException($"Couldn't read the FullTime page for team {team.Id}: {e.Message}", e);
        }

        return fixtures;
    }

    /// <summary>FullTime's fixture-type codes (first column of the fixtures table).</summary>
    public static FixtureType MapFixtureType(string code) => code switch
    {
        "F" => FixtureType.Friendly,
        "Cup" => FixtureType.Cup,
        "CC" => FixtureType.CountyCup,
        "L" => FixtureType.League,
        "ONE" => FixtureType.League,
        "TWO" => FixtureType.League,
        "HL2N" => FixtureType.League,
        "D3N" => FixtureType.League,
        "VETP" => FixtureType.League,
        "N" => FixtureType.League,
        _ => FixtureType.Other
    };
}
