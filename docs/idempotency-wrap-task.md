# Task: fix heavy-profile idempotency — width-wrap oscillation around string literals

## Goal

`format(format(x))` must equal `format(x)`. Under the "heavy editor" option profile,
51 of 393 real-world corpus files are still non-idempotent (UNSTABLE): reformatting
already-formatted output produces different output, drifting again on each pass.
The dominant cause is **max-line-width wrap-point oscillation on lines that contain
or border multi-line string literals**. Reduce the UNSTABLE count as far as possible
without breaking anything that currently passes.

Read CLAUDE.md first — all its rules apply. The non-negotiables:

- Never modify expected files in `RightWaySqlFormatter.Tests/Data/` without Jeremy's
  explicit sign-off. If a fix legitimately changes expected output, STOP and ask;
  regeneration happens only via the `ExpectedOutputRegenerator` explicit test.
- `dotnet test RightWaySqlFormatter.NoSSMS.slnx` must finish 0 failed before any
  task is called done. Current baseline: **703 total / 0 failed / 26 skipped**
  (counts vary slightly by runner; failures must be zero). The 26 skips are fixed:
  2 historical + 24 ScriptDom-oracle ignores (8 inputs × 3 profiles).
- Formatting changes are additive-only: default-profile output must not change.
- TDD: reproduce each bug in a small NUnit regression test before fixing it.

## Background you don't need to rediscover

Pipeline: tokenizer → forgiving parser → `TSqlStandardFormatter` walks the tree,
then **text-based post-passes** run at the end of `FormatSQLTree` (in
`RightWaySqlFormatter/Formatters/TSqlStandardFormatter.cs`): `EnsureColumnAliases`,
`RewriteAliasesToEqualSign`, `AlignSelectColumns`, `AlignFromJoinClauses`, plus
`AlignDdlColumns` applied during tree walk.

A ScriptDom-based oracle (`ScriptDomValidationTests`) already enforces, on every
test run, that formatted AND re-formatted output parses under the real T-SQL
grammar for three profiles: Default, AlignEquals, HeavyEditor. Those all pass —
your changes must keep them passing. The remaining problem is purely *textual
stability*, not validity.

