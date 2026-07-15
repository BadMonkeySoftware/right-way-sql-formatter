# Right Way SQL Formatter

A modernized T-SQL formatter based on [PoorMansTSqlFormatter](https://github.com/TaoK/PoorMansTSqlFormatter) by Tao Klerks.

Targets: **SSMS plugin**, **VS Code extension**, **CLI tool** — all powered by the same .NET 10 core formatting engine.

> Licensed under GNU AGPL v3 (inherited from upstream).

---

## Projects

| Project | Description |
|---|---|
| `RightWaySqlFormatter/` | Core formatting library (.NET 10) |
| `RightWaySqlFormatter.CmdLine/` | CLI tool — `SqlFormatter` binary |
| `RightWaySqlFormatter.SSMSPackage/` | SSMS plugin (Windows build only) |
| `RightWaySqlFormatter.SSMSLib/` | Shared SSMS helper library |
| `RightWaySqlFormatter.Tests/` | NUnit 4 test suite |
| `vscode-extension/` | VS Code extension (TypeScript, shells out to CLI) |

---

## Requirements

- [.NET 10 SDK](https://dot.net) — for building C# projects
- [Node.js 18+](https://nodejs.org) — for building the VS Code extension
- Windows + Visual Studio — for building the SSMS plugin only

### Install .NET 10 SDK (macOS/Linux, no sudo)

```bash
curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0 --install-dir ~/.dotnet
export PATH="$HOME/.dotnet:$PATH"
```

### Windows: long path support

Builds copy the test data under `bin\...\Data\`, so deep checkout locations can push paths past
Windows' historical 260-character `MAX_PATH` limit. Test data filenames are kept deliberately
short (see the slug convention in CLAUDE.md), but if you check out several folders deep, enable
long paths once:

```powershell
git config --global core.longpaths true   # lets git itself handle >260-char paths
```

and, as Administrator (or via Group Policy), allow long paths OS-wide:

```powershell
Set-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Control\FileSystem" -Name LongPathsEnabled -Value 1
```

Caveats: the registry setting requires Windows 10 1607+ and only helps applications whose
manifests opt in — modern git, .NET SDK, and VS 2022+ are fine, but File Explorer and some
older tools still choke on >260-char paths, so a shallow checkout location (e.g. `C:\src\`)
remains the most reliable option. `core.longpaths` alone fixes git operations but not tools
that later read those files.

---

## Building

### Core library + CLI + Tests

```bash
export PATH="$HOME/.dotnet:$PATH"

# Build everything (except SSMS plugin)
dotnet build RightWaySqlFormatter.NoSSMS.slnx

# Run tests
dotnet test RightWaySqlFormatter.NoSSMS.slnx
```

Expected: `0 failed, 10 skipped` (total counts grow as test data is added and differ by test runner; failures must be zero)

### CLI (Release build)

```bash
dotnet build RightWaySqlFormatter.CmdLine/RightWaySqlFormatter.CmdLine.csproj -c Release
# Binary: RightWaySqlFormatter.CmdLine/bin/Release/net10.0/SqlFormatter
```

### VS Code Extension

```bash
cd vscode-extension
npm install
npm run build   # builds self-contained CLI binary + compiles TypeScript
# Optional: package to .vsix
npm run package
```

---

## CLI Usage

Format SQL from stdin to stdout:

```bash
echo "select id,name from users where active=1" | SqlFormatter
```

Format a file to stdout:

```bash
SqlFormatter myquery.sql
```

Format a file, write to a file (pass the same name to format in place):

```bash
SqlFormatter --output formatted.sql myquery.sql
```

### Common flags

```
--indent-string="\t"        Indentation (default: 4 spaces). Use \s for space, \t for tab.
--uppercase-keywords=true   Uppercase keywords (default: true)
--standardize-keywords=true Normalize synonyms, e.g. NVARCHAR (default: true)
--expand-comma-lists=true   Expand column lists onto separate lines (default: true)
--expand-in-lists=true      Expand IN (...) lists (default: true)
--trailing-commas=false     Leading vs trailing commas (default: leading)
--statement-breaks=2        Blank lines between statements (default: 2)
--clause-breaks=1           Blank lines between clauses (default: 1)
--max-line-width=999        Max line width (default: 999)
--alias-style=as            Column alias style: 'as' or 'equals' (default: as)
--align-columns=false       Align aliases vertically in SELECT lists (default: false)
--column-always-has-alias=false  Add an explicit alias to every SELECT column (default: false)
--allow-parsing-errors=false Exit 0 even when the input has parse errors (default: false)
--align-table-joins=false   Align FROM/JOIN tables, aliases, ON conditions (default: false)
--align-table-joins-add-aliases=true  With table-join alignment: add aliases to unaliased tables (default: true)
--indent-where-and-or=false Put AND/OR in WHERE onto separate, indented lines (default: false)
--compact-raiserror=false   Keep RAISERROR(...) argument lists on one line (default: false)
--remove-harmless-brackets=false  Strip [brackets] from names that provably don't need them (default: false)
--compact-single-statement-blocks=false  Inline short single-statement IF/ELSE/WHILE bodies (default: false)
```

Run `SqlFormatter --help` for the full list.

Both T-SQL column alias styles (`expr AS alias` and `alias = expr`) are supported in input SQL, and the style you wrote is preserved unless you pass `--alias-style` explicitly.

### Exit codes and invalid SQL

| Code | Meaning |
|------|---------|
| 0    | Formatted cleanly (also with `--allow-parsing-errors` when errors occurred) |
| 1    | Fatal error (unreadable input, unexpected exception) — no output |
| 5    | Input had parse errors — formatted output IS still emitted, prefixed with a warning comment |

On parse errors the output starts with a diagnostic comment block, e.g.:

```sql
-- WARNING! ERRORS ENCOUNTERED DURING SQL PARSING - formatted output may be incorrect:
--   Unclosed string literal (missing closing single-quote)
--   Unexpected token ')'
```

Diagnostics cover unclosed strings/comments/bracket identifiers, unexpected or misplaced tokens/keywords, and incomplete statements at end of input.

### Examples

```bash
# Run a repo test input through the formatter with alignment options
dotnet run --project RightWaySqlFormatter.CmdLine -- --align-table-joins=true --indent-join-on=true < RightWaySqlFormatter.Tests/Data/InputSql/31_AlignTableJoins.sql 2>/dev/null

# Format with 4 spaces for indent (using escape sequences)
echo "select 1,2,3" | SqlFormatter --indent-string="\s\s\s\s"

# Format with single tab
echo "select 1,2,3" | SqlFormatter --indent-string="\t"

# Lowercase keywords
echo "SELECT 1" | SqlFormatter --uppercase-keywords=false

# Compact output (no expansions)
echo "select id,name,email from users" | SqlFormatter \
  --expand-comma-lists=false \
  --expand-boolean=false \
  --expand-case=false \
  --statement-breaks=1

# Run the real-world corpus sweep under the profiles longtime PoorMans/SSMS users actually run
PROFILE='--indent-string=\t --standardize-keywords=false' tools/realworld-test.sh   # classic SSMS defaults
PROFILE='--trailing-commas=true --expand-in-lists=false'  tools/realworld-test.sh   # common alt style
PROFILE='--align-columns=true --alias-style=equals'       tools/realworld-test.sh   # alignment features

# "Heavy editor" profile exercising all the new options at once
PROFILE='--expand-between=false --expand-boolean=false --expand-case=false --expand-in-lists=false --uppercase-keywords=false --standardize-keywords=false --trailing-commas=true --align-table-joins=true --column-always-has-alias=true --select-first-column-newline=true --align-columns=true --align-ddl-columns=true --alias-style=equals --indent-where-and-or=true --max-line-width=200 --compact-raiserror=true --compact-single-statement-blocks=true' tools/realworld-test.sh
```

### Real-world corpus results

Current sweep results (396 real-world files: First Responder Kit, Ola Hallengren,
sp_WhoIsActive, DarlingData, tSQLt):

| Profile | OK | Parse warnings | Crashes | Non-idempotent |
|---|---|---|---|---|
| default | 394 | 2* | 0 | 0 |
| classic SSMS (tabs, no keyword standardization) | 394 | 2* | 0 | 0 |
| trailing commas, no IN-list expansion | 394 | 2* | 0 | 0 |
| align-columns + equals aliases | 381 | 2* | 0 | 13** |

\* Both expected: one file is SQLCMD-mode (`$(var)`, `:OUT` — not T-SQL), one contains
an intentionally unclosed comment (tSQLt parser experiment).
\** Cosmetic oscillation on dynamic-SQL string-concatenation boundary lines; output is
valid SQL on every pass. Tracked as a known limitation of the text-based align passes.

---

## Dependencies

The shipping binaries have **zero external runtime dependencies** — the core library and
CLI are pure .NET BCL (CLI argument parsing is hand-rolled), and the VS Code extension has
no runtime npm dependencies. All upstream-era dependencies (NDesk.Options, LinqBridge,
Bridge.NET, ILRepack, UnmanagedExports) were removed during modernization.

Remaining packages, none of which ship:

- NUnit 4 / NUnit3TestAdapter / Microsoft.NET.Test.Sdk — test project only
- Microsoft.SqlServer.TransactSql.ScriptDom — test project only (validity oracle:
  formatted output of valid input must itself parse under ScriptDom)
- EnvDTE / Microsoft.VisualStudio.SDK — mandatory Visual Studio interop for the
  Windows-only SSMS plugin
- typescript / @vscode/vsce / @types/* — VS Code extension build tooling only

---

## VS Code Extension

See [vscode-extension/README.md](vscode-extension/README.md) for full setup, commands, and settings.

**Quick start:**
1. `cd vscode-extension && npm install && npm run build`
2. Open the `vscode-extension/` folder in VS Code and press `F5` to launch an Extension Development Host
3. Open a `.sql` file → right-click → "Right Way SQL: Format Document"

Use **"Right Way SQL: Format Document (Preview)"** to review changes in VS Code's native diff editor (original vs formatted) before applying. Formatting is applied as minimal line edits, so cursor position and undo history stay sane, and invalid SQL produces a warning comment at the top of the output plus a toast instead of failing silently.

---

## SSMS Plugin

The `RightWaySqlFormatter.SSMSPackage/` project is the VS Package-style SSMS plugin. Build requires:
- Windows
- Visual Studio 2019+ with VSIX development workload
- SSMS 18+ (for testing)

Build on a Windows VM. SSMS 21/22 have no official extension support; see
[docs/windows-ssms-dev.md](docs/windows-ssms-dev.md) for the manual deployment
path (Extensions folder + `ssms.exe /setup`) and its caveats.

---

## Development Notes

- **Namespaces** are intentionally kept as `PoorMansTSqlFormatterLib.*` in the core library to avoid breaking anything if upstream changes are ever cherry-picked in. Project folders and assemblies use the `RightWaySqlFormatter.*` names.
- **Upstream reference**: `git fetch upstream` to pull latest changes from TaoK/PoorMansTSqlFormatter for comparison.
- **Build warnings**: the build is warning-free; keep it that way.
- **Known test skips** (the 10 in the expected count): `02_Random_INVALID.sql` and `28_BadNestingDontCrash.sql` contain intentionally malformed SQL that can't round-trip cleanly, plus 8 ScriptDom-oracle ignores for inputs using legacy syntax the TSql170 oracle itself can't parse.
- **Test data is byte-exact** (BOM + CRLF significant) — `.editorconfig` carves `RightWaySqlFormatter.Tests/Data/**` out of all normalization; never let an editor reformat those files.

---

## License

GNU Affero General Public License v3 (see [LICENSE.txt](LICENSE.txt)) — inherited from
[PoorMansTSqlFormatter](https://github.com/TaoK/PoorMansTSqlFormatter). If you distribute
binaries built from this repo (including the VS Code extension), the AGPL requires making
the corresponding source available.
