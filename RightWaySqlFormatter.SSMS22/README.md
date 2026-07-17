# Right Way SQL Formatter — SSMS 22 Extension

[![License: AGPL-3.0-or-later](https://img.shields.io/badge/License-AGPL--3.0--or--later-blue)](https://github.com/BadMonkeySoftware/right-way-sql-formatter/blob/main/LICENSE.txt)
[![GitHub](https://img.shields.io/badge/Source-GitHub-181717?logo=github)](https://github.com/BadMonkeySoftware/right-way-sql-formatter)

Format T-SQL inside SQL Server Management Studio 22 — the classic "Poor Man's T-SQL Formatter" style, modernized. The same engine powers the
[VS Code extension](https://marketplace.visualstudio.com/items?itemName=BadMonkeySoftware.rightway-sql-formatter) and a CLI, battle-tested against 396 real-world SQL files (First Responder Kit, Ola Hallengren, sp_WhoIsActive, DarlingData, tSQLt).

![Formatting a messy T-SQL query](https://github.com/BadMonkeySoftware/right-way-sql-formatter/raw/main/vscode-extension/images/demo.gif)

## What you get in SSMS

- **Format Document / Format Selection** from the Tools menu, the query editor context menu, or **Ctrl+K, F**.
- **Options dialog** (Tools ▸ Right Way SQL Formatter Options) with grouped settings — from classic SSMS defaults to trailing commas, vertical alignment of columns and JOINs, and compact single-statement blocks. Settings persist across SSMS restarts.
- **Never destroys invalid SQL** — unparseable input still gets best-effort formatting plus a diagnostic comment describing what's wrong, never a silent failure.
- **Your style, preserved** — both `expr AS alias` and `alias = expr` column alias styles are kept as you wrote them.

![Formatter menu items in SSMS 22](https://github.com/BadMonkeySoftware/right-way-sql-formatter/raw/main/docs/images/ssms22-menu.png)

![Options dialog](https://github.com/BadMonkeySoftware/right-way-sql-formatter/raw/main/docs/images/ssms22-options.png)

## Installation

SSMS 22 has no Extension Manager UI, but installs VSIXes directly — **no admin rights needed**:

1. Download the `.vsix` from this page.
2. Close SSMS.
3. Run it with SSMS's own installer:

   ```
   "C:\Program Files\Microsoft SQL Server Management Studio 22\Release\Common7\IDE\VSIXInstaller.exe" RightWaySqlFormatter.SSMS22.vsix
   ```

   (Double-clicking the file routes to Visual Studio's installer if VS is installed, which also works — make sure "SQL Server Management Studio 22" is ticked.)
4. Start SSMS — the commands appear under Tools and in the query editor right-click menu.

To uninstall:

```
"C:\...\SSMS 22\Release\Common7\IDE\VSIXInstaller.exe" /uninstall:e857c020-26ea-4b6f-b0d0-d97fb572ee81
```

## Which SSMS versions?

| SSMS | Build |
|---|---|
| 22+ | **This extension** |
| 17–20 | [SSMS18 build](https://github.com/BadMonkeySoftware/right-way-sql-formatter#ssms-plugin) (manual install; see repo) |

Note: Microsoft does not officially support extensions in SSMS 21/22 — an SSMS update may require reinstalling the extension.

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

## Source, issues, license

Free and open source under [AGPL v3](https://github.com/BadMonkeySoftware/right-way-sql-formatter/blob/main/LICENSE.txt), forked from [PoorMansTSqlFormatter](https://github.com/TaoK/PoorMansTSqlFormatter) by Tao Klerks. Report issues and find the CLI + VS Code extension at
[github.com/BadMonkeySoftware/right-way-sql-formatter](https://github.com/BadMonkeySoftware/right-way-sql-formatter).
