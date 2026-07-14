# Changelog

All notable changes to the Right Way T-SQL Formatter extension.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [0.1.5] - 2026-07-14

### Added

- CHANGELOG.md + npm run changelog draft generator
- platform-specific .vsix packaging (44 MB -> ~12 MB/download)
- three new formatting options + IndentWhereAndOrConditions fix
- native diff preview + minimal-edit formatting
- restore + improve parse-error reporting in formatted output
- implement columnAlwaysHasAlias setting
- add alignTableJoins VSCode setting and test data
- SelectFirstColumnOnNewLine test + change default indent to 4 spaces
- keyword updates, alias style, column alignment, JOIN/WHERE alignment, DDL settings
- launch.json/tasks.json for dev/debug; SELECT first column on new line option
- npm run build handles everything — self-contained CLI + TypeScript compile
- add VSCode extension, README, .gitignore, .editorconfig

### Fixed

- absolute image URLs for Marketplace README; v0.1.4
- add license field to package-lock.json
- update license field in package.json and remove PublishTrimmed flag in build-cli.js
- add test coverage for uppercaseKeywords=false with standardizeKeywords=true; document indentString escape sequences

### Changed

- real screen recording of Format Document (Preview)
- Marketplace polish — badges, demo GIF, highlights
- dedupe root README, refresh extension README for Marketplace
- update for new baseline, parse-error contract, alias policy, regen tool


## [0.1.4] - 2026-07-14

First public Marketplace release.

### Added

- Format Document, Format Selection, and Format Document (Preview) commands for `.sql` files, plus registration as the default SQL document formatter (⇧⌥F and format-on-save).
- Native diff preview: review proposed formatting in VS Code's built-in diff editor with Apply/Discard before anything touches the file.
- 27 formatting options, including trailing commas, column/JOIN alignment, `alias = expr` column alias style (existing style always preserved), compact RAISERROR, and compact single-statement blocks.
- Platform-specific packages (win32-x64/arm64, darwin-x64/arm64, linux-x64/arm64) — each install carries a single bundled native `SqlFormatter` binary; no .NET runtime required.

### Changed

- Formatting is applied as line-minimal edits: cursor position survives and undo is a single step.
- Invalid SQL still gets best-effort formatting with a diagnostic comment (including source line numbers) and a warning toast instead of a silent failure.
