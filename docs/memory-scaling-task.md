# Task: fix superlinear memory usage on large inputs

## Goal

Formatting memory must scale roughly LINEARLY with input size. Today it is
superlinear and large-but-realistic files are unusable on modest machines.
Known data points (measured 2026-07): a 6 MB SQL file peaks ~3.5 GB RSS under
the heavy profile (OOM on 4 GB machines) and ~1.7 GB under defaults;
`--max-line-width=200` roughly doubles peak vs width 999. The 2 MB single-file
installers users actually format (darling-data `Install-All/DarlingData.sql`,
first-responder-kit `Install-All-Scripts.sql`, `Install-Azure.sql`) already
OOM sandbox-class machines under the heavy profile.

Success criteria (all measured, not estimated):

1. Peak memory scales ~O(n): measure at 0.5 / 1 / 2 / 4 / 6 MB and show the
   curve is near-linear (fit or ratio table in the report).
2. The three corpus installers format under the heavy profile within ~500 MB
   peak managed heap each (stretch: 6 MB synthetic file too).
3. Wall-clock time does not regress more than ~20% on the normal-size corpus.
4. **Output is byte-identical everywhere** — this is pure performance work.

Read CLAUDE.md first. Non-negotiables, tightened for perf work:

- ZERO output changes. Not "additive-only" — literally byte-identical output
  for every input under every profile. There is no sign-off escape hatch in
  this task: if a refactor changes any output byte, the refactor is wrong.
  Prove it (see Verification), don't assert it.
- Full suite `dotnet test RightWaySqlFormatter.NoSSMS.slnx` → 0 failed
  (baseline **726 total / 0 failed / 26 skipped**, sandbox counter), no
  expected-file diffs, `CorpusOracle` green, heavy-sweep UNSTABLE stays
  exactly the same 18 files.
- NO new runtime dependencies (the shipping library/CLI have zero NuGet deps
  — a hard project policy), and the core library must keep compiling for BOTH
  net472 (SSMS plugins) and net10.0 — no net10-only APIs in shared code
  without a compatible fallback.

## Phase 1 — measure first, name the dominant cost (do NOT fix anything yet)

Build a small scratch harness (console app referencing the library, like the
existing treedump/sdcheck pattern; keep it out of the shipped projects or in
tools/) that formats a file and reports: `GC.GetTotalAllocatedBytes` (total
churn), peak `GC.GetTotalMemory` sampled around the phases, and per-phase
numbers — tokenize / parse / tree-format / each text post-pass — so the
superlinear phase is identified, not guessed. Inputs: the three corpus
installers plus synthetic files made by concatenating corpus SQL to 0.5/1/2/4/6
MB. Run defaults, heavy profile, and width-200-alone to reproduce the known
"width doubles it" effect. `dotnet-gcdump` / `dotnet-counters` are fine
locally if deeper attribution is needed.

Suspects to CONFIRM OR ELIMINATE with measurements (history says premises
shrink on contact — do not fix on hypothesis):

- Nested formatting states: the tree walk creates an inner
  `TSqlStandardFormattingState` per parens/subquery and merges child output
  into the parent (`Assimilate` / `DumpOutput` + `AddOutputContentRaw`).
  If content is copied once per nesting level, deeply nested scripts go
  O(n × depth) in allocations — a classic superlinear driver.
- Width accounting interaction (explains "width-200 doubles it"?): what does
  the wrap path allocate per line/token that the 999 path doesn't?
- Text post-passes: each does Split → List<string> → Join (a few full copies
  of the whole document — linear each, fine at ~10×, but verify none is
  quadratic). Known O(blocks × lines) re-computation: `AlignFromJoinClauses`
  recomputes `ComputeLinesInsideStringOrComment` + `ComputeStatementContexts`
  after every block whose line-count changed — quadratic-ish churn on
  JOIN-heavy files (though transient, not peak-RSS resident).
- Parse-tree size: Node-per-token with string names/attributes — expected
  linear but with a large constant; measure its share before blaming it.
- Also check GC mode: the CLI may simply never return freed memory to the OS
  (RSS ≠ live heap). If a big slice of the "3.5 GB" is dead-but-unreturned,
  say so — `GCSettings`/ServerGC vs WorkstationGC and
  `System.GC.RetainVM`-style config are cheap wins, but only AFTER the real
  allocation curve is known.

Deliverable of phase 1 (report before fixing): a table — phase × input-size ×
profile → allocations + peak — and one sentence naming the dominant
superlinear term.

## Phase 2 — fix only what the profile indicts

Likely shapes (choose based on evidence): stream child-state output into the
parent instead of copy-per-level; reuse/pool StringBuilders; make the
recompute-after-block-change incremental; avoid intermediate full-document
strings between post-passes where possible. Prefer several small, separately
verifiable changes over one big rewrite — after EACH change, re-run the
byte-identity check before proceeding.

