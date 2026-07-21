# Task: full heavy-profile idempotency — the last 18 UNSTABLE files

## Goal

Drive the heavy-profile corpus idempotency sweep from **18 UNSTABLE → 0**.
Every remaining file is cosmetic drift/oscillation (validity is a solved,
CI-enforced invariant), classified in `docs/idempotency-wrap-task.md`:

- **JOIN `as`-insertion (4):** DatabaseBackup, DatabaseIntegrityCheck,
  IndexOptimize, MaintenanceSolution (shared Ola `REPLACE(REPLACE(…))`
  pattern). `AlignFromJoinClauses` inserts `as`, lengthening a line past
  MaxLineWidth; the wrapped continuation then falls outside the align block
  on the next pass, so the block can't rebalance to a fixed point.
- **Multi-token alias RHS wrap (4):** sp_Blitz, sp_BlitzFirst,
  sp_BlitzPlanCompare, Private_RemoveSchemaBoundReferences. Like the fixed
  lone-literal case (`WrapOverflowingAliasLiterals`) but the RHS is a
  concat/CASE expression — the core breaks mid-expression, and the post-pass
  can't currently reproduce that break.
- **True period-2 oscillators (4):** sp_BlitzCache, sp_BlitzIndex,
  sp_BlitzQueryStore, Deprecated/sp_BlitzIndex_2005 — CASE-arm/padded string
  list sitting exactly on the 200-col boundary flips sides every pass.
- **Singletons (6):** Vector Defense ×3 (`WITH … APPROXIMATE ve.Id` alias
  mis-split), sp_IndexCleanup (JOIN-align × multi-line comment on the ON
  line), ParsingDisaster (`[A = 1` — input deliberately malformed; stability
  still required, validity not), Run_Methods (FOR XML `*` × comment
  re-indent).

Re-run the sweep first and re-verify this classification — every prior task's
premise shrank on contact with the code; assume this one will too.

Read CLAUDE.md. Non-negotiables:

