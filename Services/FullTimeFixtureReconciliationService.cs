namespace FixtureManagementV3.Services;

using FixtureManagementV3.Models;
using HtmlAgilityPack;
using System.Globalization;
using System.Collections.Concurrent;
using System.Net.Cache;

public interface IFixtureReconciliationService {
    public  IAsyncEnumerable<List<TeamReconiliationRow>> Reconcile(List<Team> teams, List<Fixture> fixtures, DateOnly gameWeekStart, DateOnly gameWeekEnd);
    public void ClearCache();
    TimeSpan CacheAge { get; }
}

public class FullTimeReconciliationService : IFixtureReconciliationService
{
    public const string FullTimeURL = "https://fulltime.thefa.com/displayTeam.html?divisionseason={0}&teamID={1}";
    public const string Table_Fixtures = "fixtures-table";
    public const string Table_Results = "results-table";
    public const string TR_CountyCupRow = "fa-county-cup-fixture-row";
    public const string Class = "class";
    public const string HomeTeam = "home-team";
    public  const string AwayTeam = "road-team";
    public const string Score = "score";

    public TimeSpan CacheAge {    
        get 
        {
            return cacheRefreshDateTime is null ? new TimeSpan(0) : DateTime.Now - (cacheRefreshDateTime.Value);
        }
    }

    private ConcurrentDictionary<CacheKey, List<DownloadFixture>> downloadFixtureCache = new();
    private DateTime? cacheRefreshDateTime;

    public void ClearCache()
    {
        downloadFixtureCache.Clear();
        cacheRefreshDateTime = null;
    }

    public async IAsyncEnumerable<List<TeamReconiliationRow>>   Reconcile(List<Team> teams, List<Fixture> fixtures, DateOnly gameWeekStart, DateOnly gameWeekEnd)
    {
        List<Task<List<TeamReconiliationRow>>> teamRecTasks = teams
                .Select(t => ReconcileAsync(t, gameWeekStart, gameWeekEnd, fixtures))
                .ToList();

        while (teamRecTasks.Any())
        {
            var finishedTask = await Task.WhenAny(teamRecTasks);
            teamRecTasks.Remove(finishedTask);
            yield return finishedTask.Result;
        }
    }

