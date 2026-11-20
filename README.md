# SyntaxCircus.Blazor.Seo.Authoring

[![Build](https://github.com/Syntax-Circus/SyntaxCircus.Blazor.Seo.Authoring/actions/workflows/build.yml/badge.svg)](https://github.com/Syntax-Circus/SyntaxCircus.Blazor.Seo.Authoring/actions/workflows/build.yml)
[![NuGet](https://img.shields.io/nuget/v/SyntaxCircus.Blazor.Seo.Authoring.svg)](https://www.nuget.org/packages/SyntaxCircus.Blazor.Seo.Authoring)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

Live SEO and readability checks while writing a page, with "edit this" jump targets. Lifted from a production
site's authoring flow into a reusable package that is independent of any particular CMS model.

## Registration

```csharp
builder.Services.AddSeoAuthoring(o =>
{
    o.Provider = SeoAuthoringProvider.PowerSeo;   // PowerSeo (default), Yoast or CoreOnly
    o.YoastBaseUrl = new Uri("http://seo-yoast:8080"); // only needed for Yoast
});

// or bind from configuration (keys: Provider, YoastBaseUrl)
builder.Services.AddSeoAuthoring(builder.Configuration.GetSection("SeoAuthoring"));
```

This registers the analyzer, the PowerSeo companion (which needs `IJSRuntime`, so use it in Blazor Server or
WebAssembly hosts), a Yoast companion when `YoastBaseUrl` is a valid origin (absolute HTTP(S), no credentials,
query or fragment), `DefaultSeoDraftDocumentBuilder`, and a scoped `ISeoAuthoringPresentation`. `Provider = Yoast`
without a valid `YoastBaseUrl` behaves as `CoreOnly`.

Optionally register an `ISeoProviderSelector` to choose the provider per analysis at runtime (for example from saved
site settings). It overrides `Provider`; returning `null` (or throwing) means `CoreOnly`, so draft text is never sent to
an external service when the choice is unknown.

## Concepts

- `SeoAuthoringDraft`: the generic draft you hand over (title, SEO title/description, slug, canonical URL,
  `BodyHtml`, locale, purpose, site name, and a `Targets` map from finding field ids to `SeoEditTarget`s).
- `ISeoDraftDocumentBuilder`: turns a draft into the analyzer's `SeoDocumentInput`.
  `DefaultSeoDraftDocumentBuilder` uses the draft fields and `BodyHtml`; replace it if your content needs a
  custom projection.
- `SeoAuthoringPresentation`: prepares the draft, runs the baseline analysis, then the optional PowerSeo/Yoast
  companion, and maps findings to plain-English suggestions.
- `SeoAuthoringSession`: per-editor state. Debounces checks by 750 ms, cancels superseded checks and marks
  results outdated when the draft changes.
- `SeoAuthoringPanel`: the Blazor component that renders a `SeoAuthoringPanelViewModel` (search snippet preview and
  suggestions). Styles are scoped to the component and themed with CSS variables (see below).

### Edit-target keys

`SeoAuthoringDraft.Targets` is keyed by finding location:

- `title` and `metaDescription` are **reserved**: they receive the search-title and meta-description findings.
- Any other key is a section id, matched against `data-section-id` attributes in `BodyHtml`.

A section whose `data-section-id` equals a reserved key never claims that key: reserved keys always win, and a
finding located in such a section gets no edit target.

## Implementing `ISeoDraftDocumentBuilder` for your app

Most apps only need to build a `SeoAuthoringDraft`; implement the builder when the analyzed HTML should differ from
`BodyHtml` (for example when your body is stored as Markdown or blocks and you want to analyze the public render):

```csharp
public sealed class MyDraftBuilder(IMyRenderer renderer) : ISeoDraftDocumentBuilder
{
    public SeoDocumentInput Build(SeoAuthoringDraft draft) => new(
        renderer.RenderPublicHtml(draft.BodyHtml), draft.SeoTitle ?? draft.Title, draft.MetaDescription ?? "",
        draft.CanonicalUrl?.AbsoluteUri ?? "", draft.Slug, Locale: draft.Locale, Purpose: draft.Purpose);
}

services.AddSingleton<ISeoDraftDocumentBuilder, MyDraftBuilder>(); // before AddSeoAuthoring, or replace after
```

`CanonicalUrl` is optional: when `null` the snippet shows no address and the analyzer receives no canonical URL.
Use `CanonicalUrl.AbsoluteUri` (not `ToString()`) so percent-encoded paths stay encoded.

## Wiring a session into an editor

```razor
<SeoAuthoringPanel View="_session.View" CanCheck="@_canCheck" CheckAgain="CheckNow" Edit="JumpTo" />

@code {
    [Inject] ISeoAuthoringPresentation Presentation { get; set; } = default!;
    private SeoAuthoringSession _session = default!;

    protected override void OnInitialized()
    {
        _session = new SeoAuthoringSession(Presentation);
        _session.Changed += () => _ = InvokeAsync(StateHasChanged);
    }

    // Debounced on typing; immediate when the editor asks.
    private Task OnDraftChanged() => _session.CheckAsync(BuildDraft(), immediate: false, CancellationToken.None);
    private Task CheckNow() => _session.CheckAsync(BuildDraft(), immediate: true, CancellationToken.None);
    private Task JumpTo(SeoEditTarget target) { /* focus target.Panel / target.SectionId / target.Field */ return Task.CompletedTask; }

    public ValueTask DisposeAsync() => _session.DisposeAsync();
}
```

Call `_session.Invalidate(false)` when the draft changes but you are not re-checking yet (results are marked
outdated), or `Invalidate(true)` to clear them.

## Theming

The panel is light by default and does not follow `prefers-color-scheme`, so it stays readable in a light-only admin.
To opt into a dark look, set these custom properties on the panel or any ancestor (for example your admin's dark-theme
selector):

```css
.admin-dark {
    --seo-panel-bg: #1b2230;
    --seo-panel-fg: #e6eaf2;
    --seo-panel-border: #3a4358;
    --seo-snippet-bg: #232c3f;
    --seo-snippet-fg: #e6eaf2;
    --seo-snippet-accent: #7da2ee;
}
```

Defaults: `--seo-panel-bg #fff`, `--seo-panel-fg #1d2433`, `--seo-panel-border #d8dfeb`, `--seo-snippet-bg #f4f7fc`,
`--seo-snippet-fg` (the panel foreground), `--seo-snippet-accent #2455b5`.

## Privacy

`PowerSeo` runs in the editor's browser through JS interop; no third-party service is involved. `Yoast` sends the draft text (title, description, body) to the service at
`YoastBaseUrl`. Point it only at a service you operate, and prefer an internal network address. Selection is
fail-closed: an unknown or failing `ISeoProviderSelector` result means no text leaves your process. Only editorial
English drafts are ever sent to a companion.

## Dependencies

`SyntaxCircus.Seo.Analysis`, `SyntaxCircus.Blazor.Seo.Analysis.PowerSeo`, `SyntaxCircus.Seo.Analysis.Yoast`.

## License

MIT