- Expected files in `RightWaySqlFormatter.Tests/Data/` untouched without
  Jeremy's sign-off — STOP and show the diff if any
  `StandardFormatExpectedOutput` fails. Reminder: a global trailing-
  whitespace strip remains OFF-LIMITS (30 expected files carry intentional
  trailing whitespace; that policy call is Jeremy's, separately).
- Full suite → **0 failed, exactly 26 skipped** (totals grow as tests are
  added). `CorpusOracle` stays green. Default-profile output byte-identical
  (prove on the corpus, don't assert).
- TDD per pattern: minimal repro asserting pass1 == pass2 == pass3
  (period-2 cycles make pass3 mandatory).
- Preserve prior invariants: line-ending preservation
  (`SplitLinesPreservingEndings`), the depth guard, the continuation masks.

## Strategy: targeted fixes first (Plan A), fixed-point iteration as sanctioned fallback (Plan B)

**Plan A — targeted.** Order by expected tractability:

1. Singletons: each is likely an isolated predicate gap like the dozens
   already fixed (see `AliasPassCorruptionTests`, `CommentDriftTests`,
   `WidthWrapFixedPointTests` for the house style). APPROXIMATE is probably
   vector-syntax tokens confusing the alias splitter; Run_Methods and
   sp_IndexCleanup are comment × align interactions.
2. Multi-token RHS: generalize `WrapOverflowingAliasLiterals` — when the
   assembled `alias = <expr>` line overflows, reproduce the core's break for
   a multi-token expr (break before the token that overflows, continuation
   at the core's indent). Study how the core chooses that break so the pass
   reproduces it exactly; the lone-literal version is the template.
3. JOIN `as`-insertion: the align block must account for the width it itself
   adds — either wrap within the block (keeping the continuation attached to
   the block for the next pass) or decline to add `as` when doing so pushes
   the line past MaxLineWidth (dropping an optional nicety on an overlong
   line is additive-safe and inherently stable).
4. Period-2 oscillators: diagnose which two layouts alternate and remove the
   asymmetry so one of them is self-reproducing.

**Plan B — internal fixed-point iteration (use if A stalls on a family, or
finishes and stragglers remain).** Mathematical observation: if F(x) is the
formatter and F has a period-2 cycle {a, b}, then G(x) = F(F(x)) is
idempotent on that cycle. Implementing "format twice, emit the second result
when the post-passes were active and the results differ" guarantees
idempotency for every period-≤2 instability at the cost of a second format
pass. Conditions for taking this route:

- Only when text post-passes actually modified the output (default profile
  must remain literally the same code path — byte-identical, zero cost).
- Measure and report the runtime cost on the corpus (expect ≤2× on heavy;
  the 2 MB installers are the worst case — report their absolute times).
- Verify corpus-wide that pass3 == pass2 after the change (a period-3+ cycle
  would defeat it; none observed, but prove it).
- It is a guarantee mechanism, not a bug fix: keep the targeted fixes that
  already landed, and note in the discovery section which families B papered
  over, so future work can still find them.

If B is implemented, flag it clearly in the report — Jeremy may want it as
an internal always-on behavior or behind an option; stop and ask before
wiring it to a new user-visible option.

## Repro harness

Corpus via `tools/realworld-test.sh`; heavy profile:

```
--expand-between=false --expand-boolean=false --expand-case=false
--expand-in-lists=false --uppercase-keywords=false --standardize-keywords=false
--trailing-commas=true --align-table-joins=true --column-always-has-alias=true
--select-first-column-newline=true --align-columns=true --align-ddl-columns=true
--alias-style=equals --indent-where-and-or=true --max-line-width=200
--compact-raiserror=true --compact-single-statement-blocks=true
```

Two-pass (and three-pass) checks via temp files, never stdin. The three
giant installers need several GB RAM under heavy — skip and note if
memory-constrained.

## Verification (all required)

1. Per-pattern regression tests (pass1 == pass2 == pass3), failing before.
2. Heavy sweep: **0 FATAL / 2 known PARSEWARN / target 0 UNSTABLE** (report
   the final number; anything above 0 gets a one-line reason per file). No
   previously-stable file destabilized.
3. Full suite 0 failed / 26 skipped; CorpusOracle green; default-profile
   corpus output byte-identical to the pre-task commit.
4. If Plan B was used: the perf table and the papered-over-family notes.
5. Report: per-family root cause, fix, test names, sweep before/after, and
   anything discovered-not-fixed appended to
   `docs/idempotency-wrap-task.md`'s discovery section.

---

## RESULT — DONE (2026-07-21)

**Heavy-profile sweep UNSTABLE 18 → 0** (0 FATAL / 2 known PARSEWARN:
ChangeDbAndExecuteStatement, AnnotationParser). Full per-family root cause, fixes,
tests, before/after, perf, and the Plan-B flag-for-Jeremy note are in the
`docs/idempotency-wrap-task.md` discovery section ("Discovery: final idempotency task",
appended per Verification #5). Summary against each verification item:

1. **Per-pattern regression tests (fail-before).**
   - `DdlAlignScopeTests.ComputedColumnWrappedStringLiteralIsNeverPaddedInside` +
     `…WrappedStringExprIsIdempotent` — AlignDdlColumns string-literal corruption.
   - `AliasPassCorruptionTests.UnclosedBracketColumn_IsIdempotent_NoAliasStacking` —
     EnsureColumnAliases doubling on an unclosed `[`.
   - `WidthWrapFixedPointTests.JoinAsInsertion_ReachesFixedPoint` — Plan B fixed point
     (toggle-verified genuinely failing with the reformat disabled).
2. **Heavy sweep: 0 UNSTABLE / 2 known PARSEWARN / 0 FATAL.** No previously-stable file
   destabilized (strict subset check).
3. **Full suite 705 / 0 / 26**; CorpusOracle green; **DEFAULT-profile corpus output
   byte-identical** to the pre-task binary (70 files + all InputSql, proven).
4. **Plan B used** (internal fixed-point reformat, always-on). Perf: **1.68× worst case**
   (same-binary off vs on, Install-All-Scripts 2 MB heavy 1.67 s → 2.81 s), ≤2×.
   Papered-over families (not root-cause-fixed): JOIN-`as` insertion, multi-token alias
   RHS, and the residual wrap oscillators — all period-1/converge@p2/token-identical
   (documented in the discovery section).
5. **Report** appended to `docs/idempotency-wrap-task.md`.

### Root causes fixed at the source (not papered over)
- **AlignDdlColumns padded INSIDE string literals** (correctness bug, unbounded drift) —
  wrapped computed-column continuation lines starting with `N'…'` were mistaken for column
  defs and split at the space inside the literal. Fix: skip lines whose first token contains
  a quote. Also de-corrupted latent-but-idempotent bloat in
  sp_HumanEvents/sp_HealthParser/sp_WhoIsActive.
- **EnsureColumnAliases doubled on an unclosed bracket** (ParsingDisaster `[A = 1`, period≥3).
  Fix: `EnsureAlias` skips columns with an unclosed `[` outside strings.

⚠ **Jeremy:** Plan B changes heavy/AlignEquals output for previously-unstable files to their
pass-2 fixed point (default untouched, byte-identical). Internal, always-on, no new option.
Say if you want it behind a flag / off by default. Changes are uncommitted.
