using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using HtmlAgilityPack;
using System.Globalization;

namespace FixtureManagementV3.Models
{
    public class FixtureDownloader
    {
        public static string FullTimeURL = "https://fulltime.thefa.com/displayTeam.html?divisionseason={0}&teamID={1}";
        public static string Table_Fixtures = "fixtures-table";
        public static string Table_Results = "results-table";
        public static string TR_CountyCupRow = "fa-county-cup-fixture-row";
        public static string Class = "class";
        public static string HomeTeam = "home-team";
        public static string AwayTeam = "road-team";
        public static string Score = "score";

        public static async Task<IList<DownloadFixture>> FromFullTimeAsync(Team team)
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

            

            catch (Exception e)
            {
                Console.WriteLine($"Error getting fixtures for team {team.Id}");
            }

            return dFixtures;

        }

        private static FixtureType GetFixtureType(string type)
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
    }


    public class DownloadFixture
    {
        public Guid Id { get; set; }
        [DataType(DataType.Date)]
        public DateOnly Date { get; set; }
        [Display(Name = "Home")]
        public bool IsHome { get; set; }
        public string Opponent { get; set; } = "";
        [Display(Name = "Type")]
        public FixtureType FixtureType { get; set; }
        public bool Add { get; set; }

    }
}
