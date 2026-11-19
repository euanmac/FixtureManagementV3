namespace FixtureManagement.Server.Models;

/// <summary>
/// One upload of fixtures from a source (a FullTime fetch for a league, a CSV, a league grid …).
/// Only the latest upload per Source is kept: a new one replaces it and its fixtures.
/// </summary>
public class SourceUpload
{
    public Guid Id { get; set; }
    /// <summary>Identifies the source, e.g. "FullTime:882085998" or "Csv". The latest upload per value is kept.</summary>
    public string Source { get; set; } = "";
    /// <summary>For people, e.g. "FullTime – Wycombe and South Bucks Minor League".</summary>
    public string SourceName { get; set; } = "";
    /// <summary>When it was uploaded (UTC).</summary>
    public DateTime UploadedAt { get; set; }
    /// <summary>The signed-in user who uploaded it.</summary>
    public string UploadedBy { get; set; } = "";
    /// <summary>The dates the upload covers (inclusive). App fixtures outside them aren't compared with it.</summary>
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    /// <summary>The teams the upload covers. App fixtures of other teams aren't compared with it.</summary>
    public List<Guid> TeamIds { get; set; } = [];

    public List<UploadedFixture> Fixtures { get; set; } = [];
}
