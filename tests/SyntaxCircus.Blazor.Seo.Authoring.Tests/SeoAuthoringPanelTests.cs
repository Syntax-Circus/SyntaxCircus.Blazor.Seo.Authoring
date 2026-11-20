using System.Collections.Immutable;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using SyntaxCircus.Seo.Analysis;
using Xunit;

namespace SyntaxCircus.Blazor.Seo.Authoring.Tests;

public sealed class SeoAuthoringPanelTests
{
    private static async Task<string> Render(SeoAuthoringPanelViewModel? view, bool canCheck = true)
    {
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () => (await renderer.RenderComponentAsync<SeoAuthoringPanel>(
            ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(SeoAuthoringPanel.View)] = view,
                [nameof(SeoAuthoringPanel.CanCheck)] = canCheck,
            }))).ToHtmlString());
    }

    private static SeoAuthoringPanelViewModel View(string title = "Title", string? address = "https://example.test/blog/x",
        string description = "Safe description", string status = "Checks are ready.", ImmutableArray<SeoSuggestionViewModel>? search = null)
        => new("id", new SeoSnippetViewModel(title, address, description), search ?? [], [], [], status, false, false);

    [Fact]
    public async Task Panel_RendersSnippetTitleUrlDescription()
    {
        var html = await Render(View("A <script>title</script>", "https://example.test/blog/x", "Safe description"));

        html.ShouldNotContain("<script>");
        html.ShouldContain("A &lt;script&gt;title&lt;/script&gt;");
        html.ShouldContain("https://example.test/blog/x");
        html.ShouldContain("Safe description");
    }

    [Fact]
    public async Task Panel_DegradedCoverage_ShowsPartialNotice()
    {
        var html = await Render(View(status: "Basic checks are ready. Extra writing checks are temporarily unavailable."));

        html.ShouldContain("temporarily unavailable");
    }

    [Fact]
    public async Task Panel_NullView_RendersCheckButtonOnly()
    {
        var html = await Render(null);

        html.ShouldContain("Check again");
        html.ShouldContain("Ready to check the current draft.");
        html.ShouldNotContain("How this page may look in search");
        html.ShouldNotContain("Edit this");
    }

    [Fact]
    public async Task Panel_CanCheckFalse_DisablesTheButton()
        => (await Render(null, false)).ShouldContain("disabled");

    [Fact]
    public async Task Panel_ShowsFirstFiveSuggestionsWithMoreButtonAndEditTargets()
    {
        var target = new SeoEditTarget("settings", null, "title");
        var items = Enumerable.Range(1, 6)
            .Select(i => new SeoSuggestionViewModel("r" + i, "Suggestion " + i, "Fix " + i, FindingSeverity.Warning, i == 1 ? target : null))
            .ToImmutableArray();

        var html = await Render(View(search: items));

        html.ShouldContain("Suggestion 5");
        html.ShouldNotContain("Suggestion 6");
        html.ShouldContain("Show more search suggestions");
        html.ShouldContain("Edit this");
    }
}
