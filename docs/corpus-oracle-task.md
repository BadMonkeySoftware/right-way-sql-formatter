# Task: make the corpus-wide ScriptDom oracle pass (fix align/alias corruption of wrapped expressions)

## Goal

The `[Explicit]` test `ScriptDomValidationTests.CorpusOracle` formats every
real-world corpus file under all three validation profiles (Default,
AlignEquals, HeavyEditor) and asserts that both the formatted output AND the
re-formatted output still parse under the real T-SQL grammar (ScriptDom).
It currently FAILS: the align/alias text post-passes corrupt wrapped
expression continuation lines into semantically-changed or invalid SQL
(~33 corpus files under the heavy profile). **Run it, fix every failure it
reports, leave it green.**

```bash
# corpus checkout (one-time; clones public SQL repos into realworld-results/corpus)
tools/realworld-test.sh   # ok to Ctrl-C after the clone step finishes
# the oracle
dotnet test RightWaySqlFormatter.NoSSMS.slnx --filter "Name~CorpusOracle"
```

Read CLAUDE.md first. Non-negotiables:

- Never modify expected files under `RightWaySqlFormatter.Tests/Data/` without
  Jeremy's explicit sign-off — if a fix changes any
  `StandardFormatExpectedOutput` result, STOP and show the diff.
- Full suite `dotnet test RightWaySqlFormatter.NoSSMS.slnx` → 0 failed before
  done. Baseline: **704 total / 0 failed / 26 skipped** (sandbox counter; Mac
  discovers ~706). Do not weaken, skip, or exclude anything to get green —
  no filtering files out of CorpusOracle, no loosening its assertions.
- Additive-only: default-profile output must not change (default runs none of
  the align/alias passes, so this holds unless you touch shared code).
- TDD: each distinct corruption pattern gets a minimal NUnit repro test first.

## The known bug family (root cause already diagnosed — verify, then fix)

When a long expression is wrapped across physical lines (max-line-width, or
source line breaks preserved inside expressions), the SELECT-list text passes
treat each continuation line as a standalone column:

- `AlignSelectColumns` / `AlignBlockEqualSign` inject a spurious `alias =`
  into the middle of a wrapped CASE. Concrete, from maintenance-solution
  `DatabaseBackup.sql` under the heavy profile:
  `case when left(DatabaseItem,1) = '[' and right(...) = ']' then …` becomes
  `case when left(DatabaseItem,1) = '['` / `DatabaseItem = and right(...) = ']'`
  — invalid SQL.
- `RewriteAliasesToEqualSign` double-processes: `as ColumnAlias_1 = substring(…)`
  → `ColumnAlias_1 = as ColumnAlias_1 = …`.
- Same family: the `APPROXIMATE ve.Id` mis-split (DarlingData Vector files) and
  tsqlt `[A = 1` diffs.

Heavy-profile repro flags (Jeremy's real profile):

```
--expand-between=false --expand-boolean=false --expand-case=false
--expand-in-lists=false --uppercase-keywords=false --standardize-keywords=false
--trailing-commas=true --align-table-joins=true --column-always-has-alias=true
--select-first-column-newline=true --align-columns=true --align-ddl-columns=true
--alias-style=equals --indent-where-and-or=true --max-line-width=200
--compact-raiserror=true --compact-single-statement-blocks=true
```

Fix direction: `EnsureColumnAliases` already tracks `parenDepth` and
`caseDepth` and skips AND/OR/THEN/WHEN/ELSE/operator-leading continuation
lines — that machinery (added recently, see its use of `CountNetParens` /
`CountNetCase`) is the model. `AlignSelectColumns`, `AlignBlockAsKeywords`,
`AlignBlockEqualSign`, and `RewriteAliasesToEqualSign` have NO equivalent:
they must learn to recognize a continuation line (cumulative unbalanced
parens/CASE since the block started, and/or continuation-leading tokens) and
leave it — and the alignment column math — untouched. Factor shared detection
rather than four divergent copies if practical.

All post-passes live in `RightWaySqlFormatter/Formatters/TSqlStandardFormatter.cs`.
IMPORTANT: the passes were just converted to preserve per-line endings
(`SplitLinesPreservingEndings`/`JoinLinesPreservingEndings`) — CRLF inside
multi-line string literals is data; keep it intact.

## Verification (all required)

1. Regression tests per fixed pattern (small SQL snippets, not corpus files),
   failing before / passing after.
2. `dotnet test … --filter "Name~CorpusOracle"` → green (report how many files
   judged). Memory note: three huge installers (darling-data
   `Install-All/DarlingData.sql`, first-responder-kit `Install-Azure.sql`,
   `Install-All-Scripts.sql`) need several GB RAM under HeavyEditor — on a
   memory-constrained machine, report them as unrunnable rather than failing.
3. Full suite → 0 failed, no expected-file diffs.
4. Heavy-profile idempotency sweep (`PROFILE='<flags above>'
   tools/realworld-test.sh`): current baseline **0 FATAL / 2 known PARSEWARN /
   46 UNSTABLE**. Expect UNSTABLE to drop substantially (the corruption family
   is ~33 of the 46); it must not rise, no new FATAL/PARSEWARN, no
   previously-stable file destabilized.
5. Report: each corruption pattern found (file + minimal repro), the fix, new
   test names, CorpusOracle judged/failed counts before and after, and the
   UNSTABLE delta. Anything discovered but out of scope: append to
   `docs/idempotency-wrap-task.md`'s discovery section, don't fix silently.
