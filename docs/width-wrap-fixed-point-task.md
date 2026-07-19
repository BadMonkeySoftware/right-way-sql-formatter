# Task: core width-wrap must reach a fixed point (heavy-profile idempotency endgame)

## Goal

`format(format(x))` must equal `format(x)` byte-for-byte. 25 corpus files remain
non-idempotent under the heavy profile; ~16 of them are ONE mechanism in the
core formatter (not the text post-passes): **the max-line-width wrap decision
does not reach a fixed point when a wrapped element sits at the width
boundary** — an element flips between end-of-line and start-of-next-line on
every pass, forever. Fix the mechanism, drive heavy-profile UNSTABLE from
**25 → single digits** (ideally 0 with the secondary targets), and classify
whatever remains.

Read CLAUDE.md first. Non-negotiables:

- Expected files under `RightWaySqlFormatter.Tests/Data/` are untouchable
  without Jeremy's explicit sign-off. If any `StandardFormatExpectedOutput`
  test fails, STOP and show the diff. (This matters here: some expected files
  encode current wrap behavior — a wrap-logic change that alters them needs
  a decision, not a silent regen.)
- Full suite `dotnet test RightWaySqlFormatter.NoSSMS.slnx` → 0 failed.
  Baseline: **722 total / 0 failed / 26 skipped** (sandbox counter; Mac ~724).
- `ScriptDomValidationTests` (always-on) and the `[Explicit]` `CorpusOracle`
  stay green — validity is a solved invariant.
- Default-profile output must not change (default = max-line-width 999, so
  wrapping is rare but NOT impossible — prove with the suite + a default-mode
  corpus byte-identity spot check, as previous rounds did).
- Do NOT regress the line-ending preservation work
  (`SplitLinesPreservingEndings` — CRLF inside multi-line literals is data).
- TDD: minimal repro test per pattern, asserting pass1 == pass2 == pass3.

## The mechanism (classified in docs/idempotency-wrap-task.md, round 4/6 work)

Observed signatures (heavy profile, `--max-line-width=200`):

- A function argument flips sides of the break:
  `replace(…), '_',` at end-of-line  ⇄  next pass `'_', '[_]')` moved down.
- `Details = '…'` keeps/loses its trailing `;` across the break.
- Affected files: sp_Blitz, sp_BlitzCache, sp_BlitzFirst, sp_HumanEvents,
  ProtectSession, DatabaseBackup, MaintenanceSolution, IndexOptimize,
  DatabaseIntegrityCheck, the Deprecated Blitz procs, and friends.

This is NOT the CRLF-inside-literals driver (fixed) and NOT the post-pass
continuation bugs (fixed). It is the core tree-walk width accounting.

The invariant to establish: **the wrap decision must be a function of the
logical token stream + options only — never of the physical line layout of
the input.** Pass 1 and pass 2 tokenize to the same stream, so if output
differs, something layout-derived is leaking into the decision. Prime
suspects to verify (grep `MaxLineWidth` and read the formatting state):

1. `SourceBreakPending` / preserved source line breaks: the wrap break that
   pass 1 INSERTS becomes a "source break" in pass 2's input. If the width
   logic treats an already-broken line differently from one it would break
   itself (e.g. resets the column counter, then re-joins because the
   fragment now fits), you get exactly this flip-flop.
2. Boundary arithmetic: the decision "does the next token fit?" measured
   BEFORE emitting separators/commas (TrailingCommas=true adds a char after
   the decision), so a line can be emitted at exactly the limit on pass 1
   but measure over the limit on pass 2 (or vice versa).
3. Continuation-line indentation feeding back into the width check
   differently than the original line's indentation did.

Diagnosis approach that has worked in prior rounds: take the SMALLEST
affected file (DatabaseIntegrityCheck.sql or ProtectSession.sql), locate the
first pass1-vs-pass2 diff, extract that one statement into a minimal SQL
snippet, and shrink until the flip reproduces in a unit test. Only then read
the wrap code with the concrete case in hand. Fix direction is free — break
earlier, break later, or honor an existing break — as long as pass 2
re-derives pass 1's layout exactly and the suite stays green.

## Repro harness

Corpus: `tools/realworld-test.sh` clones into `realworld-results/corpus/`.
Heavy profile (Jeremy's real profile):

```
--expand-between=false --expand-boolean=false --expand-case=false
--expand-in-lists=false --uppercase-keywords=false --standardize-keywords=false
--trailing-commas=true --align-table-joins=true --column-always-has-alias=true
--select-first-column-newline=true --align-columns=true --align-ddl-columns=true
--alias-style=equals --indent-where-and-or=true --max-line-width=200
--compact-raiserror=true --compact-single-statement-blocks=true
```

Two-pass check via temp FILES (never stdin). Also run a pass 3 when
investigating — some cycles have period 2, and a fix that makes pass2==pass1
but pass3 differ is not a fix.

## Secondary targets (same session if time allows)

1. **Trailing whitespace at wrap points (~5 files).** The core leaves a
   trailing space when wrapping after a separator; the next pass strips it.
   Fix at the SOURCE — don't emit the trailing space when inserting a wrap
   break. Do NOT add a global trailing-whitespace strip: 30 expected files
   contain intentional trailing whitespace, and a global strip is a
   Jeremy-sign-off decision. If even the targeted fix trips an expected
   file, stop and show the diff.
2. **Singletons:** `WITH … APPROXIMATE ve.Id` alias mis-split (DarlingData
   Vector Defense ×3), `[A = 1` (tsqlt ParsingDisaster.sql — input is
   deliberately malformed; stability matters, validity doesn't),
   sp_IndexCleanup (JOIN-align × multi-line comment on the ON line).

## Verification (all required)

1. Per-pattern regression tests (pass1 == pass2 == pass3), failing before.
2. Heavy sweep: baseline **0 FATAL / 2 known PARSEWARN / 25 UNSTABLE** →
   report new count; remaining files must be a strict subset (nothing
   previously stable destabilizes). Giant installers (darling-data
   Install-All, FRK Install-Azure/Install-All-Scripts) need several GB RAM —
   skip and note if constrained.
3. Full suite 0 failed; CorpusOracle green; default-profile corpus output
   byte-identical to the pre-task commit.
4. Report: mechanism found (with the leaking layout-dependence named
   precisely), fix, test names, before/after UNSTABLE, and a one-line
   classification for every file still unstable. Discoveries out of scope:
   append to docs/idempotency-wrap-task.md, don't fix silently.
