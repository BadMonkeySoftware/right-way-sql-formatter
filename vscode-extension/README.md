# Right Way SQL Formatter — VSCode Extension

VS Code extension that formats T-SQL using the `SqlFormatter` CLI.

## How it works

The extension shells out to the `SqlFormatter` binary (built from the .NET project in this repo).
SQL is piped via stdin, formatted output comes back on stdout.
No language server, no parsing in JS — all formatting logic lives in the .NET core library.

## Commands

| Command | Keyboard Shortcut | Description |
|---|---|---|
| Right Way SQL: Format Document | — | Formats the entire active SQL file |
| Right Way SQL: Format Selection | — | Formats only the selected text |

Both commands are also available in the right-click context menu when editing a `.sql` file.

## Setup

### 1. Build the CLI

From the repo root:

```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet build PoorMansTSqlFormatterCmdLine/PoorMansTSqlFormatterCmdLine.csproj -c Release
```

The binary will be at:
```
PoorMansTSqlFormatterCmdLine/bin/Release/net10.0/SqlFormatter
```

### 2. Make it accessible

**Option A — Copy to extension bin/ folder (recommended for dev):**
```bash
mkdir -p vscode-extension/bin
cp PoorMansTSqlFormatterCmdLine/bin/Release/net10.0/SqlFormatter vscode-extension/bin/
chmod +x vscode-extension/bin/SqlFormatter
```

**Option B — Add to PATH:**
```bash
# Add to your shell profile (e.g. ~/.zshrc):
export PATH="$PATH:/path/to/right-way-sql-formatter/PoorMansTSqlFormatterCmdLine/bin/Release/net10.0"
```

**Option C — Set in VS Code settings:**
```json
{
  "rightWaySqlFormatter.executablePath": "/absolute/path/to/SqlFormatter"
}
```

### 3. Install the extension (dev mode)

```bash
cd vscode-extension
npm install
npm run compile
```

Then in VS Code: `F1` → "Extensions: Install from VSIX..." or press `F5` to launch an Extension Development Host.

## Settings

All settings are under `rightWaySqlFormatter.*`:

| Setting | Default | Description |
|---|---|---|
| `executablePath` | `""` | Explicit path to SqlFormatter binary. Auto-detected if empty. |
| `indentString` | `"\t"` | Indent character(s) per level |
| `spacesPerTab` | `4` | Spaces per tab |
| `maxLineWidth` | `999` | Max line width before wrapping |
| `uppercaseKeywords` | `true` | Uppercase SQL keywords |
| `standardizeKeywords` | `true` | Normalize keyword synonyms (e.g. `NATIONAL CHARACTER VARYING` → `NVARCHAR`) |
| `expandCommaLists` | `true` | Expand comma lists onto separate lines |
| `expandInLists` | `true` | Expand IN (...) lists |
| `trailingCommas` | `false` | Trailing commas instead of leading |
| `expandBooleanExpressions` | `true` | Expand AND/OR onto separate lines |
| `expandCaseStatements` | `true` | Expand CASE/WHEN/THEN/END |
| `expandBetweenConditions` | `true` | Expand BETWEEN ... AND ... |
| `breakJoinOnSections` | `false` | Break JOIN clauses |
| `newStatementLineBreaks` | `2` | Blank lines between statements |
| `newClauseLineBreaks` | `1` | Blank lines between clauses |

## Example

Input:
```sql
select e.employeeid,e.firstname,e.lastname,d.departmentname from employees e inner join departments d on e.departmentid=d.departmentid where e.active=1 order by e.lastname
```

Output (default settings):
```sql
SELECT
    e.EmployeeId
    ,e.FirstName
    ,e.LastName
    ,d.DepartmentName
FROM employees e
INNER JOIN departments d
    ON e.DepartmentId = d.DepartmentId
WHERE e.Active = 1
ORDER BY e.LastName
```

## Packaging

To produce a `.vsix` for manual install:

```bash
cd vscode-extension
npm run package
# Produces: right-way-sql-formatter-0.1.0.vsix
```

Install with:
```bash
code --install-extension right-way-sql-formatter-0.1.0.vsix
```
