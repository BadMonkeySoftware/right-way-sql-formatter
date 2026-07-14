# Upstream issue triage — TaoK/PoorMansTSqlFormatter

All 149 open issues from [TaoK/PoorMansTSqlFormatter](https://github.com/TaoK/PoorMansTSqlFormatter/issues)
(excluding PRs), triaged against RightWaySqlFormatter on 2026-07-14. Every issue
with a reproducible SQL snippet was run through the current `SqlFormatter` CLI
(default options, exit code + idempotency + output inspection).

Statuses: **FIXED** (verified working), **FIXED-OPT** (solved by an option we added),
**STILL** (bug reproduces today), **DIALECT** (non-T-SQL syntax; policy is
"support where it doesn't interfere with T-SQL"), **FEATURE** (enhancement
request, backlog candidate), **N/A** (upstream product/infrastructure that
isn't part of this fork: SSMS installers, notepad++, poorsql.com, packaging),
**CHECK** (needs manual investigation).

## Verified fixed in RightWaySqlFormatter

| # | Issue | Notes |
|---|---|---|
| [#4](https://github.com/TaoK/PoorMansTSqlFormatter/issues/4) | DDL triggers (FOR LOGON) | Formats sensibly now |
| [#45](https://github.com/TaoK/PoorMansTSqlFormatter/issues/45) | Better CLI error reporting | Line-numbered diagnostics, exit codes 0/1/5 |
| [#112](https://github.com/TaoK/PoorMansTSqlFormatter/issues/112) | WITH XMLNAMESPACES | Test 40 |
| [#179](https://github.com/TaoK/PoorMansTSqlFormatter/issues/179) | Service Broker + WAITFOR | Test 42 |
| [#181](https://github.com/TaoK/PoorMansTSqlFormatter/issues/181) | In-repo changelog | vscode-extension/CHANGELOG.md |
| [#192](https://github.com/TaoK/PoorMansTSqlFormatter/issues/192) | Extra GO appended with output file | Verified: no GO added |
| [#199](https://github.com/TaoK/PoorMansTSqlFormatter/issues/199) / [#238](https://github.com/TaoK/PoorMansTSqlFormatter/issues/238) | .NET 2.0 / netstandard2.0 | Core is .NET 10 |
| [#203](https://github.com/TaoK/PoorMansTSqlFormatter/issues/203) / [#268](https://github.com/TaoK/PoorMansTSqlFormatter/issues/268) | DROP VIEW/PROCEDURE IF EXISTS misparse | Test 41 |
| [#237](https://github.com/TaoK/PoorMansTSqlFormatter/issues/237) | BEGIN DIALOG formatting | Service Broker support |
| [#242](https://github.com/TaoK/PoorMansTSqlFormatter/issues/242) | Uppercase .SQL extension rejected | Any-case filenames read fine |
| [#257](https://github.com/TaoK/PoorMansTSqlFormatter/issues/257) | Max line width splits binary literals | Verified at width 12 |
| [#270](https://github.com/TaoK/PoorMansTSqlFormatter/issues/270) | Repo clone problems (long paths) | README documents core.longpaths |
| [#293](https://github.com/TaoK/PoorMansTSqlFormatter/issues/293)-part1 | `DROP TABLE IF EXISTS t1, t2, t3` seen as 2 statements | Parses clean now (part 2 still open, below) |
| [#298](https://github.com/TaoK/PoorMansTSqlFormatter/issues/298) | Culture-sensitive resource lookup | Localized resources removed in modernization |

## Fixed via options we added

| # | Issue | Option |
|---|---|---|
| [#59](https://github.com/TaoK/PoorMansTSqlFormatter/issues/59) / [#201](https://github.com/TaoK/PoorMansTSqlFormatter/issues/201) | Indent JOIN/ON | `indentJoinOnClause` (ON vs JOIN; JOIN-vs-FROM variant of #59 not offered) |
| [#63](https://github.com/TaoK/PoorMansTSqlFormatter/issues/63) | Auto column aliases | `columnAlwaysHasAlias` |
| [#64](https://github.com/TaoK/PoorMansTSqlFormatter/issues/64) | Align aliases/operators | `alignColumnDefinitions`, `alignTableJoins` (operator alignment not offered) |
| [#84](https://github.com/TaoK/PoorMansTSqlFormatter/issues/84) | Don't expand RAISERROR args | `compactRaiserror` |
| [#111](https://github.com/TaoK/PoorMansTSqlFormatter/issues/111) | Don't touch keyword case | `uppercaseKeywords=false` + `standardizeKeywords=false` |
| [#290](https://github.com/TaoK/PoorMansTSqlFormatter/issues/290) | WHERE conditions aligned under first | `indentWhereAndOrConditions` |
| [#299](https://github.com/TaoK/PoorMansTSqlFormatter/issues/299) | Leading commas in VS Code | Leading is the default; `trailingCommas` toggles |

## Still present — T-SQL bugs (fix candidates, priority order)

| # | Issue | Repro result today |
|---|---|---|
| [#288](https://github.com/TaoK/PoorMansTSqlFormatter/issues/288) / [#241](https://github.com/TaoK/PoorMansTSqlFormatter/issues/241) / [#30](https://github.com/TaoK/PoorMansTSqlFormatter/issues/30) | Nested-join double-ON (`JOIN a JOIN b ON … ON …`) | Exit 5 "Incomplete or invalid structure", both ONs glued to one line, non-idempotent. Valid T-SQL; the biggest remaining parser gap. |
| [#266](https://github.com/TaoK/PoorMansTSqlFormatter/issues/266) | `IF … THROW n, 'msg', 1;` without BEGIN/END | Exit 5; THROW args treated as comma list; each subsequent IF indents deeper (cascade) |
| [#215](https://github.com/TaoK/PoorMansTSqlFormatter/issues/215) / [#292](https://github.com/TaoK/PoorMansTSqlFormatter/issues/292)-partial | `--[noformat]` inside a statement | Block hoisted out of INSERT column list; blank line accumulates inside block (non-idempotent). Top-level noformat blocks are stable. |
| [#240](https://github.com/TaoK/PoorMansTSqlFormatter/issues/240) | Space added before brackets: `table_[x]` → `table_ [x]` | Reproduces |
| [#200](https://github.com/TaoK/PoorMansTSqlFormatter/issues/200) | `''.txt''` → `''.txt ''` (space inside doubled-quote string) | Reproduces; corrupts dynamic SQL text |
| [#151](https://github.com/TaoK/PoorMansTSqlFormatter/issues/151) | `ALTER TABLE x ALTER COLUMN …` | Blank line inserted between the two clauses (parsed as 2 statements) |
| [#128](https://github.com/TaoK/PoorMansTSqlFormatter/issues/128) | `INSERT /*+hint*/ INTO` | Hint comment moved after INTO (breaks Oracle/Vertica hints) |
| [#272](https://github.com/TaoK/PoorMansTSqlFormatter/issues/272) | `definition` uppercased to DEFINITION | Reproduces (keyword list too aggressive; not a reserved word) |
| [#293](https://github.com/TaoK/PoorMansTSqlFormatter/issues/293)-part2 | `a IS DISTINCT FROM b` (SQL 2022) | Exit 0 but FROM treated as clause start — `IS DISTINCT\nFROM b1` |
| [#99](https://github.com/TaoK/PoorMansTSqlFormatter/issues/99) | First line of block comment gets indented | Reproduces (indent added; rest of comment untouched — stable, cosmetic) |
| [#13](https://github.com/TaoK/PoorMansTSqlFormatter/issues/13) | Unary minus: `SELECT -1` → `SELECT - 1` | Reproduces (upstream: cosmetic only) |
| [#17](https://github.com/TaoK/PoorMansTSqlFormatter/issues/17) | ODBC escapes: `{d'…'}` → `{d '…' }` | Reproduces (upstream marked very low priority) |
| [#5](https://github.com/TaoK/PoorMansTSqlFormatter/issues/5)-partial | OVER() clauses | Now expands (better than upstream), but window-frame keywords not case-standardized: `ROWS BETWEEN unbounded preceding AND CURRENT row` |
| [#195](https://github.com/TaoK/PoorMansTSqlFormatter/issues/195) | Deeply nested CASE readability | Same shape as upstream |

## Still present — non-T-SQL dialect (policy call: T-SQL formatter)

[#249](https://github.com/TaoK/PoorMansTSqlFormatter/issues/249) postgres `<@` split into `< @` ·
[#116](https://github.com/TaoK/PoorMansTSqlFormatter/issues/116) colon params `:1` ·
[#152](https://github.com/TaoK/PoorMansTSqlFormatter/issues/152) / [#204](https://github.com/TaoK/PoorMansTSqlFormatter/issues/204) `END IF` (PL/SQL) ·
[#164](https://github.com/TaoK/PoorMansTSqlFormatter/issues/164) JOIN `USING(…)` ·
[#138](https://github.com/TaoK/PoorMansTSqlFormatter/issues/138) MySQL `CONVERT(x USING utf8)` ·
[#230](https://github.com/TaoK/PoorMansTSqlFormatter/issues/230) MySQL `CREATE OR REPLACE VIEW` breaks after CREATE ·
[#159](https://github.com/TaoK/PoorMansTSqlFormatter/issues/159) postgres general ·
[#80](https://github.com/TaoK/PoorMansTSqlFormatter/issues/80) MDX/DAX ·
[#92](https://github.com/TaoK/PoorMansTSqlFormatter/issues/92) SQLCMD `:r`/`:setvar` (known; "detect SQLCMD mode and warn distinctly" is on our backlog)

## Feature requests (backlog candidates)

Formatting options: [#302](https://github.com/TaoK/PoorMansTSqlFormatter/issues/302) columns separate from expressions ·
[#291](https://github.com/TaoK/PoorMansTSqlFormatter/issues/291) keep DECLARE lists on one line ·
[#282](https://github.com/TaoK/PoorMansTSqlFormatter/issues/282) align data types in DECLARE (we align CREATE TABLE only) ·
[#263](https://github.com/TaoK/PoorMansTSqlFormatter/issues/263) ALTER VIEW ·
[#262](https://github.com/TaoK/PoorMansTSqlFormatter/issues/262) params/declarations ·
[#256](https://github.com/TaoK/PoorMansTSqlFormatter/issues/256) Holywell style preset ·
[#254](https://github.com/TaoK/PoorMansTSqlFormatter/issues/254) don't indent FROM ·
[#252](https://github.com/TaoK/PoorMansTSqlFormatter/issues/252) expand CHECKSUM() args ·
[#250](https://github.com/TaoK/PoorMansTSqlFormatter/issues/250) / [#58](https://github.com/TaoK/PoorMansTSqlFormatter/issues/58) trailing/auto semicolons ·
[#235](https://github.com/TaoK/PoorMansTSqlFormatter/issues/235) strip comments ·
[#222](https://github.com/TaoK/PoorMansTSqlFormatter/issues/222) indent subsequent clauses ·
[#210](https://github.com/TaoK/PoorMansTSqlFormatter/issues/210) / [#82](https://github.com/TaoK/PoorMansTSqlFormatter/issues/82) / [#62](https://github.com/TaoK/PoorMansTSqlFormatter/issues/62) paren placement styles ·
[#209](https://github.com/TaoK/PoorMansTSqlFormatter/issues/209) don't expand math operators ·
[#196](https://github.com/TaoK/PoorMansTSqlFormatter/issues/196) expand functions ·
[#172](https://github.com/TaoK/PoorMansTSqlFormatter/issues/172) "remove clutter" profile ·
[#169](https://github.com/TaoK/PoorMansTSqlFormatter/issues/169) variable case standardization ·
[#163](https://github.com/TaoK/PoorMansTSqlFormatter/issues/163) column-name comments in long INSERTs ·
[#133](https://github.com/TaoK/PoorMansTSqlFormatter/issues/133) remove harmless `[]` ·
[#122](https://github.com/TaoK/PoorMansTSqlFormatter/issues/122) / [#57](https://github.com/TaoK/PoorMansTSqlFormatter/issues/57) uppercase built-in functions ·
[#117](https://github.com/TaoK/PoorMansTSqlFormatter/issues/117) line-width break preferences ·
[#114](https://github.com/TaoK/PoorMansTSqlFormatter/issues/114) / [#123](https://github.com/TaoK/PoorMansTSqlFormatter/issues/123) keep-on-one-line marker / auto-ignore ·
[#106](https://github.com/TaoK/PoorMansTSqlFormatter/issues/106) comment wrapping ·
[#103](https://github.com/TaoK/PoorMansTSqlFormatter/issues/103) / [#44](https://github.com/TaoK/PoorMansTSqlFormatter/issues/44) trailing booleans/ON ·
[#102](https://github.com/TaoK/PoorMansTSqlFormatter/issues/102) / [#83](https://github.com/TaoK/PoorMansTSqlFormatter/issues/83) / [#68](https://github.com/TaoK/PoorMansTSqlFormatter/issues/68) / [#65](https://github.com/TaoK/PoorMansTSqlFormatter/issues/65) casing/quoting of identifiers ·
[#77](https://github.com/TaoK/PoorMansTSqlFormatter/issues/77) / [#61](https://github.com/TaoK/PoorMansTSqlFormatter/issues/61) extra break placement ·
[#50](https://github.com/TaoK/PoorMansTSqlFormatter/issues/50) semi-compact CASE ·
[#27](https://github.com/TaoK/PoorMansTSqlFormatter/issues/27) add/remove AS (we add via option and preserve style; removal not offered) ·
[#135](https://github.com/TaoK/PoorMansTSqlFormatter/issues/135) settings presets ·
[#211](https://github.com/TaoK/PoorMansTSqlFormatter/issues/211) editorconfig support (VS Code extension) ·
[#287](https://github.com/TaoK/PoorMansTSqlFormatter/issues/287) suppress trailing GO (SSMS plugin behavior; CLI verified clean) ·
[#218](https://github.com/TaoK/PoorMansTSqlFormatter/issues/218) better keyword-standardization docs ·
[#269](https://github.com/TaoK/PoorMansTSqlFormatter/issues/269) / [#9](https://github.com/TaoK/PoorMansTSqlFormatter/issues/9) parse-tree access/docs (library exposes it; ParsedSql XML shows the shape) ·
[#43](https://github.com/TaoK/PoorMansTSqlFormatter/issues/43) 2012-17 T-SQL enhancements sweep

## N/A — upstream products/infrastructure this fork doesn't ship

- **SSMS plugin installers/versions** (we ship the SSMS plugin as source, Windows build; installer issues don't apply): #301, #300, #297, #286, #283, #281, #265, #255, #251, #234, #202, #197, #194, #193, #191, #274, #113, #253
- **Notepad++ plugin** (dropped): #279, #271, #258, #224, #223, #217, #160, #150
- **poorsql.com website** (not ours): #273, #221, #219, #198, #166, #158, #124, #76
- **Minifier/obfuscator** (not shipped in CLI/extension): #277, #278, #121
- **Packaging/distribution** (superseded by our Marketplace/GitHub setup): #294, #243, #233, #232, #199-dist, #155, #101, #93
- **Other editors/products**: #280 (Sublime), #171, #170, #208 (Azure Data Studio), #80 (MDX), #7, #8 (Mono), #259, #267, #10, #14, #42

## Notes found during triage

- **README bug (ours, fixed with this commit):** the root README claimed
  `SqlFormatter myquery.sql` formats in-place. The CLI never had in-place mode —
  a file argument is input only; output goes to stdout unless `--output` is set.
- The nested-join family (#288/#241/#30) is the highest-value fix: valid,
  reasonably common T-SQL that still exits 5 and formats non-idempotently.
- #200 and #128 are the two "silently corrupts semantics" bugs (dynamic SQL
  string content, Oracle-style hints) — small blast radius but nasty.
