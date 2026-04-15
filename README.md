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
```

Run `SqlFormatter --help` for the full list.

### Examples

```bash
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
```

---

## VS Code Extension

See [vscode-extension/README.md](vscode-extension/README.md) for full setup and usage.

**Quick start:**
1. Build the CLI (Release) and copy to `vscode-extension/bin/SqlFormatter`
2. `cd vscode-extension && npm install && npm run compile`
3. Open in VS Code and press `F5` to launch Extension Development Host
4. Open a `.sql` file → right-click → "Right Way SQL: Format Document"

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
- **Known test skips**: `02_Random_INVALID.txt` and `28_BadNestingDontCrash.txt` are skipped in reformatting tests — both contain intentionally malformed SQL that the original formatter also couldn't round-trip cleanly.

---

## License

GNU Affero General Public License v3 — inherited from [PoorMansTSqlFormatter](https://github.com/TaoK/PoorMansTSqlFormatter).
Personal use only. Not for redistribution or commercial deployment.
