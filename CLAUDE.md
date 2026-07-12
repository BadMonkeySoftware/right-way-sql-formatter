# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Primary build — macOS day-to-day (core library + CLI + tests, no SSMS plugin)
dotnet build RightWaySqlFormatter.NoSSMS.slnx

# Full solution build — Windows only (includes SSMS plugin)
dotnet build RightWaySqlFormatter.slnx

# Run all tests (expected: 347 total, 0 failed, 2 skipped)
dotnet test RightWaySqlFormatter.NoSSMS.slnx

# Regenerate expected-output files (deliberate use only — see Tests section)
REGEN_FILES="39_Foo.sql;39_Foo(SomeOption=True).sql" \
  dotnet test RightWaySqlFormatter.NoSSMS.slnx --filter "Name~RegenerateExpectedFiles"

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
- [RightWaySqlFormatter/ParseErrorAnalyzer.cs](RightWaySqlFormatter/ParseErrorAnalyzer.cs) — plain-English parse-error descriptions with source line numbers, from the tree (`hasError`/`errorLine` attributes) + token list (unfinished tokens; `IToken.LineNumber` is stamped by the tokenizer); observational only, never mutates the tree

**Parse-error handling:** invalid SQL is still formatted (best effort). The parser flags the root with `errorFound="1"` and both the offending element and its container with `hasError="1"`. The formatter prepends `ErrorOutputPrefix` (default: `--WARNING! ERRORS ENCOUNTERED DURING SQL PARSING!`). The CLI composes a detailed prefix from `ParseErrorAnalyzer`, emits the output anyway, and **exits 5** (0 with `--allow-parsing-errors`); the VS Code extension treats exit 5 as apply-output-plus-warning-toast. Don't change the library's default prefix — `28_BadNestingDontCrash` expected output encodes it.

**Alias-style policy:** both T-SQL column alias styles (`expr AS alias` and `alias = expr`) parse and format correctly, and the user's style is **preserved** — never rewrite `=` aliases to `AS` (or vice versa) unless an explicit `ColumnAliasStyle` option requests it. Alias intelligence lives in text-based post-processing passes at the end of `FormatSQLTree` (EnsureColumnAliases, RewriteAliasesToEqualSign, AlignSelectColumns), not in the parser.

**VS Code extension** shells out to the `SqlFormatter` CLI binary; it does not call .NET APIs directly. It applies formatting as line-minimal edits (LCS diff in `computeMinimalEdits`) and offers a native diff preview command (`Format Document (Preview)`) backed by a `TextDocumentContentProvider` on the `rwsql-format-preview` scheme.

**SSMS plugin** (`PoorMansTSqlFormatterSSMSLib/`, `PoorMansTSqlFormatterSSMSPackage/`) requires Windows + Visual Studio — cannot be built or tested on macOS.

## Tests

Test data lives in `PoorMansTSqlFormatterTest/Data/`:
- `InputSql/` — raw SQL inputs (`.sql`)
- `StandardFormatSql/` — expected formatter outputs (`.sql`; filename encodes formatter options)
- `ParsedSql/` — expected parse trees (`.xml`; same base name as the matching InputSql `.sql` file)

The test harness reads the `(...)` portion of each expected filename as the `TSqlStandardFormatterOptions` config string to build the formatter under test. Expected/input pairing is by filename with the `(...)` segment stripped. The two intentionally skipped tests are `02_Random_INVALID.sql` (malformed SQL) and `28_BadNestingDontCrash.sql` (broken nesting; must not crash).

Data files are byte-exact (BOM + CRLF significant); `.editorconfig` carves `Data/**` out of all normalization — never let an editor or script reformat them.

**Regenerating expected files:** use the `[Explicit]` test `ExpectedOutputRegenerator.RegenerateExpectedFiles` (never runs in a normal `dotnet test`):

```bash
REGEN_FILES="<expected-file>.sql;<expected-file>(Option=True).sql" \
  dotnet test RightWaySqlFormatter.NoSSMS.slnx --filter "Name~RegenerateExpectedFiles"
```

It formats the matching InputSql file through the library with the options encoded in each filename and writes the result to the source `Data/StandardFormatSql/` folder. This is the only sanctioned way to create or update expected outputs (still requires Jeremy's sign-off first).

## Rules (from AGENTS.md)

- **Never modify expected output files** in `StandardFormatSql/` or `ParsedSql/` without explicit sign-off from Jeremy. Changing them to make tests pass hides bugs.
- If formatter behavior changed intentionally, flag the expected file changes and wait for approval.
- When expected files legitimately need updating, use `ExpectedOutputRegenerator` (formatter library) — not the CLI (CLI defaults may differ from the test harness). Never hand-write expected output.
- Every commit touching expected files must explain in the message *why* the expected output changed.
- Run `dotnet test` before marking any task done. All tests must pass.
- Formatting behavior is additive only — new options are fine, but breaking existing formatting is not.
- SSMS plugin tasks require Windows; flag them explicitly rather than attempting them on macOS.
