# SyntaxCircus.Blazor.Seo.Authoring

Live SEO and readability checks while writing a page, with "edit this" jump targets. Lifted from a production
site's authoring flow into a reusable package that is independent of any particular CMS model.

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

```csharp
await using var session = new SeoAuthoringSession(presentation);
session.Changed += StateHasChanged;
await session.CheckAsync(draft, immediate: false, cancellationToken);
var view = session.View; // SeoAuthoringPanelViewModel
```

## Dependencies

`SyntaxCircus.Seo.Analysis`, `SyntaxCircus.Blazor.Seo.Analysis.PowerSeo`, `SyntaxCircus.Seo.Analysis.Yoast`.

## License

MIT
