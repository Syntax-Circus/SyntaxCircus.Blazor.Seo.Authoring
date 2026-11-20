# Changelog

All notable changes to this project are documented here. Format loosely follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## Unreleased

### Added

- `SeoAuthoringSession` (debounced, supersession-safe live checks), `SeoAuthoringPresentation` (maps analysis
  findings to plain-English suggestions with "edit this" targets), `SeoAuthoringDraft` and the
  `ISeoDraftDocumentBuilder` seam with `DefaultSeoDraftDocumentBuilder`.
- `SeoAuthoringPanel` component with scoped styles, `AddSeoAuthoring` registration (`SeoAuthoringOptions`,
  optional `ISeoProviderSelector`; unknown selection means core-only checks).

### Fixed

- The session no longer holds its lock while preparing a check or raising `Changed`.
- Canonical URLs keep percent-encoding (`/a%20b`) in the analyzed document and snippet.
- A body `data-section-id` equal to a reserved key (`title`, `metaDescription`) no longer claims that edit target.
