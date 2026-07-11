# Right Way SQL Formatter — VSCode Extension

VS Code extension that formats T-SQL using the `SqlFormatter` CLI.

## How it works

The extension shells out to the `SqlFormatter` binary (built from the .NET project in this repo).
SQL is piped via stdin, formatted output comes back on stdout.
No language server, no parsing in JS — all formatting logic lives in the .NET core library.

## Commands

| Command | Keyboard Shortcut | Description |
|---|---|---|
| Right Way SQL: Format Document | — | Formats the entire active SQL file (minimal edits) |
| Right Way SQL: Format Selection | — | Formats only the selected text |
| Right Way SQL: Format Document (Preview) | — | Opens a native diff (current ↔ formatted) with Apply/Discard |

All commands are also available in the right-click context menu when editing a `.sql` file. The extension additionally registers as the document formatter for SQL, so `Format Document` (⇧⌥F / Shift+Alt+F) and format-on-save work too.

### Diff preview

`Format Document (Preview)` shows the proposed formatting in VS Code's built-in diff editor — green/red change highlighting, side-by-side or inline — before anything touches your file. Choose **Apply** in the prompt to apply (formatting re-runs against the document's current text, so it's safe even if you kept typing), or **Discard** to close the preview unchanged.

### Minimal edits

Formatting is applied as line-level minimal edits (computed with an LCS diff) rather than replacing the whole document: cursor position survives, undo is a single clean step, and unchanged lines are untouched. Replacement text uses the document's own line endings.

### Invalid SQL

If the input can't be fully parsed, the formatter still produces best-effort output, prefixed with a comment describing what's wrong (e.g. unclosed string literal, unexpected token), and the extension shows a warning toast. The underlying CLI signals this with exit code 5.

## Setup

### 1. Build everything

From the `vscode-extension/` directory:

```bash
npm install
npm run build
```

That's it. `npm run build` will:
- Find the .NET SDK automatically (checks `~/.dotnet`, system locations, then PATH)
- Publish a self-contained `SqlFormatter` binary into `vscode-extension/bin/` — no .NET required at runtime
- Compile the TypeScript extension

### 2. Install the extension (dev mode)

Open the **`vscode-extension/` folder** in VS Code (not the repo root — the folder itself).

Then press `F5`. VS Code will launch an Extension Development Host window with the extension loaded.
Open any `.sql` file in that window and right-click to format.

> Tip: You need to open the `vscode-extension/` folder directly. If you open the repo root, `F5` won't find the extension manifest.

### 3. Package for install

```bash
npm run package
# Produces: right-way-sql-formatter-0.1.0.vsix
```

Install with:
```bash
code --install-extension right-way-sql-formatter-0.1.0.vsix
```

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
