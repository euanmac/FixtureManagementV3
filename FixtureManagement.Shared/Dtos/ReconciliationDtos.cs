using FixtureManagement.Shared.Reconciliation;

namespace FixtureManagement.Shared.Dtos;

/// <summary>
/// The latest upload from a source compared with the app's fixtures: GET /api/reconciliation/{source} and the
/// review page. Read-only.
/// </summary>
public record ReconciliationReviewDto
{
    /// <summary>The upload compared against, without its fixtures. Null if the source has never been uploaded.</summary>
    public SourceUploadSummaryDto? Upload { get; init; }
    /// <summary>The differences still to decide, by date (TBC last). Ones kept earlier aren't included.</summary>
    public IReadOnlyList<ReconciliationItem> Items { get; init; } = [];
    /// <summary>Fixtures that agree.</summary>
    public int SameCount { get; init; }
    /// <summary>Differences hidden because someone chose "keep the app version" for them earlier.</summary>
    public int KeptCount { get; init; }
    public IReadOnlyList<FixtureClash> Clashes { get; init; } = [];
    /// <summary>The dates compared (inclusive): from today (or the upload's start, if later) to the upload's end.</summary>
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
}

public record SourceUploadSummaryDto
{
    public string Source { get; init; } = "";
    public string SourceName { get; init; } = "";
    public DateTime UploadedAt { get; init; }
    public string UploadedBy { get; init; } = "";
    public int TeamCount { get; init; }
    public int FixtureCount { get; init; }
}

/// <summary>POST /api/reconciliation/{source}/apply: the choice for each difference, by ReconciliationItem.Id.</summary>
public record ReconciliationApplyRequest
{
    public IReadOnlyDictionary<string, Resolution> Choices { get; init; } = new Dictionary<string, Resolution>();
}

/// <summary>What applying the choices did.</summary>
public record ReconciliationApplyResultDto
{
    public int Added { get; init; }
    public int Updated { get; init; }
    public int Deleted { get; init; }
    /// <summary>Differences remembered as "keep the app version".</summary>
    public int Kept { get; init; }
    /// <summary>Choices whose difference no longer exists (the data changed since the page was shown); nothing was done for them.</summary>
    public int Skipped { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
}
