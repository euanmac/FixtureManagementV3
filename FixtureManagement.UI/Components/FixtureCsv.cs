using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using FixtureManagement.Shared.Dtos;
using FixtureManagement.Shared.Models;

namespace FixtureManagement.UI.Components;

/// <summary>
/// Reads an uploaded fixtures CSV (columns Opponent, Venue, Date) into new league fixtures for one team.
/// Used by the website's Upload2 page and the desktop app's upload page.
/// Dates are read as before (invariant culture: ISO 2026-11-14, 11/14/2026, 14-Nov-2026 …), so files that
/// imported before still import the same way.
/// </summary>
public static class FixtureCsv
{
    public sealed record Result(IReadOnlyList<FixtureDto> Fixtures, string? Error)
    {
        public bool Succeeded => Error is null;
    }

    public static async Task<Result> ReadAsync(Stream stream, Guid teamId, CancellationToken cancellationToken = default)
    {
        // Buffer first: a browser/BlazorWebView file stream only supports async reads, and CsvHelper reads synchronously.
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            PrepareHeaderForMatch = args => args.Header.Trim().ToLowerInvariant(),   // "opponent", " Venue " … all match
        };
        using var reader = new StreamReader(buffer);
        using var csv = new CsvReader(reader, config);
        try
        {
            var fixtures = new List<FixtureDto>();
            foreach (var row in csv.GetRecords<Row>())
            {
                fixtures.Add(new FixtureDto
                {
                    Id = Guid.NewGuid(),
                    TeamId = teamId,
                    Opponent = row.Opponent.Trim(),
                    IsHome = row.Venue.Trim().StartsWith("H", StringComparison.OrdinalIgnoreCase),
                    Date = row.Date,
                    FixtureType = FixtureType.League,
                });
            }
            return fixtures.Count == 0
                ? new Result([], "The file has no fixtures in it.")
                : new Result(fixtures, null);
        }
        catch (HeaderValidationException)
        {
            return new Result([], "The file needs a header row with the columns Opponent, Venue and Date.");
        }
        catch (CsvHelperException ex)
        {
            var line = ex.Context?.Parser?.RawRow;
            return new Result([], line is null
                ? "The file couldn't be read as a fixtures CSV."
                : $"Line {line} couldn't be read – check its date and that it has Opponent, Venue and Date values.");
        }
    }

    // Property names are the CSV column names (matched case-insensitively).
    private sealed class Row
    {
        public string Opponent { get; set; } = "";
        public string Venue { get; set; } = "";
        public DateOnly Date { get; set; }
    }
}
