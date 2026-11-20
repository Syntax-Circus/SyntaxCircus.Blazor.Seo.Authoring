namespace SyntaxCircus.Blazor.Seo.Authoring;

/// <summary>
/// Optional runtime override of <see cref="SeoAuthoringOptions.Provider"/>, for example from saved site settings.
/// Called once per analysis. Returning <see langword="null"/> (selection unknown) means <see cref="SeoAuthoringProvider.CoreOnly"/>,
/// so draft text is never sent to an external service when the choice cannot be determined.
/// </summary>
public interface ISeoProviderSelector
{
    ValueTask<SeoAuthoringProvider?> SelectAsync(CancellationToken ct);
}