Recently fixed (don't re-diagnose; regression tests exist): align/alias passes
firing on non-table-source FROMs, TVF-call content loss, derived-table/`@var`/
hint aliasing, CASE-arm continuations aliased, `'str' = expr` double-aliasing,
DDL alignment rewriting INSERT/VALUES parens (padded inside string literals),
`TOP (nested(calls))` severed at first `)`, aliases appended after trailing `--`
comments (both AS and `=` styles), AS-less bracketed aliases (`Name [x y]`).
See `DdlAlignScopeTests`, `ColumnAliasTopModifierTests`, `ColumnAliasEdgeTests`,
`ScriptDomValidationTests` for the patterns.

## The bug to fix now

Repro profile (Jeremy's real editor profile — use exactly this):

```
--expand-between=false --expand-boolean=false --expand-case=false
--expand-in-lists=false --uppercase-keywords=false --standardize-keywords=false
--trailing-commas=true --align-table-joins=true --column-always-has-alias=true
--select-first-column-newline=true --align-columns=true --align-ddl-columns=true
--alias-style=equals --indent-where-and-or=true --max-line-width=200
--compact-raiserror=true --compact-single-statement-blocks=true
```

Two-pass check (always via files, never stdin piping):

```bash
dotnet build -c Release RightWaySqlFormatter.CmdLine/RightWaySqlFormatter.CmdLine.csproj
FMT=RightWaySqlFormatter.CmdLine/bin/Release/net10.0/SqlFormatter
$FMT $PROFILE input.sql > p1.sql
$FMT $PROFILE p1.sql    > p2.sql
diff p1.sql p2.sql        # must be empty
```

Known failing corpus file (fetch via `tools/realworld-test.sh`, which clones the
corpus into `realworld-results/corpus/`): `first-responder-kit/sp_kill.sql`.
Observed oscillation around line 503:

```
pass1:
            else N'' end + case when @EmergencyMode is null then 
                    N',
pass2:
            else N'' end + case when @EmergencyMode is null then N',
```

The line is near the 200-char MaxLineWidth limit and the next token opens a
multi-line `N'...'` literal. Pass 1 breaks before the literal; pass 2, seeing the
already-broken shorter line, joins it back — and the two layouts flip on every
subsequent pass. The same signature appears in `maintenance-solution/*.sql`
(DatabaseBackup, IndexOptimize…) and several DarlingData files, e.g. diffs
starting at `set @Cmd =` or inside `OPTION (RECOMPILE, MAXDOP ' + cast(`.

Start by grepping `MaxLineWidth` in `TSqlStandardFormatter.cs` to find the wrap
decision points (line-length accounting and forced-break logic in the formatting
state). The core question to answer during diagnosis: **why does the measured
line length (or the break decision) differ between pass 1 and pass 2 when a
multi-line string literal is involved?** Likely suspects, verify before fixing:
how the width accounting counts a token whose string literal spans lines (the
first fragment's length vs the whole token), and how a source line break already
present in pass-1 output (`SourceBreakPending` / white-space handling) interacts
with the width check on re-parse.

The fix must make the wrap decision reach a FIXED POINT: whatever layout pass 1
emits, pass 2 must re-derive the same layout. Either direction (always break
before a multi-line literal near the limit, or never re-join one) is acceptable
as long as it is stable, deterministic, and default-profile output is untouched
(default has `--max-line-width=999`, so most default output never wraps — but
prove it with the full suite).

## Secondary targets (if wrap-fix stalls or finishes early)

Remaining smaller UNSTABLE singles, visible in sweep diffs:

- `ColumnAlias_1 = APPROXIMATE ve.Id, …` (DarlingData Vector files) — something
  mis-splits `APPROXIMATE` from the expression when aliasing.
- tsqlt files with `[A = 1` style diffs — bracket/equals interplay in align passes.
- Comment-position drift: `/* ˅˅˅˅˅ … */` indentation walks right each pass
  (darling-data `Isolation Levels.sql`).

Each of these should get its own minimal repro + regression test, same as the
recent fixes.

## Discovered while fixing the wrap oscillation (2026-07-18) — NOT yet fixed

The width-wrap hypothesis was only ~5/51 of the UNSTABLE set. After fixing it
(see below), the remaining 46 split into two distinct families, neither of which
is width-wrap. Both live in the text post-passes, not the core walk:

1. **Align/alias post-pass corrupts WRAPPED expression continuation lines** —
   ✅ **FIXED 2026-07-18** (see `docs/corpus-oracle-task.md`; regression tests in
   `AliasPassCorruptionTests`). A corpus-wide oracle (`ScriptDomValidationTests.
   CorpusOracle`) was added and driven from **188 → 0** parse failures. This was
   NOT one family — it was ~8 distinct textual mis-parses in `EnsureColumnAliases`
   / `RewriteAliasesToEqualSign` / `AlignSelectColumns`: AS-less plain & string-
   literal aliases re-aliased; wrapped RHS / concat / CASE continuations aliased as
   columns; keyword-named aliases needing brackets; `@var` self-aliased; `+=` split
   into `+ =`; `]]` escaped-bracket aliases; trailing block comments glued to the
   alias; `FROM::fn_`/`FROM(` clause glue; function-call/member-access width splits;
   and DDL `CREATE/ALTER USER|LOGIN|ASSEMBLY … FROM`. All fixed by giving the three
   passes shared continuation detection (paren/CASE depth, prev-line-ends-open,
   next-line-continues, leading-token) and hardening the alias predicates. The
   original hunch that it was one "inject alias `=` into a wrapped CASE" family was
   only ~4 of the 188. Fixing this also dropped the heavy-profile UNSTABLE 46 → 35.

2. **Comment-position drift** — ✅ **FIXED 2026-07-18** (see
   `docs/comment-drift-task.md`; regression tests in `CommentDriftTests`).
   `AlignDdlColumns` treated comment lines as column definitions and padded them:
   single-line `/* */`/`--` comment-only lines (the `Isolation Levels` `˅˅˅˅˅`
   ratchet, sp_WhoIsActive param-doc `--` lines) AND interior lines of multi-line
   `/* … */` comments (sp_BlitzCache) — it lacked the `ComputeLinesInsideStringOrComment`
   mask the other align passes use. Also `EnsureAlias`/`TryRewriteColumnLine`
   re-attached a trailing `-- comment` with a leading space even after a `;`, so
   `expr; --c` drifted to the core's `expr;--c`. Heavy-profile UNSTABLE 35 → 25.

## Still UNSTABLE after comment-drift fix (2026-07-18) — NOT comment drift

The 25 remaining heavy-profile UNSTABLE files are two other families, both
distinct from comment drift and left for a future pass:

- **Width-wrap fixed-point oscillation (~16 files, the bulk — sp_Blitz,
  sp_BlitzCache, sp_BlitzFirst, DatabaseBackup, MaintenanceSolution, IndexOptimize,
  DatabaseIntegrityCheck, sp_HumanEvents, ProtectSession, the Deprecated Blitz
  procs, …).** A function-call argument / concat string / trailing `;` near the
  200-col limit flips between end-of-line and start-of-next-line each pass
  (`replace(…), '_',` ⏄ `'_', '[_]')`; `Details = '…'` ⏄ trailing `;`). The core
  width accounting does not reach a fixed point when a wrapped element sits right
  at the boundary — a different mechanism from the CRLF driver fixed earlier.
- **Trailing whitespace at wrap points (~5 files — sp_BlitzIndex, sp_PerfCheck,
  QuickieStore, Run_Methods, tSQLt.Private_ProcessTestAnnotations).** The core
  leaves a trailing space when it wraps after a `,`/word separator; the next pass
  strips it. A global trailing-WS strip is **NOT safe** — 30 `StandardFormatSql`
  expected files contain intentional trailing whitespace, so it needs sign-off.
- Two singletons: the `WITH … APPROXIMATE ve.Id` vector-search alias split
  (DarlingData Vector Defense, 3 files) and the malformed `[A = 1` unclosed-bracket
  case (`tsqlt/ParsingDisaster.sql`); plus `sp_IndexCleanup` (a JOIN-align ×
  multi-line-comment-on-the-ON-line interaction).

3. **`+`-operator wrap oscillation under lighter profiles (fixed as a
   side effect).** Under the AlignEquals profile, `str' + @var + N'...` used to
   flip `@var` between its own line and joined. This shared the CRLF root cause
   and is now stable, but noted here in case a non-CRLF variant resurfaces.

## Verification checklist (all required)

1. New regression tests for each fixed pattern, failing before / passing after.
2. `dotnet test RightWaySqlFormatter.NoSSMS.slnx` → 0 failed. If ANY
   `StandardFormatExpectedOutput` test fails, stop and show Jeremy the diff —
   that's an expected-file change needing sign-off.
3. Corpus sweep with the heavy profile: `PROFILE='<flags above>'
   tools/realworld-test.sh`. Compare against the current baseline:
   **0 FATAL / 2 PARSEWARN (known-legit: one SQLCMD-mode file, one intentional
   unclosed comment in tsqlt) / 51 UNSTABLE**. Success = UNSTABLE materially
   lower, no new FATAL or PARSEWARN, and no previously-stable file destabilized.
   Note: three huge single-file installers (darling-data `Install-All/DarlingData.sql`,
   first-responder-kit `Install-Azure.sql` + `Install-All-Scripts.sql`) need
   several GB of RAM under this profile (known, separate superlinear-memory
   issue) — skip them if memory-constrained and say so in the report.
4. Also run one AlignEquals-profile spot check (`--align-columns=true
   --alias-style=equals --column-always-has-alias=true`) on a couple of fixed
   files to confirm the fix isn't heavy-profile-specific.

## Report back

End with: what oscillated and why (root cause, file/line), what changed, new
tests added, before/after UNSTABLE counts, and anything discovered but
deliberately not fixed (add it to the list above rather than scope-creeping).
