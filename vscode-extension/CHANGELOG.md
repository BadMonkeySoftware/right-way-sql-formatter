# Changelog

All notable changes to the Right Way T-SQL Formatter extension.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

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
