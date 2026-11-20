namespace SyntaxCircus.Blazor.Seo.Authoring;

public interface ISeoAuthoringPresentation
{
    Task<SeoPreparedDraft> PrepareAsync(SeoAuthoringDraft draft, CancellationToken token);
    Task<SeoAuthoringPanelViewModel> BaselineAsync(SeoPreparedDraft draft, CancellationToken token);
    Task<SeoAuthoringPanelViewModel> CompleteAsync(SeoPreparedDraft draft, CancellationToken token);
}
