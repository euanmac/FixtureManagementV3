namespace FixtureManagement.Server.Models;

/// <summary>A fixture as a source listed it, in its latest SourceUpload. Seen from our team's side.</summary>
public class UploadedFixture
{
    public Guid Id { get; set; }
    public Guid SourceUploadId { get; set; }
    /// <summary>Our team, or null if the source names a club team that isn't linked to one of ours.</summary>
    public Guid? TeamId { get; set; }
    /// <summary>Our team's name as the source gives it.</summary>
    public string SourceTeamName { get; set; } = "";
    /// <summary>Null when the source says TBC.</summary>
    public DateOnly? Date { get; set; }
    public bool IsHome { get; set; }
    public string Opponent { get; set; } = "";
    public FixtureType FixtureType { get; set; }
    /// <summary>Division or competition name.</summary>
    public string Competition { get; set; } = "";
    /// <summary>The source's own id for the fixture (e.g. FullTime's), if it has one.</summary>
    public string? SourceFixtureId { get; set; }
    /// <summary>The source's status note, e.g. "Postponed".</summary>
    public string Status { get; set; } = "";

    public SourceUpload? SourceUpload { get; set; }
}
