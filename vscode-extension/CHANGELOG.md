# Changelog

All notable changes to the Right Way T-SQL Formatter extension.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [1.0.0] - 2026-07-21

**1.0.** Two properties are now guaranteed and machine-enforced, verified
across ~400 real-world SQL files (First Responder Kit, Ola Hallengren,
DarlingData, tSQLt) under every settings profile:

- **Validity** — for any input that parses under Microsoft's own T-SQL grammar
  (ScriptDom), the formatted output parses too. The formatter never corrupts
  your SQL.
- **Stability** — re-formatting already-formatted SQL is a byte-for-byte
  no-op. Format-on-save never produces churn.

The public contracts are frozen as of 1.0: settings names and semantics, the
CLI flags and exit codes (0 clean / 1 fatal / 5 parse-errors-with-output), the
preserve-your-alias-style policy, and additive-only formatting changes going
forward.

### Fixed

- The last known re-format drift cases with alignment/alias settings are gone: aligned JOIN blocks now account for the `AS` aliases they add near the line-width limit, wrapped computed-column expressions are never padded inside their string literals, and a column with an unclosed `[` bracket (invalid SQL) no longer gains a fresh alias on every pass.
- Pathologically deep nesting (hundreds of stacked derived tables — think generated SQL) now produces a normal best-effort result with a parse-error warning instead of crashing the formatter process.

### Changed

- When the styling passes adjust a line's layout, the formatter internally settles the result before returning it, so what you get is already its own fixed point. This can add up to ~70% to formatting time on very large files (multi-MB scripts) when alignment/alias settings are active; typical editor-sized files are unaffected, and formatting with default settings is byte-identical to 0.2.0 with zero overhead.


## [0.2.0] - 2026-07-19

A correctness release for the styling options. The formatting engine was
validated against Microsoft's own T-SQL parser (ScriptDom) across ~400
real-world SQL files (First Responder Kit, Ola Hallengren, DarlingData,
tSQLt): every profile now always produces valid T-SQL, and re-formatting
already-formatted files is stable in all but a handful of known cosmetic
cases. If you use the alignment/alias settings, this release fixes a long
tail of cases where they could silently corrupt SQL.

### Fixed

- `alignTableJoins` no longer rewrites statements that merely contain a FROM keyword: `BULK INSERT … FROM`, Service Broker `BEGIN DIALOG … FROM SERVICE` (which previously **lost content**), `REVOKE … FROM`, `FETCH NEXT FROM cursor`, `BACKUP`/`RESTORE`, and DELETE targets are all left alone. Table-valued function calls in JOINs keep their full argument lists (arguments were previously dropped).
- Auto-alias settings (`columnAlwaysHasAlias`, `tableJoinsAddAliases`) no longer invent invalid aliases: table variables, derived tables, `(nolock)` hints, reserved-word names, and complex bracketed names are skipped; reserved-word column aliases get brackets.
- Existing aliases in every T-SQL spelling are now recognized (so they're never double-aliased): AS-less plain aliases (`count(1) Cnt`), bracketed (`Name [Test Case Name]`, including `]]` escapes), string-literal (`CONVERT(…) N'v2.02'`, `'time' = expr`), and compound assignment `@v += …` is left intact.
- Expressions wrapped across lines (long CASE arms, string concatenations, `TOP (nested(calls))`) are treated as one expression — continuation lines are no longer aliased or spliced as if they were new columns.
- Comments are never damaged: aliases are inserted before trailing `--` comments instead of after them (where they were dead text), alignment never pads inside or re-positions comments, and comment positions no longer drift on repeated formatting.
- `alignDdlColumns` only aligns real column/parameter definitions (CREATE/ALTER, `DECLARE @t TABLE`) — INSERT column lists and VALUES rows are untouched (previously padding could be inserted **inside string literals**).
- Multi-line string literals keep their original line endings — CRLF inside dynamic-SQL text is no longer rewritten to LF.
- With `maxLineWidth` set, wrap decisions are now stable on re-format, and no trailing space is left at wrap points.

### Changed

- Lines wrapped at `maxLineWidth` no longer end with a stranded space. If you diff formatter output before/after upgrading, expect whitespace-only changes at wrap points.

## [0.1.6] - 2026-07-15

Nine formatting-engine fixes (from a full triage of the original
PoorMansTSqlFormatter's open issues) and one new opt-in setting.

### Added

- New setting `removeHarmlessBrackets` (default **off**): strips square brackets from names that provably don't need them — valid plain identifiers that aren't reserved words and wouldn't merge with adjacent text. `[Name]` → `Name`, `[dbo].[MyTable] [t]` → `dbo.MyTable t`, while `[Order]`, `[Some Name]`, and `table_[some_id]` keep their brackets.

### Fixed

- Nested joins with chained ON clauses (`FROM a JOIN b JOIN c ON … ON …`) now parse and format cleanly — previously flagged as a parse error with unstable output.
- `IF @x IS NULL THROW 50001, 'msg', 1;` without BEGIN/END: the THROW is no longer swallowed into the IF condition, so consecutive IF…THROW lines don't nest ever deeper.
- Dynamic-SQL fragments survive formatting untouched: no space injected inside doubled-quote text (`''.txt''` previously became `''.txt ''`) or before directly adjacent bracket names (`table_[some_id]` previously became `table_ [some_id]`).
- `ALTER TABLE … ALTER COLUMN …` is formatted as the single statement it is — no more blank line splitting it in two.
- Multi-line banner comments that start at column 0 keep their first line at column 0, so box-art comment blocks stay aligned.
- `--[noformat]` regions inside statements are no longer moved out of their surrounding parentheses, and a blank line no longer accumulates inside the region on every reformat.
- `definition` and `status` are no longer treated as keywords (they're everyday catalog column names): your casing is preserved instead of being uppercased, and `removeHarmlessBrackets` treats them as unbracketable.

### Changed

- Dev-dependency updates (Dependabot: form-data, markdown-it, tmp, js-yaml, undici) and npm audit fix; debug launch configs now isolate the extension under development.

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
