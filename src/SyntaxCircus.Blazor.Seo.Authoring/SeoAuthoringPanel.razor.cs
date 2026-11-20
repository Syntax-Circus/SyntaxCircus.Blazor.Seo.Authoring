using Microsoft.AspNetCore.Components;

namespace SyntaxCircus.Blazor.Seo.Authoring;

/// <summary>Search snippet preview and "things to review" list for a <see cref="SeoAuthoringPanelViewModel"/>.</summary>
public partial class SeoAuthoringPanel
{
    private const int InitialCount = 5;
    private bool _showSearch;
    private bool _showWriting;
    private string? _snapshot;

    [Parameter] public SeoAuthoringPanelViewModel? View { get; set; }
    [Parameter] public bool CanCheck { get; set; }
    [Parameter] public EventCallback CheckAgain { get; set; }
    [Parameter] public EventCallback<SeoEditTarget> Edit { get; set; }

    private IEnumerable<SeoSuggestionViewModel> SearchItems => View?.Search.Take(_showSearch ? int.MaxValue : InitialCount) ?? [];

    private IEnumerable<SeoSuggestionViewModel> WritingItems => View?.Writing.Take(_showWriting ? int.MaxValue : InitialCount) ?? [];

    protected override void OnParametersSet()
    {
        if (_snapshot != View?.SnapshotId)
        {
            _snapshot = View?.SnapshotId;
            _showSearch = _showWriting = false;
        }
    }

    private void ShowMoreSearch() => _showSearch = true;

    private void ShowMoreWriting() => _showWriting = true;

    private Task EditTarget(SeoEditTarget target) => Edit.InvokeAsync(target);
}
