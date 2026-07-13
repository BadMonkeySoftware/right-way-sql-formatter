# Right Way SQL Formatter

A modernized T-SQL formatter based on [PoorMansTSqlFormatter](https://github.com/TaoK/PoorMansTSqlFormatter) by Tao Klerks.

Targets: **SSMS plugin**, **VS Code extension**, **CLI tool** — all powered by the same .NET 10 core formatting engine.

> Personal use. Licensed under GNU AGPL v3 (inherited from upstream).

---

## Projects

| Project | Description |
|---|---|
| `RightWaySqlFormatter/` | Core formatting library (.NET 10) |
| `PoorMansTSqlFormatterCmdLine/` | CLI tool — `SqlFormatter` binary |
| `PoorMansTSqlFormatterSSMSPackage/` | SSMS plugin (Windows build only) |
| `PoorMansTSqlFormatterSSMSLib/` | Shared SSMS helper library |
| `PoorMansTSqlFormatterTest/` | NUnit 4 test suite |
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
dotnet build RightWaySqlFormatter.slnx

# Run tests
dotnet test PoorMansTSqlFormatterTest/PoorMansTSqlFormatterTests.csproj
```

Expected: `180 passed, 2 skipped, 0 failed`

### CLI (Release build)

```bash
dotnet build PoorMansTSqlFormatterCmdLine/PoorMansTSqlFormatterCmdLine.csproj -c Release
# Binary: PoorMansTSqlFormatterCmdLine/bin/Release/net10.0/SqlFormatter
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

Format a file in-place:

```bash
SqlFormatter myquery.sql
```

Format a file, write to a new file:

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
dotnet run --project PoorMansTSqlFormatterCmdLine -- --align-table-joins=true --indent-join-on=true < PoorMansTSqlFormatterTest/Data/InputSql/31_AlignTableJoins.sql 2>/dev/null

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

# Run with different profiles using realworld tests by the profiles longtime PoorMans/SSMS users actually run
PROFILE='--indent-string=\t --standardize-keywords=false' tools/realworld-test.sh   # classic SSMS defaults
PROFILE='--trailing-commas=true --expand-in-lists=false'  tools/realworld-test.sh   # common alt style
PROFILE='--align-columns=true --alias-style=equals'       tools/realworld-test.sh   # your new features
```
```text
PROFILE='--indent-string=\t --standardize-keywords=false' tools/realworld-test.sh   # classic SSMS defaults

PARSE-WARN tsqlt/Build/SQL/ChangeDbAndExecuteStatement(tSQLt.Build).sql
PARSE-WARN tsqlt/Experiments/AnnotationParser.sql

====================================================
Profile:     --indent-string=\t --standardize-keywords=false
Total files: 396
OK:          394
PARSE-WARN:  2   (exit 5 — inspect <HOME>/Workspaces/BadMonkeySoftware/Git/right-way-sql-formatter/realworld-results/flagged/*.parsewarn)
FATAL:       0  (crashes — always bugs)
UNSTABLE:    0 (non-idempotent — diff pass1 vs pass2 in <HOME>/Workspaces/BadMonkeySoftware/Git/right-way-sql-formatter/realworld-results/flagged)
====================================================


PROFILE='--trailing-commas=true --expand-in-lists=false'  tools/realworld-test.sh   # common alt style

PARSE-WARN tsqlt/Build/SQL/ChangeDbAndExecuteStatement(tSQLt.Build).sql
PARSE-WARN tsqlt/Experiments/AnnotationParser.sql

====================================================
Profile:     --trailing-commas=true --expand-in-lists=false
Total files: 396
OK:          394
PARSE-WARN:  2   (exit 5 — inspect <HOME>/Workspaces/BadMonkeySoftware/Git/right-way-sql-formatter/realworld-results/flagged/*.parsewarn)
FATAL:       0  (crashes — always bugs)
UNSTABLE:    0 (non-idempotent — diff pass1 vs pass2 in <HOME>/Workspaces/BadMonkeySoftware/Git/right-way-sql-formatter/realworld-results/flagged)
====================================================

PROFILE='--align-columns=true --alias-style=equals'       tools/realworld-test.sh   # your new features

UNSTABLE   first-responder-kit/Deprecated/sp_BlitzInMemoryOLTP.sql
UNSTABLE   first-responder-kit/Deprecated/sp_BlitzIndex_SQL_Server_2005.sql
UNSTABLE   first-responder-kit/Deprecated/sp_BlitzQueryStore.sql
UNSTABLE   first-responder-kit/Documentation/Using_AI.sql
UNSTABLE   first-responder-kit/Install-All-Scripts.sql
UNSTABLE   first-responder-kit/Install-Azure.sql
UNSTABLE   first-responder-kit/sp_BlitzAnalysis.sql
UNSTABLE   first-responder-kit/sp_BlitzCache.sql
UNSTABLE   first-responder-kit/sp_BlitzFirst.sql
UNSTABLE   first-responder-kit/sp_BlitzIndex.sql
UNSTABLE   first-responder-kit/sp_BlitzLock.sql
UNSTABLE   first-responder-kit/sp_BlitzWho.sql
PARSE-WARN tsqlt/Build/SQL/ChangeDbAndExecuteStatement(tSQLt.Build).sql
PARSE-WARN tsqlt/Experiments/AnnotationParser.sql
UNSTABLE   tsqlt/Tests/RenameClassTests.class.sql

====================================================
Profile:     --align-columns=true --alias-style=equals
Total files: 396
OK:          381
PARSE-WARN:  2   (exit 5 — inspect <HOME>/Workspaces/BadMonkeySoftware/Git/right-way-sql-formatter/realworld-results/flagged/*.parsewarn)
FATAL:       0  (crashes — always bugs)
UNSTABLE:    13 (non-idempotent — diff pass1 vs pass2 in <HOME>/Workspaces/BadMonkeySoftware/Git/right-way-sql-formatter/realworld-results/flagged)
====================================================

```
## fixes needed
- RAISERROR('Setting up configuration variables',10,1) WITH NOWAIT; is formatting and putting on separate lines:
after: raiserror (
            'Setting up configuration variables',
            10,
            1
            )
    with nowait;
- if/else without begin/end that are 1 line are being broken out.
- alias are added to from, is that because of columnAlwaysHasAlias = true ? 
- maxLineWidth set at 200, but SET @StringToExecute = N' IF EXISTS(SELECT * FROM '
        + @OutputDatabaseName
        + '.INFORMATION_SCHEMA.SCHEMATA WHERE QUOTENAME(SCHEMA_NAME) = '''
        + @OutputSchemaName + ''') INSERT '
        + @OutputDatabaseName + '.'
        + @OutputSchemaName + '.'
        + @OutputTableName
        + ' (ServerName, CheckDate, CheckID, Priority, FindingsGroup, Finding, Details, URL) VALUES( '
        + ' @SrvName, @LogMessageCheckDate, @LogMessageCheckID, @LogMessagePriority, @LogMessageFindingsGroup, @LogMessageFinding, @LogMessage, @LogMessageURL)';
broke at 206 and was changed to:
        set @StringToExecute = N' IF EXISTS(SELECT * FROM ' + @OutputDatabaseName + '.INFORMATION_SCHEMA.SCHEMATA WHERE QUOTENAME(SCHEMA_NAME) = ''' + @OutputSchemaName + ''') INSERT ' + @OutputDatabaseName + '.' + 
            @OutputSchemaName + '.' + @OutputTableName + ' (ServerName, CheckDate, CheckID, Priority, FindingsGroup, Finding, Details, URL) VALUES( ' + 
            ' @SrvName, @LogMessageCheckDate, @LogMessageCheckID, @LogMessagePriority, @LogMessageFindingsGroup, @LogMessageFinding, @LogMessage, @LogMessageURL)';
- indentWhereAndOrConditions = true, but putting into one line
WHERE c.TABLE_NAME = @TableName
        and c.TABLE_NAME = @TableName
        and c.TABLE_NAME = @TableName
        and c.TABLE_NAME = @TableName
        and c.TABLE_NAME = @TableName
is then changed to:
where c.TABLE_NAME = @TableName and c.TABLE_NAME = @TableName and c.TABLE_NAME = @TableName and c.TABLE_NAME = @TableName and c.TABLE_NAME = @TableName

---

## TODO (Remove external libraries)
### Analysis of dependency and if remove, replace, or rewrite
| Dependency                | Original Purpose      | Modern Replacement                                                 | Recommendation    |
| ------------------------- | --------------------- | ------------------------------------------------------------------ | ----------------- |
| NDesk.Options             | CLI parsing           | `System.CommandLine`, `Spectre.Console.Cli`, or hand-rolled parser | Remove            |
| LinqBridge                | LINQ for .NET 2.0     | Built into .NET 3.5+                                               | Delete            |
| NUnit                     | Unit tests            | xUnit or NUnit 4                                                   | Replace           |
| UnmanagedExports          | Native DLL exports    | NativeAOT, CsWin32, or remove plugin                               | Depends           |
| Notepad++ Plugin Template | Notepad++ integration | Modern C++ plugin or C# via .NET hosting                           | Rewrite if needed |
| ILRepack                  | Merge DLLs            | Single-file publish                                                | Remove            |
| Bridge.NET                | C# → JavaScript       | Blazor, TypeScript wrapper, or WebAssembly                         | Replace           |


## VS Code Extension

See [vscode-extension/README.md](vscode-extension/README.md) for full setup and usage.

**Quick start:**
1. Build the CLI (Release) and copy to `vscode-extension/bin/SqlFormatter`
2. `cd vscode-extension && npm install && npm run compile`
3. Open in VS Code and press `F5` to launch Extension Development Host
4. Open a `.sql` file → right-click → "Right Way SQL: Format Document"

Use **"Right Way SQL: Format Document (Preview)"** to review changes in VS Code's native diff editor (original vs formatted) before applying. Formatting is applied as minimal line edits, so cursor position and undo history stay sane, and invalid SQL produces a warning comment at the top of the output plus a toast instead of failing silently.

---

## SSMS Plugin

The `PoorMansTSqlFormatterSSMSPackage/` project is the VS Package-style SSMS plugin. Build requires:
- Windows
- Visual Studio 2019+ with VSIX development workload
- SSMS 18+ (for testing)

Build on Windows VM, install the resulting `.vsix`.

---

## Development Notes

- **Namespaces** are intentionally kept as `PoorMansTSqlFormatterLib.*` in the core library to avoid breaking anything if upstream changes are ever cherry-picked in.
- **Upstream reference**: `git fetch upstream` to pull latest changes from TaoK/PoorMansTSqlFormatter for comparison.
- **Nullable warnings**: 76 nullable reference warnings remain in the core library (pre-existing from the original codebase). Not blocking but targeted for cleanup.
- **Known test skips**: `02_Random_INVALID.sql` and `28_BadNestingDontCrash.sql` are skipped in reformatting tests — both contain intentionally malformed SQL that the original formatter also couldn't round-trip cleanly.

---

## License

GNU Affero General Public License v3 — inherited from [PoorMansTSqlFormatter](https://github.com/TaoK/PoorMansTSqlFormatter).
Personal use only. Not for redistribution or commercial deployment.
