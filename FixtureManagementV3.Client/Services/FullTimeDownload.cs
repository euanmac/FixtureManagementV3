namespace FixtureManagementV3.Client.Services;

using HtmlAgilityPack;
using System.Globalization;
using System.Collections.Concurrent;
using System.Net.Cache;



public class FullTimeDownload
{
    public const string FullTimeURL = "https://fulltime.thefa.com/displayTeam.html?divisionseason={0}&teamID={1}";
    public const string Table_Fixtures = "fixtures-table";
    public const string Table_Results = "results-table";
    public const string TR_CountyCupRow = "fa-county-cup-fixture-row";
    public const string Class = "class";
    public const string HomeTeam = "home-team";
    public  const string AwayTeam = "road-team";
    public const string Score = "score";

    private ConcurrentDictionary<CacheKey, List<DownloadFixture>> downloadFixtureCache = new();
    private DateTime? cacheRefreshDateTime;

    public void ClearCache()
    {
        downloadFixtureCache.Clear();
        cacheRefreshDateTime = null;
    }


    public async Task<IList<DownloadFixture>> FromFullTimeAsync(string LeagueId, string TeamId, string TeamName)
    {
        List<DownloadFixture> dFixtures = new List<DownloadFixture>();

        //Get fixtures from FulLTime
        if (LeagueId == null || TeamId == null)
        {
            return dFixtures;
        }

        //var url = $"https://fulltime.thefa.com/fixtures.html?selectedSeason={team.FullTimeLeagueId}&selectedFixtureGroupKey=&selectedDateCode=all&selectedClub=&selectedTeam={Team.FullTimeTeamId}&selectedRelatedFixtureOption=2&selectedFixtureDateStatus=&selectedFixtureStatus=&previousSelectedFixtureGroupAgeGroup=&previousSelectedFixtureGroupKey=&previousSelectedClub=&itemsPerPage=100";
        //var url = $"https://fulltime.thefa.com/displayTeam.html?divisionseason={team.FullTimeLeagueId}&teamID={team.FullTimeTeamId}";
        var url = String.Format(FullTimeURL, LeagueId, TeamId);
        HtmlWeb web = new();
        var htmlDoc =  web.Load(url);


        //Get fixture rows
        var rows = htmlDoc.DocumentNode.SelectNodes($"//div[contains(@class,'{Table_Fixtures}')]/table/tbody/tr");

        try
        {
            if (rows != null)
            {
                //Get fixtures from fixtures table
                foreach (var row in rows)
                {
                    //Check whether CC row - if so default type to CC
                    var typeNode = row.SelectSingleNode("td[1]");
                    string type = (row.Attributes["class"].Value != TR_CountyCupRow) ?
                        row.SelectSingleNode("td[1]").InnerText.Trim() :
                        "CC";

                    string date = row.SelectSingleNode("./td[2]").InnerText.Trim().Substring(0, 8);
                    string home = HtmlEntity.DeEntitize(row.SelectSingleNode($"td[contains(@class,'{HomeTeam}')]").InnerText.Trim());
                    string away = HtmlEntity.DeEntitize(row.SelectSingleNode($"td[contains(@class,'{AwayTeam}')]").InnerText.Trim());

                    //string home = HtmlEntity.DeEntitize(row.SelectSingleNode("./td[3]").InnerText.Trim());
                    //string away = HtmlEntity.DeEntitize(row.SelectSingleNode("./td[7]").InnerText.Trim());
                    //string link = row.SelectSingleNode("./td[3]/a").Attributes[0].Value;
                    //int index = link.IndexOf("=") + 1;
                    //string fixtureId = link.Substring(index, link.Length - index);

                    DateOnly fdate = DateOnly.Parse(date, new CultureInfo("en-GB"));
                    bool isHome = home.ToLower().IndexOf("thame ") >= 0;
                    bool isAway = away.ToLower().IndexOf("thame ") >= 0;

                    //if thame in both home and away then try and differentiate based on last name
                    if (isHome && isAway)
                    {
                        //find team colour / differentiator, i.e. last word in full name
                        string[] names = TeamName.Split(' ');
                        string name = names[names.Length - 1].ToLower();
                        isHome = (home.ToLower().IndexOf(name) >= 0);
                        isAway = (away.ToLower().IndexOf(name) >= 0);
                    }

                    string opponent = isHome ? away : home;
                    FixtureType ftype = GetFixtureType(type);

                    // var fixture = new Fixture { Date = fdate, IsHome = isHome, Opponent = opponent, TeamId = team.Id, Team = team, FixtureType = ftype, Id = Guid.NewGuid() };
                    var downloadFixture = new DownloadFixture { Id = Guid.NewGuid(), Date = fdate, IsHome = isHome, Opponent = opponent, FixtureType = ftype, Add = true };

                    // fixtureList.Add((fixture, true));
                    dFixtures.Add(downloadFixture);
                }
            }

            //now find postponed rows
            var postponedrows = htmlDoc.DocumentNode.SelectNodes($"//div[contains(@class,'{Table_Results}')]/table/tbody/tr");

            if (postponedrows != null)
            {
                foreach (var row in postponedrows)
                {
                    string date = row.SelectSingleNode("./td[2]").InnerText.Trim().Substring(0, 8);
                    string home = row.SelectSingleNode($"td[contains(@class,'{HomeTeam}')]").InnerText.Trim();
                    string away = row.SelectSingleNode($"td[contains(@class,'{AwayTeam}')]").InnerText.Trim();

                    var scoreNode = row.SelectSingleNode($"td[contains(@class,'{Score}')]");
                    bool isPostponed = scoreNode.SelectSingleNode("a[contains(text(),'P')]") != null;
                    if (isPostponed)
                    {
                        DateOnly fdate = DateOnly.Parse(date, new CultureInfo("en-GB"));
                        bool isHome = home.ToLower().IndexOf("thame ") >= 0;
                        string opponent = isHome ? away : home;
                        FixtureType ftype = FixtureType.Postponed;

                        // var fixture = new Fixture { Date = fdate, IsHome = isHome, Opponent = opponent, TeamId = team.Id, Team = team, FixtureType = ftype, Id = Guid.NewGuid() };
                        var downloadFixture = new DownloadFixture { Id = Guid.NewGuid(), Date = fdate, IsHome = isHome, Opponent = opponent, FixtureType = ftype, Add = true };

                        // fixtureList.Add((fixture, true));
                        dFixtures.Add(downloadFixture);

                    }
                }
            }
        }

        catch (Exception e)
        {
            Console.WriteLine($"Error getting fixtures for team {TeamId}: {e.Message}");
            throw new Exception($"Error getting fixtures for team {TeamId}: {e.Message}");
        }

        return dFixtures;

    }


