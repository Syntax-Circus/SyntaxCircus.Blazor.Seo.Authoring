using System.Text.Encodings.Web;
using SyntaxCircus.Seo.Analysis;

namespace SyntaxCircus.Blazor.Seo.Authoring;

/// <summary>
/// Builds the analyzed document from the draft fields and <see cref="SeoAuthoringDraft.BodyHtml"/>. The page title
/// is emitted as the leading <c>h1</c> (as a typical article page does) unless the body already supplies one.
/// </summary>
public sealed class DefaultSeoDraftDocumentBuilder : ISeoDraftDocumentBuilder
{
    public SeoDocumentInput Build(SeoAuthoringDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var html = draft.BodyHtml.Contains("<h1", StringComparison.OrdinalIgnoreCase)
            ? draft.BodyHtml
            : "<h1>" + HtmlEncoder.Default.Encode(draft.Title) + "</h1>" + draft.BodyHtml;
        return new SeoDocumentInput(html, SearchTitle(draft), draft.MetaDescription ?? "", CanonicalUrlText(draft) ?? "",
            draft.Slug, Locale: draft.Locale, Purpose: draft.Purpose);
    }

    /// <summary>A missing canonical URL yields <c>null</c>. Absolute URIs keep their escaped form (<c>/a%20b</c>), unlike <see cref="Uri.ToString"/>.</summary>
    internal static string? CanonicalUrlText(SeoAuthoringDraft draft)
        => draft.CanonicalUrl is null ? null : draft.CanonicalUrl.IsAbsoluteUri ? draft.CanonicalUrl.AbsoluteUri : draft.CanonicalUrl.OriginalString;

    internal static string SearchTitle(SeoAuthoringDraft draft)
        => string.IsNullOrWhiteSpace(draft.SeoTitle) ? draft.Title : draft.SeoTitle;
}
