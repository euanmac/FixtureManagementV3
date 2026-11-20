using Bunit;
using FixtureManagement.Shared.Reconciliation;
using FixtureManagement.UI.Pages.Fixtures;
using NSubstitute;

namespace FixtureManagement.Tests.UI;

public class ReviewPageInteractiveTests : InteractivePageTestBase
{
    private static readonly DateOnly Dec6 = new(2026, 12, 6);
    private readonly TeamDto lions = new() { Id = Guid.NewGuid(), Name = "Thame United Lions", AgeGroup = AgeGroup.U13, Gender = Gender.Male };

    private ReconciliationItem Moved(bool allocated = false) => new()
    {
        Kind = DifferenceKind.DateMoved, Team = lions, Changes = FixtureChanges.Date, Suggested = Resolution.UseSource,
        App = new FixtureDto
        {
            Id = Guid.NewGuid(), TeamId = lions.Id, Date = Dec6.AddDays(7), Opponent = "Rivals", IsHome = true, FixtureType = FixtureType.League,
            Allocation = allocated ? new FixtureAllocationDto { PitchId = Guid.NewGuid(), Start = new TimeOnly(10, 0), Duration = TimeSpan.FromHours(1) } : null,
        },
        Source = new SourceFixture { TeamId = lions.Id, Date = Dec6, Opponent = "Rivals", IsHome = true, FixtureType = FixtureType.League },
    };

    private ReconciliationItem NewOnFullTime() => new()
    {
        Kind = DifferenceKind.OnlyInSource, Team = lions, Suggested = Resolution.UseSource,
        Source = new SourceFixture { TeamId = lions.Id, Date = Dec6.AddDays(14), Opponent = "Newcomers", IsHome = false, FixtureType = FixtureType.Cup },
    };

    private void Returns(params ReconciliationItem[] items) =>
        Reconciliation.GetReviewAsync("FullTime", Arg.Any<CancellationToken>()).Returns(new ReconciliationReviewDto
        {
            Upload = new SourceUploadSummaryDto { Source = "FullTime", UploadedAt = new DateTime(2026, 11, 20, 9, 0, 0, DateTimeKind.Utc), UploadedBy = "me@example.com", TeamCount = 36, FixtureCount = 312 },
            Items = items, SameCount = 250, KeptCount = 2, From = new DateOnly(2026, 11, 20), To = DateOnly.MaxValue,
        });

    [Fact]
    public void Shows_each_difference_with_the_suggested_choice_selected()
    {
        var moved = Moved(allocated: true);
        Returns(moved, NewOnFullTime());

        var page = Render<Review>();

        Assert.Contains("250 match", page.Markup);
        Assert.Contains("2 to review", page.Markup);
        Assert.Contains("Brought forward", page.Markup);
        Assert.Contains("removes its pitch allocation", page.Markup);
        Assert.True(page.Find($"#use-{moved.Id}").HasAttribute("checked"));
        Assert.False(page.Find($"#keep-{moved.Id}").HasAttribute("checked"));
        Assert.Contains("Add it", page.Markup);
        Assert.Contains("Don't add", page.Markup);
        Assert.Equal($"Choices[{moved.Id}]", page.Find($"#use-{moved.Id}").GetAttribute("name"));   // what the static site posts
    }

    [Fact]
    public void Apply_sends_the_clicked_choices()
    {
        var moved = Moved();
        var added = NewOnFullTime();
        Returns(moved, added);
        Reconciliation.ApplyAsync("FullTime", Arg.Any<IReadOnlyDictionary<string, Resolution>>(), Arg.Any<CancellationToken>())
            .Returns(new ReconciliationApplyResultDto { Updated = 1, Kept = 1 });
        var page = Render<Review>();

        page.Find($"#keep-{added.Id}").Change(true);
        page.Find("form").Submit();

        Reconciliation.Received(1).ApplyAsync("FullTime",
            Arg.Is<IReadOnlyDictionary<string, Resolution>>(c => c.Count == 2 && c[moved.Id] == Resolution.UseSource && c[added.Id] == Resolution.KeepApp),
            Arg.Any<CancellationToken>());
        Assert.Contains("updated 1", page.Markup);
        Assert.Contains("kept as they are 1", page.Markup);
    }

    [Fact]
    public void Says_when_nothing_has_been_fetched_yet()
    {
        Reconciliation.GetReviewAsync("FullTime", Arg.Any<CancellationToken>()).Returns(new ReconciliationReviewDto());

        Assert.Contains("Nothing has been fetched from FullTime yet", Render<Review>().Markup);
    }
}
