using SyntaxCircus.Seo.Analysis;

namespace SyntaxCircus.Blazor.Seo.Authoring;

/// <summary>
/// Turns an authoring draft into the document the analyzer inspects. Implementations may throw
/// <see cref="ArgumentException"/> for a draft that cannot be projected; the draft is then reported as incomplete.
/// </summary>
public interface ISeoDraftDocumentBuilder
{
    SeoDocumentInput Build(SeoAuthoringDraft draft);
}