    // public IList<DownloadFixture> FromFullTime(Team team)
    // {
    //     List<(Fixture, bool)> fixtureList = new List<(Fixture, bool)>();
    //     List<DownloadFixture> dFixtures = new List<DownloadFixture>();

    //     //Get fixtures from FulLTime
    //     if (team.FullTimeTeamId == null || team.FullTimeLeagueId == null)
    //     {
    //         return dFixtures;
    //     }

    //     //var url = $"https://fulltime.thefa.com/fixtures.html?selectedSeason={team.FullTimeLeagueId}&selectedFixtureGroupKey=&selectedDateCode=all&selectedClub=&selectedTeam={Team.FullTimeTeamId}&selectedRelatedFixtureOption=2&selectedFixtureDateStatus=&selectedFixtureStatus=&previousSelectedFixtureGroupAgeGroup=&previousSelectedFixtureGroupKey=&previousSelectedClub=&itemsPerPage=100";
    //     //var url = $"https://fulltime.thefa.com/displayTeam.html?divisionseason={team.FullTimeLeagueId}&teamID={team.FullTimeTeamId}";
    //     var url = String.Format(FullTimeURL, team.FullTimeLeagueId, team.FullTimeTeamId);
    //     HtmlWeb web = new HtmlWeb();
    //     var htmlDoc = web.Load(url);


    //     //Get fixture rows
    //     var rows = htmlDoc.DocumentNode.SelectNodes($"//div[contains(@class,'{Table_Fixtures}')]/table/tbody/tr");

    //     try
    //     {
    //         if (rows != null)
    //         {
    //             //Get fixtures from fixtures table
    //             foreach (var row in rows)
    //             {
    //                 //Check whether CC row - if so default type to CC
    //                 var typeNode = row.SelectSingleNode("td[1]");
    //                 string type = (row.Attributes["class"].Value != TR_CountyCupRow) ?
    //                     row.SelectSingleNode("td[1]").InnerText.Trim() :
    //                     "CC";

    //                 string date = row.SelectSingleNode("./td[2]").InnerText.Trim().Substring(0, 8);
    //                 string home = HtmlEntity.DeEntitize(row.SelectSingleNode($"td[contains(@class,'{HomeTeam}')]").InnerText.Trim());
    //                 string away = HtmlEntity.DeEntitize(row.SelectSingleNode($"td[contains(@class,'{AwayTeam}')]").InnerText.Trim());

