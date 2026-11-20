using System.Collections.Immutable;
using SyntaxCircus.Seo.Analysis;

namespace SyntaxCircus.Blazor.Seo.Authoring;

/// <summary>Identifies the record being edited and which edit/authority generation the draft belongs to.</summary>
public sealed record SeoAuthoringIdentity(string RecordId, long AuthorityGeneration, long InputGeneration);

/// <summary>
/// The content being authored, independent of any CMS model.
/// <paramref name="Targets"/> maps finding field ids to where "Edit this" jumps: <c>title</c> and
/// <c>metaDescription</c> for the search title and description findings, and a section id (as emitted in
/// <c>data-section-id</c> attributes of <paramref name="BodyHtml"/>) for findings located in a section.
/// </summary>
public sealed record SeoAuthoringDraft(SeoAuthoringIdentity Identity, string Title, string? SeoTitle, string? MetaDescription,
    string Slug, Uri CanonicalUrl, string BodyHtml, string Locale, ContentPurpose Purpose, string SiteDisplayName,
    bool InputValid, ImmutableDictionary<string, SeoEditTarget> Targets);

public sealed record SeoEditTarget(string Panel, string? SectionId, string Field);

public sealed record SeoSnippetViewModel(string Title, string? Address, string Description);

public sealed record SeoPreparedDraft(SeoAuthoringIdentity Identity, SeoSnapshot? Snapshot,
    SeoSnippetViewModel Snippet, ImmutableDictionary<string, SeoEditTarget> Targets, string? IncompleteReason);

public sealed record SeoSuggestionViewModel(string Id, string Message, string Recommendation,
    FindingSeverity Severity, SeoEditTarget? Target);

public sealed record SeoAuthoringPanelViewModel(string? SnapshotId, SeoSnippetViewModel Snippet,
    ImmutableArray<SeoSuggestionViewModel> Search, ImmutableArray<SeoSuggestionViewModel> Writing,
    ImmutableArray<string> Diagnostics, string Status, bool ExtraPending, bool Outdated);
