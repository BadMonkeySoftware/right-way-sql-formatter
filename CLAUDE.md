# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Primary build — macOS day-to-day (core library + CLI + tests, no SSMS plugin)
dotnet build RightWaySqlFormatter.NoSSMS.slnx

# Full solution build — Windows only (includes SSMS plugin)
dotnet build RightWaySqlFormatter.slnx

# Run all tests (expected: 0 failed, 26 skipped — 2 historical + 24 ScriptDom-oracle ignores
# (8 inputs × 3 always-on oracle profiles); total-count differs by runner: sandbox counter 689)
dotnet test RightWaySqlFormatter.NoSSMS.slnx

# Regenerate expected-output files (deliberate use only — see Tests section)
REGEN_FILES="39_Foo.sql;39_Foo__AlignCols.sql" \
  dotnet test RightWaySqlFormatter.NoSSMS.slnx --filter "Name~RegenerateExpectedFiles"

# Run a specific test by name
dotnet test RightWaySqlFormatter.NoSSMS.slnx --filter "TestName=TestCmdLineIO"

# Run a specific test class
dotnet test RightWaySqlFormatter.NoSSMS.slnx --filter "ClassName=TSqlStandardFormatterTests"

# Build VS Code extension (compiles TypeScript + publishes self-contained CLI binary)
cd vscode-extension && npm install && npm run build      # all six platforms
cd vscode-extension && npm run build:host                # fast dev loop (this machine only)

# Release the extension (platform-specific .vsix per target, ~7 MB each)
# 1. bump "version" in vscode-extension/package.json
# 2. npm run changelog   (drafts from commits since last v* tag - EDIT it for users)
# 3. commit, git tag v<version>, push --tags
# 4. rm -rf dist && npm run package:all
# 5. npx vsce publish --packagePath dist/*.vsix
```

`dotnet` must be on PATH. If not found: `export PATH="$HOME/.dotnet:$PATH"`.
- builds must stay warning-free

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

**SSMS plugin** (`RightWaySqlFormatter.SSMSLib/`, `RightWaySqlFormatter.SSMS18/`) requires Windows + Visual Studio — cannot be built or tested on macOS. Jeremy has a Windows VM with SSMS 22; the full playbook (machine setup, msbuild-not-dotnet for VSSDK projects, manual deployment into SSMS 22's Extensions folder, ActivityLog diagnostics, and the caveat that Microsoft doesn't officially support SSMS 21/22 extensions) is in [docs/windows-ssms-dev.md](docs/windows-ssms-dev.md).

**Upstream issue triage**: [docs/upstream-issues-triage.md](docs/upstream-issues-triage.md) classifies all 149 open issues of the original PoorMansTSqlFormatter against this fork, with a prioritized still-present bug list. Consult it before hunting for formatter work.

## Tests

Test data lives in `RightWaySqlFormatter.Tests/Data/`:
- `InputSql/` — raw SQL inputs (`.sql`)
- `StandardFormatSql/` — expected formatter outputs (`.sql`; filename encodes formatter options)
- `ParsedSql/` — expected parse trees (`.xml`; same base name as the matching InputSql `.sql` file)

Configured expected filenames use the slug convention `<InputBaseName>__<Slug1>_<Slug2>.sql` (e.g. `30_NewFormatOptions__AlignCols_EqAlias.sql`) — filesystem-safe on all platforms and short enough for Windows MAX_PATH. Each slug maps to one `TSqlStandardFormatterOptions` fragment via the `CONFIG_SLUGS` dictionary in `RightWaySqlFormatter.Tests/Utils.cs`; unknown slugs fail loudly. Expected/input pairing strips everything from `__` onward. The two intentionally skipped tests are `02_Random_INVALID.sql` (malformed SQL) and `28_BadNestingDontCrash.sql` (broken nesting; must not crash).

Data files are byte-exact (BOM + CRLF significant); `.editorconfig` carves `Data/**` out of all normalization — never let an editor or script reformat them.

**Regenerating expected files:** use the `[Explicit]` test `ExpectedOutputRegenerator.RegenerateExpectedFiles` (never runs in a normal `dotnet test`):

```bash
REGEN_FILES="<expected-file>.sql;<expected-file>__<Slug>.sql" \
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
- SSMS plugin tasks require Windows; on macOS flag them rather than attempting them. On the Windows VM, follow [docs/windows-ssms-dev.md](docs/windows-ssms-dev.md) (VSSDK projects build with `msbuild`, not `dotnet build`).
