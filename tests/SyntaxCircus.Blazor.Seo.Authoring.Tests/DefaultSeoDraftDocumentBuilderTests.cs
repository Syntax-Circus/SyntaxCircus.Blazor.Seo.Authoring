using Shouldly;
using SyntaxCircus.Seo.Analysis;
using Xunit;

namespace SyntaxCircus.Blazor.Seo.Authoring.Tests;

public sealed class DefaultSeoDraftDocumentBuilderTests
{
    private readonly DefaultSeoDraftDocumentBuilder _builder = new();

    [Fact]
    public void DefaultBuilder_UsesSeoTitleFallbackToTitle()
    {
        var draft = TestDrafts.Draft();

        _builder.Build(draft with { SeoTitle = "Search title" }).Title.ShouldBe("Search title");
        _builder.Build(draft with { SeoTitle = null }).Title.ShouldBe("Current title");
        _builder.Build(draft with { SeoTitle = "  " }).Title.ShouldBe("Current title");
    }

    [Fact]
    public void DefaultBuilder_MapsDraftFieldsToTheDocument()
    {
        var document = _builder.Build(TestDrafts.Draft());

        document.MetaDescription.ShouldBe("Current description");
        document.Slug.ShouldBe("current-title");
        document.CanonicalUrl.ShouldBe("https://example.test/blog/current-title");
        document.Locale.ShouldBe("en_US");
        document.Purpose.ShouldBe(ContentPurpose.Editorial);
        _builder.Build(TestDrafts.Draft() with { MetaDescription = null }).MetaDescription.ShouldBe("");
    }

    [Fact]
    public void DefaultBuilder_EmitsEncodedTitleAsLeadingH1()
    {
        var document = _builder.Build(TestDrafts.Draft() with { Title = "A <b> title" });

        document.ContentHtml.ShouldStartWith("<h1>A &lt;b&gt; title</h1>");
        document.ContentHtml.ShouldEndWith("<p>Some words to read.</p>");
    }

    [Fact]
    public void DefaultBuilder_DoesNotDuplicateAnH1TheBodyAlreadyHas()
    {
        var document = _builder.Build(TestDrafts.Draft() with { BodyHtml = "<H1>Own heading</H1><p>x</p>" });

        document.ContentHtml.ShouldBe("<H1>Own heading</H1><p>x</p>");
    }
}