    //                 //string home = HtmlEntity.DeEntitize(row.SelectSingleNode("./td[3]").InnerText.Trim());
    //                 //string away = HtmlEntity.DeEntitize(row.SelectSingleNode("./td[7]").InnerText.Trim());
    //                 //string link = row.SelectSingleNode("./td[3]/a").Attributes[0].Value;
    //                 //int index = link.IndexOf("=") + 1;
    //                 //string fixtureId = link.Substring(index, link.Length - index);

    //                 DateOnly fdate = DateOnly.Parse(date, new CultureInfo("en-GB"));
    //                 bool isHome = home.ToLower().IndexOf("thame ") >= 0;
    //                 bool isAway = away.ToLower().IndexOf("thame ") >= 0;

    //                 //if thame in both home and away then try and differentiate based on last name
    //                 if (isHome && isAway)
    //                 {
    //                     //find team colour / differentiator, i.e. last word in full name
    //                     string[] names = team.Name.Split(' ');
    //                     string name = names[names.Length - 1].ToLower();
    //                     isHome = (home.ToLower().IndexOf(name) >= 0);
    //                     isAway = (away.ToLower().IndexOf(name) >= 0);
    //                 }

    //                 string opponent = isHome ? away : home;
    //                 FixtureType ftype = GetFixtureType(type);

    //                 var fixture = new Fixture { Date = fdate, IsHome = isHome, Opponent = opponent, TeamId = team.Id, Team = team, FixtureType = ftype, Id = Guid.NewGuid() };
    //                 var downloadFixture = new DownloadFixture { Id = Guid.NewGuid(), Date = fdate, IsHome = isHome, Opponent = opponent, FixtureType = ftype, Add = true };

    //                 fixtureList.Add((fixture, true));
    //                 dFixtures.Add(downloadFixture);
    //             }
    //         }

    //         //now find postponed rows
    //         var postponedrows = htmlDoc.DocumentNode.SelectNodes($"//div[contains(@class,'{Table_Results}')]/table/tbody/tr");

    //         if (postponedrows != null)
    //         {
    //             foreach (var row in postponedrows)
    //             {
    //                 string date = row.SelectSingleNode("./td[2]").InnerText.Trim().Substring(0, 8);
    //                 string home = row.SelectSingleNode($"td[contains(@class,'{HomeTeam}')]").InnerText.Trim();
    //                 string away = row.SelectSingleNode($"td[contains(@class,'{AwayTeam}')]").InnerText.Trim();

    //                 var scoreNode = row.SelectSingleNode($"td[contains(@class,'{Score}')]");
    //                 bool isPostponed = scoreNode.SelectSingleNode("a[contains(text(),'P')]") != null;
    //                 if (isPostponed)
    //                 {
    //                     DateOnly fdate = DateOnly.Parse(date, new CultureInfo("en-GB"));
    //                     bool isHome = home.ToLower().IndexOf("thame ") >= 0;
    //                     string opponent = isHome ? away : home;
    //                     FixtureType ftype = FixtureType.Postponed;

    //                     var fixture = new Fixture { Date = fdate, IsHome = isHome, Opponent = opponent, TeamId = team.Id, Team = team, FixtureType = ftype, Id = Guid.NewGuid() };
    //                     var downloadFixture = new DownloadFixture { Id = Guid.NewGuid(), Date = fdate, IsHome = isHome, Opponent = opponent, FixtureType = ftype, Add = true };

    //                     fixtureList.Add((fixture, true));
    //                     dFixtures.Add(downloadFixture);

    //                 }
    //             }

    //         }

    //     }

    //     catch (Exception e)
    //     {
    //         Console.WriteLine($"Error getting fixtures for team {team.Id}");
    //         throw new Exception($"Error getting fixtures for team {team.Id}: {e.Message}");
    //     }

    //     return dFixtures;

    // }

    private FixtureType GetFixtureType(string type)
    {
        return type switch
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

    public struct DownloadFixture
    {
        public Guid Id { get; set; }
        public DateOnly Date { get; set; }
        public bool IsHome { get; set; }
        public string Opponent { get; set; }
        public FixtureType FixtureType { get; set; }
        public bool Add { get; set; }
    }

    public struct CacheKey
    {
        public Guid TeamId { get; set; }
        public DateOnly Start { get; set; }
        public DateOnly End { get; set; }
    }

    public enum FixtureType
    {
        Friendly =0,
        League =1,
        Cup =2,
        CountyCup =6,
        Cancelled =3,
        Postponed =4,
        Other =5
    }


        private string FixtureTypeShortName(FixtureType fixtureType)
        {
            return fixtureType switch
            {
                FixtureType.Friendly => "F",
                FixtureType.League => "L",
                FixtureType.Cup => "C",
                FixtureType.Postponed => "P",
                FixtureType.Cancelled => "X",
                FixtureType.CountyCup => "CC",
                _ => "O"
            };
        }


}