## Verification (all required)

1. Byte-identity harness: format the ENTIRE corpus (or at minimum: all
   `RightWaySqlFormatter.Tests/Data/InputSql` files + 20 largest corpus files)
   under Default, AlignEquals, and HeavyEditor with the pre-task binary and
   the post-task binary; every output byte-identical. Report the file count.
2. Full suite 0 failed; CorpusOracle green; heavy sweep still exactly 18
   UNSTABLE (same files).
3. The measured before/after table for the success criteria above, same
   machine, same method.
4. Anything discovered but not fixed (e.g. "parse tree is 40% and linear —
   left alone"): record it in this doc's discovery section, added at the end.

---

## Phase 1 findings (2026-07-19) — measured, machine: macOS/Apple-Si, .NET 10, Workstation GC, 14 cores

**Dominant superlinear term: nesting DEPTH, via copy-per-level in `Assimilate`.**
Everything else measured LINEAR — the premise that shipped in the task header
(`--max-line-width=200` doubling, installers OOMing purely from size) does NOT
reproduce here; those are near-linear with a large constant. The real driver is
a dimension none of the "size" inputs varied: parenthesis/subquery nesting depth.

### What is linear (eliminated as the superlinear driver)
Repeat-proc synthetic (GO-separated batches; `GO` resets depth so this holds depth
constant while growing size), plus N-column SELECT and N-JOIN queries — all O(n):

| axis            | 0.5→6 MB (12×) alloc growth | verdict |
|-----------------|----------------------------|---------|
| size, default   | 35→519 MB (14.6×)          | ~linear |
| size, heavy     | 63→829 MB (13×)            | ~linear |
| SELECT columns  | fmt-MB doubles per 2× N    | linear  |
| JOIN count      | fmt-MB doubles per 2× N    | linear  |

Real installers (2 MB, shallow-per-statement) under heavy: ~380 MB churn / ~290 MB
peak-live / 259 MB RSS. Modest and linear. `width=200` vs `999`: within noise here
(not the "doubling" the header claims — that claim did not reproduce).

### The superlinear axis: nesting depth (nested derived tables, default profile)
| depth D | input B | fmt churn MB | out B     | fmt ms |
|--------:|--------:|-------------:|----------:|-------:|
| 50      | 1,155   | 3.9          | 16,355    | 41     |
| 100     | 2,305   | 25.5         | 62,705    | 205    |
| 200     | 4,705   | 190.2        | 245,505   | 1,394  |
| 400     | 9,505   | **2,987.4**  | 1,762,721 | 20,001 |
| 800     | 19,105  | — StackOverflow (~2018 recursion frames) — |

Input 2×, churn ~7–15×. A **9.5 KB** input allocates **~3 GB** of churn — this is
what reconciles the header's "3.5 GB": the file that hit it had real nesting depth,
not just size. Note the output itself is O(D²) (indentation width × line count both
grow with depth) — that part is inherent to correct output; the FIXABLE waste is the
extra O(depth) re-copying on top of it.

### Precise leaking mechanism (named)
For every `ENAME_EXPRESSION_PARENS`/`ENAME_IN_PARENS`/derived-table element the
tree walk builds an isolated `new TSqlStandardFormattingState(sourceState)`
(TSqlStandardFormatter.cs:2799) with its own `_outBuilder`, formats the children
into it, then folds it back with `state.Assimilate(inner)` →
`_outBuilder.Append(partialState.DumpOutput())` (3692). `DumpOutput()` is
`_outBuilder.ToString()` — a full copy of the child's entire (already-nested)
buffer, re-materialized and re-appended at EVERY ancestor level → innermost bytes
copied O(depth) times. Two more full-buffer `ToString()`s per level compound it:
`StartsWithBreak` (3580) and `OutputContainsLineBreak` (3684), each called per
parens in ProcessSqlNode (2802/2804). The isolated sub-state exists only for
line-width bookkeeping (the `CurrentLineLength` reset at 3551, the "cross-dependent
wrapping maze" TODO) — NOT because the output text needs to live in a separate
buffer. That is the seam the fix exploits.

### Also observed, not the memory driver (recorded per Verification #4)
- **Stack overflow at depth ≈800** — `ProcessSqlNode`/`ProcessSqlNodeList` recursion was
  unbounded; deep (pathological) nesting crashed the process (exit 134) before memory
  mattered. **FIXED (2026-07-20, follow-up ask):** `MAX_NESTING_DEPTH = 300`
  (SqlStructureConstants) — the parser flags any tree deeper than that as a parse error
  (iterative post-parse depth scan, `FindNodeBeyondDepth`) BEFORE formatting, and
  `ProcessSqlNode` stops descending past it. Pathological input now degrades to the normal
  best-effort parse-error result: warning-comment prefix + CLI exit 5, bounded RSS (~80 MB
  on the depth-1000 case) instead of a crash. Real T-SQL tops out at tree depth 89
  (measured across all corpus + test files), so the guard is provably inert on real input —
  output byte-identical (70 files × 3 profiles verified; suite 701/0/26; CorpusOracle green;
  parse time unchanged; net472 + net10 compile). Regression test
  `CmdLineTests.TestCmdLineDeeplyNestedInputExitsGracefullyNotStackOverflow` (depth 1000,
  asserts exit 5 in a child process). The free ToString micro-opt was NOT taken — it is not
  a clean one-liner (net472 `StringBuilder` is not `IEnumerable<char>`; needs a scan loop)
  and is worth only ~0.7 % churn on real files.
- **GC/RSS:** the shipped CLI uses default Workstation GC (no runtimeconfig GC keys).
  Forcing Server GC ~doubled RSS (259→455 MB, 2 MB installer); Workstation +
  `DOTNET_GCConserveMemory=9` dropped it to 220 MB. A Linux-sandbox host defaulting
  to Server GC + high churn + VM-retention is the most likely reason a 4 GB box OOMs
  where this Mac peaks < 500 MB. Cheap, output-safe lever if RSS (not churn) is the
  real production pain — but it treats the symptom; the depth copy-per-level is the
  disease.

### Arbiter measurement — is the copy-per-level fix worth it on REAL files?
Instrumented `Assimilate` (bytes folded per level) + the two per-level buffer
`ToString()`s, summed vs final output size, on the installers and the deepest-nesting
real files (Ola `REPLACE(REPLACE(…))`):

| file (heavy)          | out B     | (assim+tostr)/out |
|-----------------------|----------:|------------------:|
| MaintenanceSolution   | 697,099   | **1.0×**          |
| DatabaseBackup        | 317,187   | 1.1×              |
| Install-All-Scripts   | 2,700,778 | 1.0×              |
| DarlingData           | 2,198,708 | 0.7×              |
| sp_HumanEvents        | 333,406   | 0.5×              |

Copy-per-level touches ~**1× output** on real files. Total churn is ~**140× output**.
So the copy stack is **~0.7 % of real-file allocation** — the shared-buffer refactor
would remove 0.7 % of churn at high output-regression risk. **On real inputs it is
academic.** The 140× churn is dominated by the **parse phase** (Node-per-token +
string attributes, ~47× output, LINEAR) and the tokenizer — not the copy stack.

### Conclusion / fix plan (Phase 2)
The task's central premise — superlinear memory on the real installers — does NOT
reproduce on measurable hardware. Real-file memory is already O(n); the named
installers already peak < 300 MB managed / < 500 MB RSS here (success criteria 1 & 2
already met). The only superlinear term (depth copy-per-level) is unreachable by real
SQL and would net 0.7 %. Therefore the high-value, output-safe levers are NOT an
algorithmic rewrite:

1. **GC configuration** (the likely real production OOM cause): shipped CLI uses
   default Workstation GC. On a many-core Linux sandbox the runtime may default to
   Server GC, which + high linear churn + VM retention balloons RSS. Forcing
   Workstation + `GCConserveMemory` dropped RSS 259→220 MB here; a `GCHeapHardLimit`
   caps it hard. One `runtimeconfig`/csproj change, ZERO output bytes changed. This is
   the disease's real symptom-lever and needs Jeremy's sign-off (outward CLI behavior).
2. **(Optional, free) ToString removal** on the three per-level getters — helps only
   the pathological deep-nesting case; ~0.7 % on real files. Output-identical.
3. **Recursion guard** for the depth≈800 StackOverflow — real robustness bug, separate
   from memory; behavior-changing, flagged not fixed.

Full shared-buffer refactor: NOT recommended (0.7 % real benefit, high risk). Reducing
parse-phase allocation (the real 60 %) is a large parser change that only lowers an
already-linear constant. Both deferred pending an explicit decision.

### Decision (2026-07-19, Jeremy): STOP & DOCUMENT
Real-file memory is already O(n); the named installers already meet criteria 1 & 2
on measurable hardware; the only superlinear term is unreachable by real SQL and
nets ~0.7 %. No formatter change made — output remains byte-identical to the pre-task
binary (verified: 70 files × Default/AlignEquals/HeavyEditor, all identical). The three
deferred levers above (GC config, ToString micro-opt, recursion guard) are recorded here
for a future call. Measurement harness kept at `tools/memprofile/` (not in any solution;
does not affect builds/tests). If the production OOM recurs, lever #1 (GC config) is the
first thing to try and the one most likely to help on a many-core Server-GC host.
