# AGENTS.md — right-way-sql-formatter

Agent-specific rules for working in this repository.

---

## Test Expected Output Files

*Never modify expected output files without explicit sign-off from Jeremy.*

Expected output files live in `PoorMansTSqlFormatterTest/Data/StandardFormatSql/`, `ParsedSql/`, and `CmdLineSql/`. These files define what correct formatter behavior looks like. Changing them to make tests pass is how bugs get hidden.

### Rules

- If a test fails because the formatter behavior changed **intentionally** (e.g. a bug was fixed), flag the expected file changes to Jeremy and wait for approval before committing them.
- If the formatter is producing **wrong output**, fix the formatter — do not update the expected file to match wrong output.
- When expected files legitimately need updating (e.g. a new feature adds new expected output), use the `ExpectedOutputRegenerator` explicit test (`REGEN_FILES="file1.sql;file2(Option=True).sql" dotnet test RightWaySqlFormatter.NoSSMS.slnx --filter "Name~RegenerateExpectedFiles"`) — it drives the formatter library directly. Do not hand-write expected output; hand-written expected files have been wrong before.
- Every commit that touches expected files must explain in the commit message *why* the expected output changed and *what formatter behavior changed*.

### How to regenerate expected files correctly

Use the formatter library directly, not the CLI — CLI defaults may differ from what the test harness uses. The test harness builds a `TSqlStandardFormatter` from `TSqlStandardFormatterOptions(configString)` where `configString` is the `(...)` portion of the filename.

---

## General Coding Rules

- Run `dotnet test` before marking any task done. All tests must pass (baseline: 347 total, 0 failed, 2 skipped).
- Do not suppress or skip tests without approval.
- This is a personal project — no deadlines, but code quality matters. Don't cut corners.
- SSMS plugin tasks require a Windows build environment and cannot be executed on this Mac dev machine — flag these explicitly rather than guessing.
- Preserve existing SQL formatting behavior. New options are additive only.
