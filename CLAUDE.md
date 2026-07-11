# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Primary build — macOS day-to-day (core library + CLI + tests, no SSMS plugin)
dotnet build RightWaySqlFormatter.NoSSMS.slnx

# Full solution build — Windows only (includes SSMS plugin)
dotnet build RightWaySqlFormatter.slnx

# Run all tests (expected: 180 passed, 2 skipped, 0 failed)
dotnet test RightWaySqlFormatter.NoSSMS.slnx

# Run a specific test by name
dotnet test RightWaySqlFormatter.NoSSMS.slnx --filter "TestName=TestCmdLineIO"

# Run a specific test class
dotnet test RightWaySqlFormatter.NoSSMS.slnx --filter "ClassName=TSqlStandardFormatterTests"

# Build VS Code extension (compiles TypeScript + publishes self-contained CLI binary)
cd vscode-extension && npm install && npm run build
```

`dotnet` must be on PATH. If not found: `export PATH="$HOME/.dotnet:$PATH"`.

## Architecture

A modernized T-SQL formatter (forked from PoorMansTSqlFormatter) with three products sharing one core library: a VS Code extension, a CLI (`SqlFormatter`), and a Windows-only SSMS plugin.

**Three-phase pipeline:**

1. **Tokenize** — `TSqlStandardTokenizer` converts SQL string → token list
2. **Parse** — `TSqlStandardParser` converts tokens → AST (parse tree with attributes)
3. **Format** — `TSqlStandardFormatter` walks the AST → formatted SQL string

Entry point: `SqlFormattingManager.Format()` composes these via `ISqlTokenizer` / `ISqlTokenParser` / `ISqlTreeFormatter` interfaces (strategy pattern). Formatter behavior is controlled by `TSqlStandardFormatterOptions`, which serializes to a compact `key=value` string embedded in test filenames.

**Key files:**
- [RightWaySqlFormatter/Formatters/TSqlStandardFormatter.cs](RightWaySqlFormatter/Formatters/TSqlStandardFormatter.cs) — largest component (108KB), all formatting logic
- [RightWaySqlFormatter/Parsers/TSqlStandardParser.cs](RightWaySqlFormatter/Parsers/TSqlStandardParser.cs) — AST construction
- [RightWaySqlFormatter/Tokenizers/TSqlStandardTokenizer.cs](RightWaySqlFormatter/Tokenizers/TSqlStandardTokenizer.cs) — lexer
- [RightWaySqlFormatter/SqlFormattingManager.cs](RightWaySqlFormatter/SqlFormattingManager.cs) — public API

**VS Code extension** shells out to the `SqlFormatter` CLI binary; it does not call .NET APIs directly.

**SSMS plugin** (`PoorMansTSqlFormatterSSMSLib/`, `PoorMansTSqlFormatterSSMSPackage/`) requires Windows + Visual Studio — cannot be built or tested on macOS.

## Tests

Test data lives in `PoorMansTSqlFormatterTest/Data/`:
- `InputSql/` — raw SQL inputs
- `StandardFormatSql/` — expected formatter outputs (filename encodes formatter options)
- `ParsedSql/` — expected parse trees
- `CmdLineSql/` — expected CLI outputs

The test harness reads the `(...)` portion of each expected filename as the `TSqlStandardFormatterOptions` config string to build the formatter under test. The two intentionally skipped tests are `02_Random_INVALID.sql` (malformed SQL) and `28_BadNestingDontCrash.sql` (broken nesting; must not crash).

## Rules (from AGENTS.md)

- **Never modify expected output files** in `StandardFormatSql/`, `ParsedSql/`, or `CmdLineSql/` without explicit sign-off from Jeremy. Changing them to make tests pass hides bugs.
- If formatter behavior changed intentionally, flag the expected file changes and wait for approval.
- When expected files legitimately need updating, use the formatter library directly — not the CLI (CLI defaults may differ from the test harness). Never hand-write expected output.
- Every commit touching expected files must explain in the message *why* the expected output changed.
- Run `dotnet test` before marking any task done. All tests must pass.
- Formatting behavior is additive only — new options are fine, but breaking existing formatting is not.
- SSMS plugin tasks require Windows; flag them explicitly rather than attempting them on macOS.
