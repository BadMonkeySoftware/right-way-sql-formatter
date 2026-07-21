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

## Width-wrap fixed-point work (2026-07-19; docs/width-wrap-fixed-point-task.md)

**Mechanism correction — the task's three named suspects were all wrong.** The
width-wrap non-idempotency is NOT input physical-layout leaking via
`SourceBreakPending` (verified inert for word tokens — it only forces a break
before a *comment*), NOT boundary arithmetic, and NOT continuation-line
indentation. The real, unified mechanism:

> **The core tree-walk makes its max-line-width wrap decision FIRST; then a text
> post-pass LENGTHENS the same line** — `AlignFromJoinClauses` inserts an `as`
> keyword, `EnsureColumnAliases`/`RewriteAliasesToEqualSign` insert a
> `ColumnAlias_N = ` prefix — **so pass 1 wraps against a width the final line
> won't have.** Once formatted, the inserted tokens are present in the input, so
> pass 2 (and every pass after) wraps the *longer* line and settles on a
> different, stable layout. These files are therefore a **one-time raw→formatted
> transient (they converge at pass 2)**, not the "flips forever" the task title
> assumed. (A true period-2 minority does exist — see oscillators below.)

Traced to the exact 2-char divergence: raw `INNER JOIN @x alias` (no `as`) wraps
`REPLACE(REPLACE(…),'_','[_]')` before `'[_]'`; after the align pass adds ` as `,
the with-`as` line wraps 2 chars earlier (before `'_'`). Both layouts are
internally stable; only the raw→formatted step differs.

### Fixed this round (heavy-profile UNSTABLE 25 → 18)

- **Overflowing single-literal alias value** — ✅ **FIXED** (`WrapOverflowingAliasLiterals`,
  tests in `WidthWrapFixedPointTests`). When a post-pass assembles `alias = <lone
  string/binary literal>` that overflows `MaxLineWidth`, the core would break
  immediately before the (unbreakable) literal, dropping it to its own line at the
  column indent. The new pass reproduces that break at assembly time, matching the
  core's length accounting (padding + separator spaces are uncounted — measure from
  the trimmed LHS + `=`). Additive-safe: only fires on overflow, default output
  byte-identical. Cleared ProtectSession, sp_HumanEvents, sp_BlitzBackups,
  Deprecated/sp_Blitz_SQL_Server_2005.
- **Stranded trailing space at a width break** — ✅ **FIXED** (core `AddOutputContent`
  drops the just-emitted word-separator space when it wraps before an over-long
  token; `WrapOverflowingAliasLiterals` and `AlignBlockEqualSign` emit a bare `=`
  for a wrapped value, consistently). Cleared sp_QuickieStore, sp_PerfCheck,
  tSQLt.Private_ProcessTestAnnotations. **Jeremy-approved default-behavior change:**
  this alters default-profile output on 38 corpus files — verified **trailing-
  whitespace-only** (zero non-whitespace changes; makes default *more* idempotent)
  — and required one sanctioned regen (`04_MiscProceduralSample_Unstructured__
  NoExpandComma_Width60_Tab8.sql`, line 18 lost a stranded wrap-point space).

### Still UNSTABLE (18) — classified, NOT fixed (would require the wrapping maze)

