using FixtureManagement.Shared;
using FixtureManagement.Shared.Dtos;
using FixtureManagement.Shared.Models;
using FixtureManagement.Shared.Reconciliation;

namespace FixtureManagement.FullTime;

/// <summary>
/// Turns the FullTime team pages the browser extension sent into fixtures for an upload. Pure.
/// A page only counts if it looks like a FullTime team page: a blocked or error page has no fixtures in it, and
/// treating it as "this team has no fixtures" would make every one of the team's app fixtures look deleted.
/// </summary>
public static class FullTimeUploadReader
{
    /// <summary>The SourceUpload.Source value for FullTime uploads.</summary>
    public const string Source = "FullTime";

    public sealed record Result(IReadOnlyList<Guid> TeamIds, IReadOnlyList<SourceFixture> Fixtures,
        IReadOnlyList<FullTimePageErrorDto> Failed, IReadOnlyList<FullTimeTypeCodeDto> UnknownTypeCodes);

    public static Result Read(IReadOnlyList<FullTimePageDto> pages, IReadOnlyCollection<TeamDto> teams)
    {
        var teamsById = teams.ToDictionary(t => t.Id);
        var teamIds = new List<Guid>();
        var fixtures = new List<SourceFixture>();
        var failed = new List<FullTimePageErrorDto>();

        foreach (var page in pages)
        {
            teamsById.TryGetValue(page.TeamId, out var team);
            string? error =
                team is null ? "Not a team on the site."
                : string.IsNullOrWhiteSpace(team.FullTimeLeagueId) || string.IsNullOrWhiteSpace(team.FullTimeTeamId) ? "The team has no FullTime ids."
                : teamIds.Contains(team.Id) ? "The team's page was sent more than once."
                : !LooksLikeATeamPage(page.Html) ? "This isn't a FullTime team page (FullTime may have blocked the request, or changed its pages)."
                : null;

            if (error is null)
            {
                try
                {
                    fixtures.AddRange(FullTimeParser.ParseTeamPage(page.Html, team!));
                    teamIds.Add(team!.Id);
                }
                catch (FormatException e)
                {
                    error = e.Message;
                }
            }

            if (error is not null)
                failed.Add(new FullTimePageErrorDto { TeamId = page.TeamId, TeamName = team?.DisplayName ?? "", Error = error });
        }

        return new Result(teamIds, fixtures, failed, UnknownCodes(fixtures, teamsById));
    }

    /// <summary>Type codes the team's league doesn't know, per league, so they can be added to FullTimeLeagues.</summary>
    private static List<FullTimeTypeCodeDto> UnknownCodes(List<SourceFixture> fixtures, Dictionary<Guid, TeamDto> teamsById) =>
        fixtures
            .Select(f => (Fixture: f, Team: teamsById[f.TeamId]))
            .Where(x => x.Fixture.TypeCode.Length > 0 && FullTimeLeagues.FixtureTypeFor(x.Team.League, x.Fixture.TypeCode) is null)
            .GroupBy(x => (x.Team.League, x.Fixture.TypeCode))
            .OrderBy(g => g.Key.League.DisplayName()).ThenBy(g => g.Key.TypeCode)
            .Select(g =>
            {
                var (fixture, team) = g.First();
                return new FullTimeTypeCodeDto
                {
                    League = g.Key.League,
                    LeagueName = g.Key.League.DisplayName(),
                    Code = g.Key.TypeCode,
                    Count = g.Count(),
                    Example = $"{team.DisplayName}: {fixture.Date?.ToString("dd/MM/yyyy") ?? "TBC"} v {fixture.Opponent}",
                };
            })
            .ToList();

    /// <summary>A team page has a fixtures or results table. Blocked/error pages have neither.</summary>
    private static bool LooksLikeATeamPage(string html) =>
        html.Contains("fixtures-table", StringComparison.Ordinal) || html.Contains("results-table", StringComparison.Ordinal);
}
