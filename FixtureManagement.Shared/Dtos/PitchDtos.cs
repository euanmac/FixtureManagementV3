namespace FixtureManagement.Shared.Dtos;

/// <summary>A pitch as returned by IPitchService / GET /api/pitches. Read-only for now.</summary>
public record PitchDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = "";
    public int DisplayOrder { get; init; }
}
