using FixtureManagement.Shared.Reconciliation;

namespace FixtureManagement.Shared.Dtos;

/// <summary>
/// An upload of fixtures from a source (FullTime, a CSV …), as saved for reconciliation. Only the latest upload
/// per Source is kept. Id and UploadedAt are set when it's saved.
/// </summary>
public record SourceUploadDto
{
    public Guid Id { get; init; }
    /// <summary>Identifies the source, e.g. "FullTime". A new upload replaces the last one with the same value.</summary>
    public string Source { get; init; } = "";
    /// <summary>For people, e.g. "FullTime".</summary>
    public string SourceName { get; init; } = "";
    public DateTime UploadedAt { get; init; }
    public string UploadedBy { get; init; } = "";
    /// <summary>The dates the upload covers (inclusive): app fixtures outside them aren't compared with it.</summary>
    public DateOnly From { get; init; }
    public DateOnly To { get; init; }
    /// <summary>The teams the upload covers: app fixtures of other teams aren't compared with it.</summary>
    public IReadOnlyList<Guid> TeamIds { get; init; } = [];
    public IReadOnlyList<SourceFixture> Fixtures { get; init; } = [];
}

/// <summary>POST /api/fulltime/upload: the FullTime team pages the browser extension fetched, as they are.</summary>
public record FullTimeUploadRequest
{
    public IReadOnlyList<FullTimePageDto> Pages { get; init; } = [];
}

public record FullTimePageDto
{
    /// <summary>Our team (from the plan).</summary>
    public Guid TeamId { get; init; }
    /// <summary>The team's FullTime page, unchanged.</summary>
    public string Html { get; init; } = "";
}

/// <summary>What POST /api/fulltime/upload saved.</summary>
public record FullTimeUploadResultDto
{
    public Guid UploadId { get; init; }
    /// <summary>Teams whose pages were read and are now covered by the upload.</summary>
    public int Teams { get; init; }
    public int Fixtures { get; init; }
    /// <summary>Pages that couldn't be used. Those teams aren't covered, so their fixtures aren't compared.</summary>
    public IReadOnlyList<FullTimePageErrorDto> Failed { get; init; } = [];
}

public record FullTimePageErrorDto
{
    public Guid TeamId { get; init; }
    public string TeamName { get; init; } = "";
    public string Error { get; init; } = "";
}
