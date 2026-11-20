using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using SyntaxCircus.Seo.Analysis;

namespace SyntaxCircus.Blazor.Seo.Authoring;

public sealed class SeoAuthoringPresentation(ISeoDraftDocumentBuilder builder, IContentSeoAnalyzer analyzer, ISeoCompanion? companion,
    Func<CancellationToken, Task<ISeoCompanion?>>? selectCompanion = null) : ISeoAuthoringPresentation
{
    /// <summary>Largest <see cref="SeoAuthoringDraft.BodyHtml"/> (in characters) that is analyzed.</summary>
    public const int MaximumBodyCharacters = 500_000;

    private const string TitleField = "title";
    private const string MetaDescriptionField = "metaDescription";

    private readonly ConditionalWeakTable<SeoPreparedDraft, Task<ISeoCompanion?>> _selections = new();

    public async Task<SeoPreparedDraft> PrepareAsync(SeoAuthoringDraft draft, CancellationToken token)
    {
        var prepared = Prepare(draft, token);
        await SelectionAsync(prepared, token);
        return prepared;
    }

    public async Task<SeoAuthoringPanelViewModel> BaselineAsync(SeoPreparedDraft draft, CancellationToken token)
    {
        if (draft.Snapshot is not { } snapshot)
        {
            return Incomplete(draft);
        }

        var selected = await SelectionAsync(draft, token);
        return Map(draft, await analyzer.AnalyzeAsync(snapshot, cancellationToken: token), selected is not null, selected is not null);
    }

    public async Task<SeoAuthoringPanelViewModel> CompleteAsync(SeoPreparedDraft draft, CancellationToken token)
    {
        if (draft.Snapshot is not { } snapshot)
        {
            return Incomplete(draft);
        }

        var selected = await SelectionAsync(draft, token);
        return Map(draft, await analyzer.AnalyzeAsync(snapshot, selected, token), false, selected is not null);
    }

    private SeoPreparedDraft Prepare(SeoAuthoringDraft draft, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var searchTitle = DefaultSeoDraftDocumentBuilder.SearchTitle(draft);
        var title = string.IsNullOrWhiteSpace(draft.SiteDisplayName) ? searchTitle : searchTitle + " · " + draft.SiteDisplayName;
        var snippet = new SeoSnippetViewModel(title, draft.CanonicalUrl.ToString(), draft.MetaDescription ?? "");
        SeoPreparedDraft Unfinished() => new(draft.Identity, null, snippet, draft.Targets,
            "Finish the incomplete text before checking this draft.");

        if (!draft.InputValid || draft.BodyHtml.Length > MaximumBodyCharacters)
        {
            return Unfinished();
        }

        try
        {
            return new SeoPreparedDraft(draft.Identity, analyzer.Prepare(builder.Build(draft), token), snippet, draft.Targets, null);
        }
        catch (ArgumentException)
        {
            return Unfinished();
        }
    }

    private Task<ISeoCompanion?> SelectionAsync(SeoPreparedDraft draft, CancellationToken token)
        => _selections.GetValue(draft, _ => draft.Snapshot is { } snapshot && Eligible(snapshot)
            ? selectCompanion?.Invoke(token) ?? Task.FromResult(companion)
            : Task.FromResult<ISeoCompanion?>(null));

    private static bool Eligible(SeoSnapshot snapshot) => snapshot.Purpose == ContentPurpose.Editorial &&
        (snapshot.Locale.Equals("en", StringComparison.OrdinalIgnoreCase) ||
         snapshot.Locale.StartsWith("en_", StringComparison.OrdinalIgnoreCase) ||
         snapshot.Locale.StartsWith("en-", StringComparison.OrdinalIgnoreCase));

    private static SeoAuthoringPanelViewModel Incomplete(SeoPreparedDraft draft)
        => new(null, draft.Snippet, [], [], [], "This draft is incomplete. " + draft.IncompleteReason, false, true);

    private static SeoAuthoringPanelViewModel Map(SeoPreparedDraft draft, SeoAnalysisReport report, bool pending, bool extrasEnabled)
    {
        var search = ImmutableArray.CreateBuilder<SeoSuggestionViewModel>();
        var writing = ImmutableArray.CreateBuilder<SeoSuggestionViewModel>();
        var diagnostics = ImmutableArray.CreateBuilder<string>();
        foreach (var finding in report.Findings.OrderByDescending(f => f.Severity))
        {
            if (finding.Severity == FindingSeverity.Information)
            {
                diagnostics.Add(finding.Message);
                continue;
            }

            var targetKey = finding.SemanticKey switch
            {
                "metadata.title" => TitleField,
                "metadata.description" => MetaDescriptionField,
                _ => finding.SectionId,
            };
            SeoEditTarget? target = targetKey is not null && draft.Targets.TryGetValue(targetKey, out var owned) ? owned : null;
            var (message, recommendation) = PlainWritingCopy(finding);
            var item = new SeoSuggestionViewModel(finding.RuleId, message, recommendation, finding.Severity, target);
            (finding.Category == FindingCategory.Readability ? writing : search).Add(item);
        }

        foreach (var coverage in report.Coverage)
        {
            diagnostics.Add(coverage.Provider + ": " + coverage.State);
        }

        var degraded = report.Coverage.Any(c => c.State is CoverageState.Unavailable or CoverageState.Failed or CoverageState.TimedOut or CoverageState.Stale);
        var partial = report.Coverage.Any(c => c.State == CoverageState.Partial);
        var unsupported = report.Coverage.Any(c => c.State == CoverageState.UnsupportedLanguage)
            || draft.Snapshot is { Purpose: ContentPurpose.Editorial } snapshot && !Eligible(snapshot);
        var status = pending ? "Basic checks are ready. Checking extra writing suggestions…"
            : degraded ? "Basic checks are ready. Extra writing checks are temporarily unavailable."
            : partial ? "Basic checks are ready. Some extra writing checks could not be completed. Review the available suggestions in context."
            : unsupported ? "Basic checks are ready. Extra writing checks support English articles only."
            : draft.Snapshot?.Purpose != ContentPurpose.Editorial ? "Basic checks are ready. Extra writing checks are for articles."
            : !extrasEnabled ? "Basic checks are ready. Extra writing checks are turned off."
            : "Checks are ready. Review the suggestions in context.";
        return new SeoAuthoringPanelViewModel(report.SnapshotId, draft.Snippet, search.ToImmutable(), writing.ToImmutable(),
            diagnostics.ToImmutable(), status, pending, false);
    }

    private static (string Message, string Recommendation) PlainWritingCopy(SeoFinding finding) => finding.Source is "power-seo" or "yoast"
        ? finding.SemanticKey switch
        {
            "writing.word-complexity" => ("Some wording may be hard to follow.", "Consider familiar alternatives for long or unusual words. Keep useful names and terms."),
            "writing.transition-words" => ("Some ideas may need a clearer connection.", "Read the article aloud. Add a connecting word only where it helps the reader."),
            "writing.subheading-distribution" => ("Some parts may be easier to follow with headings.", "Break up a long passage where the topic changes."),
            "writing.passive-voice" => ("Some sentences may be clearer with an active voice.", "Make it clear who is doing the action, where that helps the reader."),
            "writing.sentence-beginnings" => ("Several sentences begin the same way.", "Try varying a few openings if the repetition makes the article harder to read."),
            "phrase.length" => ("Check that the topic phrase describes this article clearly.", "Use natural wording that matches what the article helps the reader do."),
            "phrase.subheadings" => ("Check that the headings describe the article's topics.", "Use the topic phrase in a heading only where it helps the reader."),
            _ => (finding.Message, finding.Recommendation),
        }
        : (finding.Message, finding.Recommendation);
}
