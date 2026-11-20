using FixtureManagement.Server.Models;
using FixtureManagement.Shared;
using FixtureManagement.Shared.Dtos;
using FixtureManagement.Shared.Reconciliation;
using FixtureManagement.Shared.Services;
using Microsoft.EntityFrameworkCore;

namespace FixtureManagement.Server.Services;

/// <summary>
/// IReconciliationService for the website: the latest upload (ISourceUploadService) against the app's fixtures
/// (IFixtureService), with "keep the app version" decisions in the ReconciliationDecision table.
/// </summary>
public class EfReconciliationService(
    IDbContextFactory<AppDBContext> dbFactory,
    ISourceUploadService uploads,
    ITeamService teams,
    IFixtureService fixtures,
    IHttpContextAccessor httpContext,
    TimeProvider time) : IReconciliationService
{
    public async Task<ReconciliationReviewDto> GetReviewAsync(string source, CancellationToken cancellationToken = default)
    {
        var (review, _) = await BuildAsync(source, cancellationToken);
        return review;
    }

    public async Task<ReconciliationWeekDto> GetWeekAsync(string source, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var upload = await uploads.GetCombinedUploadAsync(source, cancellationToken);
        if (upload is null) return new ReconciliationWeekDto();

        // Compare everything the upload covers (not just the week), so moves into or out of the week pair up.
        var covered = (await teams.GetTeamsAsync(cancellationToken)).Where(t => upload.TeamIds.Contains(t.Id)).ToList();
        var appFixtures = await fixtures.GetFixturesAsync(
            new FixtureQuery { From = upload.From, To = upload.To == DateOnly.MaxValue ? null : upload.To }, cancellationToken);
        var result = FixtureReconciler.Reconcile(covered, upload.From, upload.To, appFixtures, upload.Fixtures);

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var prefix = DecisionPrefix(source);
        var decisions = (await db.ReconciliationDecision.AsNoTracking()
                .Where(d => d.Key.StartsWith(prefix)).ToListAsync(cancellationToken))
            .ToDictionary(d => d.Key);

        bool InWeek(DateOnly? date) => date is not null && date.Value >= from && date.Value <= to;
        ReconciliationItem WithDecision(ReconciliationItem item)
        {
            if (item.Kind == DifferenceKind.Same || !decisions.TryGetValue(prefix + item.Key, out var decision)) return item;
            return item with
            {
                Kept = true,
                KeptBy = decision.DecidedBy,
                KeptAt = DateTime.SpecifyKind(decision.DecidedAt, DateTimeKind.Utc),
            };
        }
        return new ReconciliationWeekDto
        {
            Upload = Summary(upload),
            CoveredTeamIds = upload.TeamIds,
            CoveredFrom = upload.From,
            Items = result.Items
                .Where(i => InWeek(i.App?.Date) || InWeek(i.Source?.Date))
                .Select(WithDecision)
                .ToList(),
        };
    }

    public async Task<ReconciliationApplyResultDto> ApplyAsync(string source, IReadOnlyDictionary<string, Resolution> choices,
        CancellationToken cancellationToken = default)
    {
        var (review, allDifferences) = await BuildAsync(source, cancellationToken);
        // Every current difference, accepted ones included, so an earlier "keep" can be changed (using the source's
        // version makes the difference go, and SaveDecisionsAsync drops the old decision).
        var byId = allDifferences.GroupBy(i => i.Id).ToDictionary(g => g.Key, g => g.First());

        int updated = 0, deleted = 0, skipped = 0;
        var toAdd = new List<FixtureDto>();
        var keep = new List<ReconciliationItem>();
        var resolved = new List<ReconciliationItem>();      // differences settled with the source's version
        var toAddItems = new List<ReconciliationItem>();
        var errors = new List<string>();

        foreach (var (id, choice) in choices)
        {
            if (!byId.TryGetValue(id, out var item)) { skipped++; continue; }
            if (choice == Resolution.KeepApp && item.Kind is not (DifferenceKind.Same or DifferenceKind.NotLinked))
            {
                keep.Add(item);
                continue;
            }
            if (choice != Resolution.UseSource) continue;

            switch (item.Kind)
            {
                case DifferenceKind.OnlyInSource:
                    if (item.Source!.Date is null)
                    {
                        errors.Add($"{Describe(item)}: can't be added without a date.");
                    }
                    else
                    {
                        toAddItems.Add(item);
                        toAdd.Add(new FixtureDto
                        {
                            TeamId = item.Team!.Id,
                            Date = item.Source.Date.Value,
                            IsHome = item.Source.IsHome,
                            Opponent = item.Source.Opponent,
                            FixtureType = item.Source.FixtureType,
                        });
                    }
                    break;

                case DifferenceKind.OnlyInApp:
                    if (await fixtures.DeleteFixtureAsync(item.App!.Id, cancellationToken)) { deleted++; resolved.Add(item); }
                    else errors.Add($"{Describe(item)}: the fixture had already gone.");
                    break;

                case DifferenceKind.Different or DifferenceKind.DateMoved:
                    var app = item.App!;
                    var src = item.Source!;
                    // Its pitch was booked for the old date or as a home game: free it rather than leave it wrong.
                    if (item.AffectsAllocation) await fixtures.RemoveAllocationAsync(app.Id, cancellationToken);
                    var result = await fixtures.UpdateFixtureAsync(app.Id, new FixtureDto
                    {
                        Id = app.Id,
                        TeamId = app.TeamId,
                        Date = src.Date ?? app.Date,
                        IsHome = src.IsHome,
                        Opponent = src.Opponent,
                        FixtureType = src.FixtureType,
                    }, cancellationToken);
                    if (result.Succeeded) { updated++; resolved.Add(item); }
                    else errors.Add($"{Describe(item)}: {Reason(result)}.");
                    break;
            }
        }

        int added = 0;
        if (toAdd.Count > 0)
        {
            var created = await fixtures.CreateFixturesAsync(toAdd, cancellationToken);
            if (created.Succeeded) { added = toAdd.Count; resolved.AddRange(toAddItems); }
            else errors.Add($"The {toAdd.Count} new fixtures couldn't be added: {Reason(created)}.");
        }

        await SaveDecisionsAsync(source, keep, allDifferences.Except(resolved).ToList(), cancellationToken);

        return new ReconciliationApplyResultDto
        {
            Added = added, Updated = updated, Deleted = deleted, Kept = keep.Count, Skipped = skipped, Errors = errors,
        };
    }

    /// <summary>The review, plus every current difference (including ones kept earlier) for tidying decisions.</summary>
    private async Task<(ReconciliationReviewDto Review, List<ReconciliationItem> AllDifferences)> BuildAsync(string source, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
        var upload = await uploads.GetCombinedUploadAsync(source, ct);
        if (upload is null)
            return (new ReconciliationReviewDto { From = today, To = today }, []);

        var from = upload.From > today ? upload.From : today;
        var to = upload.To;
        var covered = (await teams.GetTeamsAsync(ct)).Where(t => upload.TeamIds.Contains(t.Id)).ToList();
        var appFixtures = await fixtures.GetFixturesAsync(
            new FixtureQuery { From = from, To = to == DateOnly.MaxValue ? null : to }, ct);

        var result = FixtureReconciler.Reconcile(covered, from, to, appFixtures, upload.Fixtures);
        var differences = result.Differences.ToList();

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var prefix = DecisionPrefix(source);
        var keptKeys = (await db.ReconciliationDecision.AsNoTracking()
                .Where(d => d.Key.StartsWith(prefix)).Select(d => d.Key).ToListAsync(ct))
            .ToHashSet();
        var open = differences.Where(i => !keptKeys.Contains(prefix + i.Key)).ToList();

        var review = new ReconciliationReviewDto
        {
            Upload = Summary(upload),
            Items = open,
            SameCount = result.Items.Count(i => i.Kind == DifferenceKind.Same),
            KeptCount = differences.Count - open.Count,
            Clashes = result.Clashes,
            From = from,
            To = to,
        };
        return (review, differences);
    }

    private async Task SaveDecisionsAsync(string source, List<ReconciliationItem> keep, List<ReconciliationItem> allDifferences, CancellationToken ct)
    {
        var prefix = DecisionPrefix(source);
        var current = allDifferences.Select(i => prefix + i.Key).ToHashSet();

        await using var db = await dbFactory.CreateDbContextAsync(ct);
        // Decisions about differences that no longer exist (fixed on either side since) are of no further use.
        var stale = await db.ReconciliationDecision.Where(d => d.Key.StartsWith(prefix)).ToListAsync(ct);
        db.ReconciliationDecision.RemoveRange(stale.Where(d => !current.Contains(d.Key)));

        var existing = stale.Select(d => d.Key).ToHashSet();
        var now = time.GetUtcNow().UtcDateTime;
        var by = httpContext.HttpContext?.User.Identity?.Name ?? "";
        foreach (var item in keep.DistinctBy(i => i.Key).Where(i => !existing.Contains(prefix + i.Key)))
        {
            db.ReconciliationDecision.Add(new ReconciliationDecision
            {
                Id = Guid.NewGuid(),
                Key = prefix + item.Key,
                TeamId = item.Team?.Id ?? Guid.Empty,
                Description = Describe(item),
                DecidedAt = now,
                DecidedBy = by,
            });
        }
        await db.SaveChangesAsync(ct);
    }

    private static string DecisionPrefix(string source) => source + "|";

    private static SourceUploadSummaryDto Summary(SourceUploadDto upload) => new()
    {
        Source = upload.Source,
        SourceName = upload.SourceName,
        UploadedAt = upload.UploadedAt,
        UploadedBy = upload.UploadedBy,
        TeamCount = upload.TeamIds.Count,
        FixtureCount = upload.Fixtures.Count,
    };

    private static string Reason(ServiceResult result) =>
        result.Message
        ?? (result.Errors.Count > 0 ? string.Join(" ", result.Errors.SelectMany(e => e.Value)) : null)
        ?? (result.Status == ServiceStatus.NotFound ? "the fixture had already gone" : result.Status.ToString());

    private static string Describe(ReconciliationItem item)
    {
        var date = item.Date?.ToString("dd/MM/yyyy") ?? "TBC";
        var team = item.Team?.DisplayName ?? item.Source?.SourceTeamName ?? "";
        var opponent = item.Source?.Opponent ?? item.App?.Opponent ?? "";
        return $"{date} {team} v {opponent} – {item.Kind.DisplayName()}";
    }
}