All share the mechanism above (post-pass lengthens a core-wrapped line) but the
wrapped tail is a *multi-token expression*, so reproducing the core's break means
re-implementing per-token width accounting inside a text pass — the "wrapping
maze" the core author flagged (`TODO: find a way out of the cross-dependent
wrapping maze`). Left deliberately unfixed:

- **JOIN `as`-insertion (4 — DatabaseBackup, DatabaseIntegrityCheck, IndexOptimize,
  MaintenanceSolution; all the shared Ola availability-group `REPLACE(REPLACE(…))`
  pattern).** conv@p2. The wrapped `'[_]')` continuation breaks *out* of the align
  block (doesn't start with AND/OR), so `AlignFromJoinBlock` can't rebalance it.
  (Rejected: making the core emit `as` for JOIN aliases — core alias-policy change,
  shifts wrap in every AlignTableJoins expected file, blast radius ≫ 4 files.)
- **Multi-token alias RHS wrap (4 — sp_BlitzFirst `= 'On the ' + case…`,
  Private_RemoveSchemaBoundReferences `+ ';'`, sp_BlitzPlanCompare, sp_Blitz
  trailing `;`).** conv@p2. Same as the fixed single-literal case but the RHS is a
  concat/CASE expression; the core breaks mid-expression, not before one token.
- **True period-2 oscillators (4 — sp_BlitzCache, sp_BlitzIndex, sp_BlitzQueryStore,
  Deprecated/sp_BlitzIndex_SQL_Server_2005).** Never converge (p2≠p3). A CASE-arm /
  padded-string-list concat sits exactly on the 200-col boundary and flips each pass.
- **Singletons (6 — Vector Defense ×3 `WITH … APPROXIMATE ve.Id` alias split;
  sp_IndexCleanup JOIN-align × multi-line comment on the ON line; ParsingDisaster
  malformed `[A = 1` (deliberately invalid input); Run_Methods FOR XML `*` column ×
  comment re-indent).** conv@p2 except ParsingDisaster (oscillates).

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

---

## Discovery: final idempotency task (docs/final-idempotency-task.md) — DONE (2026-07-21)

**Heavy sweep UNSTABLE 18 → 0** (0 FATAL / 2 known PARSEWARN: ChangeDbAndExecuteStatement,
AnnotationParser). Re-running the sweep first paid off — the classification shrank on contact
exactly as warned:

- The "18" was measured with the installers skipped; a fresh 3-pass temp-file sweep found
  **21** (the 18 + the 3 giant installers, which just BUNDLE the unstable procs).
- The "period-2 oscillators (4)" were **not** period-2. Six files
  (sp_BlitzIndex, sp_BlitzCache, sp_BlitzQueryStore, sp_BlitzIndex_2005 + 2 installers) showed
  **unbounded, non-convergent drift** (8 passes → 8 distinct hashes, bytes growing every pass) —
  and it was a **correctness bug**, not cosmetic: string-literal CONTENT was being corrupted.

### Root cause #1 — AlignDdlColumns padded INSIDE string literals (correctness bug, FIXED)
`CREATE TABLE` computed columns (`index_definition AS CASE … N'[DISABLED] ' … END`) wrap; the
wrapped **continuation lines** were mistaken for new column definitions by `AlignDdlColumns`,
which tokenizes on whitespace (`ExtractFirstToken` stops at the first space). When a continuation
began with a string literal (`N'EXEC dbo…'`), it split the token at the space INSIDE the literal
and injected alignment padding there — `N'EXEC dbo…'` → `N'EXEC          dbo…'`, growing ~40
spaces every pass. The `LineTouchesStringOrComment` mask only guards MULTI-line strings; a
single-line literal has `mask[i]=mask[i+1]=false`. **Fix:** skip any candidate line whose first
token contains a `'` (a real column name never does). TSqlStandardFormatter.cs `AlignDdlColumns`.
Provably de-corruption-only (the guard only ADDS a `continue`). Also fixed latent (idempotent-
but-corrupt) string bloat in sp_HumanEvents/sp_HealthParser/sp_WhoIsActive. Dropped 21→18 and
converted the 6 drifters to convergent/idempotent. Tests: `DdlAlignScopeTests`
(ComputedColumnWrappedStringLiteralIsNeverPaddedInside, ComputedColumnWrappedStringExprIsIdempotent).

### Root cause #2 — EnsureColumnAliases doubled on an unclosed bracket (FIXED)
tsqlt `ParsingDisaster` (deliberately malformed `SELECT 1 AS [A`, the `[A` pairing with a later
`]`) is exit-0 but period≥3: `[A = 1` → `[A = 1 = [A = 1` → … unbounded. The unclosed `[` makes
the alias LHS/expr split ambiguous, so `EnsureColumnAliases` re-aliased it every pass. **Fix:**
`EnsureAlias` leaves a column untouched when it has an unclosed `[` outside strings
(`HasUnclosedBracketOutsideString`) — well-formed columns always close brackets, so valid SQL is
unaffected (idempotent by construction). Test: `AliasPassCorruptionTests.UnclosedBracketColumn_IsIdempotent_NoAliasStacking`.

### The remaining 17 were genuinely cosmetic — Plan B (fixed-point reformat)
After the two source fixes, all 17 remaining files were **period-1, converge@p2, and
token-identical p1→p2** (collapse-whitespace equal — pure wrap/indent drift, the "wrapping maze"
this doc's earlier sections deferred). With no period-3+ cycle left in the corpus, F∘F is
idempotent everywhere. Implemented **Plan B as internal always-on**: `FormatSQLTree`, after the
text post-passes, reformats once IF the post-passes actually changed the output (bounded by
`_inReformatPass`). Guarded so the DEFAULT profile — whose gated passes never run, leaving output
unchanged here — never reformats and stays byte-for-byte identical at zero cost. Test:
`WidthWrapFixedPointTests.JoinAsInsertion_ReachesFixedPoint` (verified fail-before by toggling the
reformat off). **Plan B papers over (does not root-cause-fix) these families:** JOIN-`as`
insertion (Ola DatabaseBackup/DatabaseIntegrityCheck/IndexOptimize/MaintenanceSolution),
multi-token alias RHS (sp_Blitz/sp_BlitzFirst/sp_BlitzPlanCompare/Private_RemoveSchemaBoundReferences),
and the Vector-Defense/sp_IndexCleanup/Run_Methods/sp_BlitzIndex/installer wrap oscillations.

**⚠ FOR JEREMY:** Plan B is a deliberate behavior change under post-pass-active profiles
(heavy/AlignEquals): previously-unstable files now emit their pass-2 fixed point. Default is
untouched (proven byte-identical). Cost: heavy formatting does ~2 pipeline passes →
**1.68× on the worst case** (same-binary Plan-B off vs on, Install-All-Scripts 2 MB heavy:
1.67 s → 2.81 s), within the task's ≤2× expectation. It is internal and always-on (no new
user-visible option, per the task). If you'd rather it be behind a flag or off by default, say so.

### Verification
Suite 705/0/26; **DEFAULT profile byte-identical** (the hard requirement — proven on the corpus
+ all InputSql); CorpusOracle green; heavy sweep 0 UNSTABLE / 2 PARSEWARN / 0 FATAL; no
previously-stable file destabilized. AlignEquals profile changed on 4 inputs
(01_SimpleTypes_StrangeRules, 23_ReparsingInconsistency via Plan B; DarlingData, sp_HumanEvents
via the AlignDdlColumns de-corruption) — all now idempotent, and NONE has an AlignEquals-configured
expected file (their expected outputs use the default config), so the suite is unaffected. Harness:
`tools/memprofile` (hash/depth) + a 3-pass temp-file `heavy-sweep.sh`.
