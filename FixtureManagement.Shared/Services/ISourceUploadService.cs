using FixtureManagement.Shared.Dtos;

namespace FixtureManagement.Shared.Services;

/// <summary>Uploads of fixtures from sources (FullTime, CSV …) kept for reconciliation. Latest per source only.</summary>
public interface ISourceUploadService
{
    /// <summary>Saves the upload, replacing the previous one with the same Source. Returns it with Id and UploadedAt set.</summary>
    Task<SourceUploadDto> SaveUploadAsync(SourceUploadDto upload, CancellationToken cancellationToken = default);

    /// <summary>The latest upload from a source, with its fixtures, or null if there's none.</summary>
    Task<SourceUploadDto?> GetLatestUploadAsync(string source, CancellationToken cancellationToken = default);
}
