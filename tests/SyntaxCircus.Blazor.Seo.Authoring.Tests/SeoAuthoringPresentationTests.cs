using Shouldly;
using SyntaxCircus.Seo.Analysis;
using Xunit;

namespace SyntaxCircus.Blazor.Seo.Authoring.Tests;

public sealed class SeoAuthoringPresentationTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Presentation_NonEnglishLocale_IsNotEligible_ShowsStatusNotFindings()
    {
        var companion = new Companion();
        var service = TestDrafts.Presentation(companion);
        var prepared = await service.PrepareAsync(TestDrafts.Draft() with { Locale = "fr_FR" }, Token);

        var report = await service.CompleteAsync(prepared, Token);

        companion.Called.ShouldBeFalse();
        report.ExtraPending.ShouldBeFalse();
        report.Writing.ShouldBeEmpty();
        report.Status.ShouldContain("English");
    }

    [Fact]
    public async Task Presentation_MapsFindingToEditTarget()
    {
        var service = TestDrafts.Presentation(new Companion());
        var prepared = await service.PrepareAsync(TestDrafts.Draft() with { MetaDescription = null }, Token);

        var report = await service.BaselineAsync(prepared, Token);

        var finding = report.Search.Where(x => x.Target is { Field: "metaDescription" }).ShouldHaveSingleItem();
        finding.Target.ShouldBe(prepared.Targets["metaDescription"]);
    }

    [Fact]
    public async Task Presentation_MapsSectionFindingsToTheirSectionTarget()
    {
        var section = new SeoEditTarget("content", "intro", "body");
        var draft = TestDrafts.Draft() with
        {
            BodyHtml = "<section data-section-id=\"intro\"><h3>Skipped a level</h3></section>",
            Targets = TestDrafts.Draft().Targets.Add("intro", section),
        };
        var service = TestDrafts.Presentation(new Companion());

        var report = await service.BaselineAsync(await service.PrepareAsync(draft, Token), Token);

        report.Search.ShouldContain(x => x.Id == "core.heading-order" && x.Target == section);
    }

    [Fact]
    public async Task Presentation_SnippetAddress_KeepsPercentEncodedPathEncoded()
    {
        var draft = TestDrafts.Draft() with { CanonicalUrl = new Uri("https://example.test/a%20b") };

        var prepared = await TestDrafts.Presentation(null).PrepareAsync(draft, Token);

        prepared.Snippet.Address.ShouldBe("https://example.test/a%20b");
    }

    [Theory]
    [InlineData("title")]
    [InlineData("metaDescription")]
    public async Task Presentation_ReservedKeys_WinOverSectionIdsOfTheSameName(string reserved)
    {
        var draft = TestDrafts.Draft() with
        {
            MetaDescription = null,
            BodyHtml = $"<section data-section-id=\"{reserved}\"><h3>Skipped a level</h3></section>",
        };
        var service = TestDrafts.Presentation(null);

        var report = await service.BaselineAsync(await service.PrepareAsync(draft, Token), Token);

        // The metadata finding still jumps to the reserved field...
        report.Search.Where(x => x.Target is { Field: "metaDescription" }).ShouldHaveSingleItem();
        // ...and a heading problem inside a body section named like a reserved key never jumps to that field.
        report.Search.Single(x => x.Id == "core.heading-order").Target.ShouldBeNull();
    }

    [Fact]
    public async Task Presentation_MapsPowerSeoAndYoastWritingFindingsToPlainEnglish()
    {
        var power = new Companion();
        var service = TestDrafts.Presentation(power);
        var prepared = await service.PrepareAsync(TestDrafts.Draft(), Token);
        power.Reply.SetResult(Result(prepared, "power-seo", FindingCategory.Readability, "word-complexity", "writing.word-complexity",
            "Flesch-Kincaid Grade Level: 8.8. 50% complex."));
        var writing = (await service.CompleteAsync(prepared, Token)).Writing.ShouldHaveSingleItem();
        writing.Message.ShouldBe("Some wording may be hard to follow.");
        writing.Recommendation.ShouldBe("Consider familiar alternatives for long or unusual words. Keep useful names and terms.");
        writing.Target.ShouldBeNull();

        var yoast = new Companion();
        service = TestDrafts.Presentation(yoast);
        prepared = await service.PrepareAsync(TestDrafts.Draft(), Token);
        yoast.Reply.SetResult(Result(prepared, "yoast", FindingCategory.FocusPhrase, "focus", "phrase.length",
            "<a>More than the recommended maximum of four words. Make it shorter!</a>"));
        var suggestion = (await service.CompleteAsync(prepared, Token)).Search.Where(x => x.Id.StartsWith("yoast.", StringComparison.Ordinal)).ShouldHaveSingleItem();
        suggestion.Message.ShouldBe("Check that the topic phrase describes this article clearly.");
        suggestion.Recommendation.ShouldBe("Use natural wording that matches what the article helps the reader do.");
        suggestion.Target.ShouldBeNull();
    }

    [Fact]
    public async Task Presentation_UnknownWritingKey_KeepsVendorCopy()
    {
        var companion = new Companion();
        var service = TestDrafts.Presentation(companion);
        var prepared = await service.PrepareAsync(TestDrafts.Draft(), Token);
        companion.Reply.SetResult(Result(prepared, "power-seo", FindingCategory.Readability, "x", "writing.future-rule", "Vendor message"));

        var finding = (await service.CompleteAsync(prepared, Token)).Writing.ShouldHaveSingleItem();

        finding.Message.ShouldBe("Vendor message");
        finding.Target.ShouldBeNull();
    }

    [Fact]
    public async Task Presentation_BaselinePublishesBeforeCompanionAndRetainsSameSnapshot()
    {
        var companion = new Companion();
        var service = TestDrafts.Presentation(companion);
        var prepared = await service.PrepareAsync(TestDrafts.Draft() with { MetaDescription = null }, Token);

        var baseline = await service.BaselineAsync(prepared, Token);

        companion.Called.ShouldBeFalse();
        baseline.ExtraPending.ShouldBeTrue();
        baseline.Search.ShouldContain(x => x.Target != null && x.Target.Field == "metaDescription");
        var pending = service.CompleteAsync(prepared, Token);
        companion.Called.ShouldBeTrue();
        pending.IsCompleted.ShouldBeFalse();
        companion.Reply.SetResult(Result(prepared, "power-seo", FindingCategory.Readability, "transition-words", "writing.transition-words", "Review transitions"));
        var result = await pending;
        result.SnapshotId.ShouldBe(baseline.SnapshotId);
        foreach (var item in baseline.Search) result.Search.ShouldContain(item);
        result.Writing.ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData(CoverageState.Unavailable)]
    [InlineData(CoverageState.TimedOut)]
    [InlineData(CoverageState.Stale)]
    [InlineData(CoverageState.Failed)]
    public async Task Presentation_DegradedCompanion_NeverRemovesBaselineOrLooksClean(CoverageState state)
    {
        var companion = new Companion();
        var service = TestDrafts.Presentation(companion);
        var prepared = await service.PrepareAsync(TestDrafts.Draft() with { MetaDescription = null }, Token);
        companion.Reply.SetResult(new CompanionResult(prepared.Snapshot!.Id, "power-seo", "1", state, []));

        var report = await service.CompleteAsync(prepared, Token);

        report.Search.ShouldContain(x => x.Target != null && x.Target.Field == "metaDescription");
        report.Status.ShouldContain("unavailable", Case.Insensitive);
        report.ExtraPending.ShouldBeFalse();
    }

    [Fact]
    public async Task Presentation_GeneralContent_NeverRequestsAnEditorialWorker()
    {
        var companion = new Companion();
        var service = TestDrafts.Presentation(companion);
        var prepared = await service.PrepareAsync(TestDrafts.Draft(ContentPurpose.General), Token);

        var report = await service.CompleteAsync(prepared, Token);

        companion.Called.ShouldBeFalse();
        report.ExtraPending.ShouldBeFalse();
    }

    [Fact]
    public async Task Presentation_InvalidDraft_HasNoCleanSnapshot()
    {
        var service = TestDrafts.Presentation(new Companion());
        var prepared = await service.PrepareAsync(TestDrafts.Draft() with { InputValid = false }, Token);

        var report = await service.BaselineAsync(prepared, Token);

        report.SnapshotId.ShouldBeNull();
        report.Status.ShouldContain("incomplete", Case.Insensitive);
    }

    [Fact]
    public async Task Presentation_OversizedBody_HasNoCleanSnapshot()
    {
        var service = TestDrafts.Presentation(new Companion());

        var prepared = await service.PrepareAsync(TestDrafts.Draft() with { BodyHtml = "<p>" + new string('x', 500_001) + "</p>" }, Token);

        prepared.Snapshot.ShouldBeNull();
        prepared.IncompleteReason.ShouldNotBeNull();
    }

    [Fact]
    public async Task Presentation_BuilderRejectingTheDraft_IsIncomplete()
    {
        var service = new SeoAuthoringPresentation(new ThrowingBuilder(), new ContentSeoAnalyzer(), null);

        (await service.PrepareAsync(TestDrafts.Draft(), Token)).Snapshot.ShouldBeNull();
    }

    [Fact]
    public async Task Presentation_SnippetUsesSearchTitleSiteNameAndCanonicalUrl()
    {
        var service = TestDrafts.Presentation(null);

        var snippet = (await service.PrepareAsync(TestDrafts.Draft() with { SeoTitle = "Search title" }, Token)).Snippet;

        snippet.Title.ShouldBe("Search title · Example site");
        snippet.Address.ShouldBe("https://example.test/blog/current-title");
        snippet.Description.ShouldBe("Current description");
    }

    private static CompanionResult Result(SeoPreparedDraft prepared, string provider, FindingCategory category, string id, string key, string message)
        => new(prepared.Snapshot!.Id, provider, "1", CoverageState.Complete,
            [new CompanionAssessment(id, key, category, FindingSeverity.Warning, message, "Vendor recommendation")]);

    private sealed class ThrowingBuilder : ISeoDraftDocumentBuilder
    {
        public SeoDocumentInput Build(SeoAuthoringDraft draft) => throw new ArgumentException("nope");
    }

    private sealed class Companion : ISeoCompanion
    {
        public bool Called;
        public TaskCompletionSource<CompanionResult> Reply = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<CompanionResult> AnalyzeAsync(SeoSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            Called = true;
            return Reply.Task.WaitAsync(cancellationToken);
        }
    }
}