    public async Task<List<TeamReconiliationRow>> ReconcileAsync(Team team, DateOnly start, DateOnly end, IList<Fixture> fixtures)
    {

        Console.WriteLine($"{team.DisplayName} started");
        TimeOnly startTime = TimeOnly.FromDateTime(DateTime.Now);


        //Download fixtures if team is on fulltime, filter so only within start and end date
        List<DownloadFixture> downloadFixtures = new List<DownloadFixture>();
        CacheKey cacheKey = new CacheKey { TeamId = team.Id, Start = start, End = end };

        //Download or get from cache
        if (team.IsOnFullTime) {
            // downloadFixtures = (await FromFullTimeAsync(team))
            //         .Where(f => (f.Date >= start && f.Date <= end))
            //     .ToList();
            if (downloadFixtureCache.ContainsKey(cacheKey)) 
            {
                downloadFixtures = downloadFixtureCache[cacheKey].ToList();
            } 
            else 
            {
                downloadFixtures = (await FromFullTimeAsync(team))
                    .Where(f => (f.Date >= start && f.Date <= end))
                .ToList();
                downloadFixtureCache[cacheKey] = downloadFixtures.ToList();
            }
        }
        
        //Get list of DB fixtures for date range
        List<Fixture> filteredFixtures = fixtures
            .Where(f => (f.Date >= start && f.Date <= end && f.TeamId == team.Id))
            .ToList();


        //If no local or fulltime fixtures then return rec row
        //Check for rows, if none, create empty row for team to represent no fixture
        if (downloadFixtures.Count == 0 && filteredFixtures.Count == 0)
        {
            int offset = team.MatchDay == DayOfWeek.Sunday ? 6 : (int)team.MatchDay - 1;
            return new List<TeamReconiliationRow> { new TeamReconiliationRow { Team = team, MatchDate = start.AddDays(offset), RecStatus = FixtureRecMatchType.noFixture } };
        }

        //Find fixtures for dates
        //If fixtures exists then try to match
        IList<TeamReconiliationRow> recRows = filteredFixtures.Select(f =>
            {
                //Default row for local fixture
                TeamReconiliationRow recRow = new TeamReconiliationRow {
                    Team = team,
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
                List<DownloadFixture> downloadMatches = downloadFixtures.Where(df =>
                    (f.Date) == df.Date &&
                    f.Opponent == df.Opponent &&
                    f.IsHome == df.IsHome &&
                    f.FixtureType == df.FixtureType
                ).ToList();

                //Found download so check whether it matches
                if (downloadMatches.Count > 0)
                {
                    recRow.RecStatus = FixtureRecMatchType.fullTimematched;
                    //Need to remove downloaded from list!
                    downloadFixtures = downloadFixtures.Except(downloadMatches).ToList();   
                }
                //No match so just set whether there should be a full time match or not if team not set up 
                else
                {
                    recRow.RecStatus = team.IsOnFullTime ? FixtureRecMatchType.localFixtureUnmatched : FixtureRecMatchType.localFixtureOnly;
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
                    Team = team,
                    MatchDate = df.Date,
                    Opponent = df.Opponent,
                    Venue = (df.IsHome ? "H" : "A"),
                    RecStatus = FixtureRecMatchType.fullTimeUnmatched,
                    FixtureType = df.FixtureType

                };
            }).ToList();
        
        var duration = TimeOnly.FromDateTime(DateTime.Now) - startTime;

        Console.WriteLine($"{team.DisplayName} ended in {duration.Seconds}.{duration.Milliseconds}");
        return recRows.Concat(downloadRecs).ToList();

    }

    public async Task<IList<DownloadFixture>> FromFullTimeAsync(Team team)
    {
        List<(Fixture, bool)> fixtureList = new List<(Fixture, bool)>();
        List<DownloadFixture> dFixtures = new List<DownloadFixture>();

        //Get fixtures from FulLTime
        if (team.FullTimeTeamId == null || team.FullTimeLeagueId == null)
        {
            return dFixtures;
        }

        //var url = $"https://fulltime.thefa.com/fixtures.html?selectedSeason={team.FullTimeLeagueId}&selectedFixtureGroupKey=&selectedDateCode=all&selectedClub=&selectedTeam={Team.FullTimeTeamId}&selectedRelatedFixtureOption=2&selectedFixtureDateStatus=&selectedFixtureStatus=&previousSelectedFixtureGroupAgeGroup=&previousSelectedFixtureGroupKey=&previousSelectedClub=&itemsPerPage=100";
        //var url = $"https://fulltime.thefa.com/displayTeam.html?divisionseason={team.FullTimeLeagueId}&teamID={team.FullTimeTeamId}";
        var url = String.Format(FullTimeURL, team.FullTimeLeagueId, team.FullTimeTeamId);
        HtmlWeb web = new HtmlWeb();
        var htmlDoc = await web.LoadFromWebAsync(url);


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
                        string[] names = team.Name.Split(' ');
                        string name = names[names.Length - 1].ToLower();
                        isHome = (home.ToLower().IndexOf(name) >= 0);
                        isAway = (away.ToLower().IndexOf(name) >= 0);
                    }

                    string opponent = isHome ? away : home;
                    FixtureType ftype = GetFixtureType(type);

                    var fixture = new Fixture { Date = fdate, IsHome = isHome, Opponent = opponent, TeamId = team.Id, Team = team, FixtureType = ftype, Id = Guid.NewGuid() };
                    var downloadFixture = new DownloadFixture { Id = Guid.NewGuid(), Date = fdate, IsHome = isHome, Opponent = opponent, FixtureType = ftype, Add = true };

                    fixtureList.Add((fixture, true));
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

                        var fixture = new Fixture { Date = fdate, IsHome = isHome, Opponent = opponent, TeamId = team.Id, Team = team, FixtureType = ftype, Id = Guid.NewGuid() };
                        var downloadFixture = new DownloadFixture { Id = Guid.NewGuid(), Date = fdate, IsHome = isHome, Opponent = opponent, FixtureType = ftype, Add = true };

                        fixtureList.Add((fixture, true));
                        dFixtures.Add(downloadFixture);

                    }
                }
            }
        }

        catch
        {
            Console.WriteLine($"Error getting fixtures for team {team.Id}");
        }

        return dFixtures;

    }


    public IList<DownloadFixture> FromFullTime(Team team)
    {
        List<(Fixture, bool)> fixtureList = new List<(Fixture, bool)>();
        List<DownloadFixture> dFixtures = new List<DownloadFixture>();

        //Get fixtures from FulLTime
        if (team.FullTimeTeamId == null || team.FullTimeLeagueId == null)
        {
            return dFixtures;
        }

        //var url = $"https://fulltime.thefa.com/fixtures.html?selectedSeason={team.FullTimeLeagueId}&selectedFixtureGroupKey=&selectedDateCode=all&selectedClub=&selectedTeam={Team.FullTimeTeamId}&selectedRelatedFixtureOption=2&selectedFixtureDateStatus=&selectedFixtureStatus=&previousSelectedFixtureGroupAgeGroup=&previousSelectedFixtureGroupKey=&previousSelectedClub=&itemsPerPage=100";
        //var url = $"https://fulltime.thefa.com/displayTeam.html?divisionseason={team.FullTimeLeagueId}&teamID={team.FullTimeTeamId}";
        var url = String.Format(FullTimeURL, team.FullTimeLeagueId, team.FullTimeTeamId);
        HtmlWeb web = new HtmlWeb();
        var htmlDoc = web.Load(url);


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
                        string[] names = team.Name.Split(' ');
                        string name = names[names.Length - 1].ToLower();
                        isHome = (home.ToLower().IndexOf(name) >= 0);
                        isAway = (away.ToLower().IndexOf(name) >= 0);
                    }

                    string opponent = isHome ? away : home;
                    FixtureType ftype = GetFixtureType(type);

                    var fixture = new Fixture { Date = fdate, IsHome = isHome, Opponent = opponent, TeamId = team.Id, Team = team, FixtureType = ftype, Id = Guid.NewGuid() };
                    var downloadFixture = new DownloadFixture { Id = Guid.NewGuid(), Date = fdate, IsHome = isHome, Opponent = opponent, FixtureType = ftype, Add = true };

                    fixtureList.Add((fixture, true));
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

                        var fixture = new Fixture { Date = fdate, IsHome = isHome, Opponent = opponent, TeamId = team.Id, Team = team, FixtureType = ftype, Id = Guid.NewGuid() };
                        var downloadFixture = new DownloadFixture { Id = Guid.NewGuid(), Date = fdate, IsHome = isHome, Opponent = opponent, FixtureType = ftype, Add = true };

                        fixtureList.Add((fixture, true));
                        dFixtures.Add(downloadFixture);

                    }
                }

            }

        }

        catch (Exception e)
        {
            Console.WriteLine($"Error getting fixtures for team {team.Id}");
        }

        return dFixtures;

    }

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

}