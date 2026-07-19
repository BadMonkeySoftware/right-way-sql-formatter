# Task: heavy-profile idempotency endgame — comment-position drift

## Goal

`format(format(x))` must equal `format(x)` byte-for-byte. After the validity
work (CorpusOracle green: every corpus file formats AND re-formats to valid
T-SQL under all three profiles), 35 of ~390 corpus files remain non-idempotent
under the heavy profile — all believed to be one family: **the align passes
re-position comments a little more on every pass** (indentation/padding of
comment-only lines and inline comments drifts, typically rightward). Drive the
heavy-profile UNSTABLE count from **35 → 0**, or as close as possible, and
classify anything that turns out not to be comment drift.

Read CLAUDE.md first. Non-negotiables:

- Never modify expected files under `RightWaySqlFormatter.Tests/Data/` without
  Jeremy's explicit sign-off — if any `StandardFormatExpectedOutput` test
  fails, STOP and show the diff.
- Full suite `dotnet test RightWaySqlFormatter.NoSSMS.slnx` → 0 failed.
  Baseline: **718 total / 0 failed / 26 skipped** (sandbox counter; Mac ~720).
- `ScriptDomValidationTests` (always-on, 3 profiles) must stay green, and the
  `[Explicit]` `CorpusOracle` must stay green — validity is now a solved
  invariant; do not trade it for stability.
- Additive-only: default-profile output must not change (the align passes are
  off at default — prove it with the suite, and spot-check a corpus file
  byte-identical pre/post under default).
- TDD: minimal NUnit repro per drift pattern before fixing.

## Known repro

Corpus via `tools/realworld-test.sh` (clones into `realworld-results/corpus/`).
Heavy profile flags (Jeremy's real profile):

```
--expand-between=false --expand-boolean=false --expand-case=false
--expand-in-lists=false --uppercase-keywords=false --standardize-keywords=false
--trailing-commas=true --align-table-joins=true --column-always-has-alias=true
--select-first-column-newline=true --align-columns=true --align-ddl-columns=true
--alias-style=equals --indent-where-and-or=true --max-line-width=200
--compact-raiserror=true --compact-single-statement-blocks=true
```

Two-pass check via files (never stdin). Example failure,
`darling-data/Presentations/Isolation Levels/Isolation Levels.sql` (~line 940):

```
pass1:    /*            ˅˅˅˅˅ Pay attention to this */
pass2:    /*                         ˅˅˅˅˅ Pay attention to this */
```

The comment gains padding on every pass — unbounded drift. Same family across
tsqlt files and several DarlingData presentations (~35 files total; enumerate
with a two-pass sweep and diff each flagged pair).

## Where to look

All in `RightWaySqlFormatter/Formatters/TSqlStandardFormatter.cs`. The align
passes (`AlignSelectColumns` / `AlignBlockAsKeywords` / `AlignBlockEqualSign`,
`AlignFromJoinClauses`, `AlignDdlColumns`) compute a target column from the
lines in a block, then pad lines to it. Two hypotheses to verify per pattern:

1. Comment lines (or lines with inline comments) are INCLUDED when measuring
   the block's max width, so each pass measures the previously-padded comment
   and pads further — a ratchet. Comments must be excluded from column-width
   math AND left un-repadded.
2. A comment between column lines splits or extends an alignment block
   differently on pass 2 (because pass 1 moved it), shifting the block's
   target column.

The invariant to enforce: a line that is comment-only, and the comment portion
of any line, must come out of an align pass byte-identical to how it went in —
alignment applies to code, never to comments. (Note `LineTouchesStringOrComment`
masks lines inside MULTI-line comments; single-line `/* ... */` and `--`
comment lines are the unprotected case.) Recent machinery you can build on:
`FindLineCommentStart`, `IsWholeLineComment`, the continuation mask, and the
line-ending preservation helpers (`SplitLinesPreservingEndings` — keep it).

## Verification (all required)

1. Regression tests per pattern (small snippets; comment before/between/after
   aligned columns; inline trailing comments in aligned blocks; the ratchet
   case asserting pass1 == pass2 == pass3).
2. Heavy-profile sweep: baseline **0 FATAL / 2 known PARSEWARN / 35 UNSTABLE**
   → report the new UNSTABLE count and confirm remaining files are a strict
   subset (no previously-stable file destabilized). The three giant installers
   (darling-data Install-All, FRK Install-Azure/Install-All-Scripts) need
   several GB RAM under this profile — skip and note if memory-constrained.
3. `CorpusOracle` still green; full suite 0 failed; no expected-file changes.
4. If any of the 35 turn out NOT to be comment drift, don't force-fit: fix if
   small, otherwise classify precisely and append to the discovery section of
   `docs/idempotency-wrap-task.md`.
5. Report: patterns found (with repro), fixes, test names, before/after
   UNSTABLE counts, and the final list of any still-unstable files with a
   one-line reason each.
