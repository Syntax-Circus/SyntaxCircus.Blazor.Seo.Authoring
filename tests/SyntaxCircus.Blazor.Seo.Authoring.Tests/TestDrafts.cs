using System.Collections.Immutable;
using SyntaxCircus.Seo.Analysis;

namespace SyntaxCircus.Blazor.Seo.Authoring.Tests;

internal static class TestDrafts
{
    public static SeoAuthoringDraft Draft(ContentPurpose purpose = ContentPurpose.Editorial) => new(
        new SeoAuthoringIdentity("post-1", 1, 1), "Current title", null, "Current description", "current-title",
        new Uri("https://example.test/blog/current-title"), "<p>Some words to read.</p>", "en_US", purpose, "Example site", true,
        ImmutableDictionary<string, SeoEditTarget>.Empty
            .Add("title", new SeoEditTarget("settings", null, "title"))
            .Add("metaDescription", new SeoEditTarget("settings", null, "metaDescription")));

    public static SeoAuthoringPresentation Presentation(ISeoCompanion? companion)
        => new(new DefaultSeoDraftDocumentBuilder(), new ContentSeoAnalyzer(), companion);
}
