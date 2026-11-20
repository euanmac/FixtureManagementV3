using FixtureManagement.Shared.Dtos;

namespace FixtureManagement.Shared.Reconciliation;

/// <summary>
/// A source can be uploaded in parts – FullTime one league at a time ("FullTime:OxGirls", …) – each part replacing only
/// its own latest upload. Combine merges the latest parts into one upload to compare with. Pure.
/// </summary>
public static class SourceUploads
{
    /// <summary>The part of a source an upload belongs to, e.g. "FullTime:OxGirls".</summary>
    public static string Part(string source, string part) => $"{source}:{part}";

    /// <summary>
    /// Each team's fixtures come from the most recent upload that covers the team (a team that changed league may be in
    /// two parts). Fixtures keep their own upload's time and uploader. The combined upload is stamped with the most
    /// recent part, and covers from the latest start date of the parts used, so no part is compared before it began.
    /// </summary>
    public static SourceUploadDto? Combine(string source, IReadOnlyList<SourceUploadDto> uploads)
    {
        if (uploads.Count == 0) return null;
        var newestFirst = uploads.OrderByDescending(u => u.UploadedAt).ToList();
        var teams = new HashSet<Guid>();
        var fixtures = new List<SourceFixture>();
        var used = new List<SourceUploadDto>();

        foreach (var upload in newestFirst)
        {
            var newTeams = upload.TeamIds.Where(t => !teams.Contains(t)).ToHashSet();
            var unlinked = upload.Fixtures.Where(f => f.TeamId == Guid.Empty);
            fixtures.AddRange(upload.Fixtures.Where(f => newTeams.Contains(f.TeamId)).Concat(unlinked)
                .Select(f => f with { UploadedAt = upload.UploadedAt, UploadedBy = upload.UploadedBy }));
            if (newTeams.Count > 0) used.Add(upload);
            teams.UnionWith(newTeams);
        }

        var newest = newestFirst[0];
        var parts = used.Count > 0 ? used : [newest];
        return new SourceUploadDto
        {
            Id = newest.Id,
            Source = source,
            SourceName = source,
            UploadedAt = newest.UploadedAt,
            UploadedBy = newest.UploadedBy,
            From = parts.Max(u => u.From),
            To = parts.Min(u => u.To),
            TeamIds = teams.ToList(),
            Fixtures = fixtures,
        };
    }
}
