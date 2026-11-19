namespace FixtureManagement.Server.Models;

/// <summary>
/// A remembered "keep the app version" for one difference, so it isn't raised again on the next upload.
/// The key describes both sides exactly (ReconciliationItem.Key), so if either side changes again the
/// difference is shown afresh.
/// </summary>
public class ReconciliationDecision
{
    public Guid Id { get; set; }
    public string Key { get; set; } = "";
    public Guid TeamId { get; set; }
    /// <summary>What was decided about, for people (e.g. "13/12/2026 Rivals (H) – not on FullTime").</summary>
    public string Description { get; set; } = "";
    /// <summary>When it was decided (UTC).</summary>
    public DateTime DecidedAt { get; set; }
    public string DecidedBy { get; set; } = "";
}
