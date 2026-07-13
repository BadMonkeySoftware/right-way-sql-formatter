#!/bin/bash
# Real-world corpus test for RightWaySqlFormatter.
#
# Downloads well-known public T-SQL codebases (the kind of SQL that
# PoorMansTSqlFormatter users format daily), runs every .sql file through the
# CLI, and reports:
#   - FATAL:      formatter crashed (exit 1) — always a bug
#   - PARSE-WARN: parse errors reported (exit 5) — inspect: real invalid SQL
#                 or a parser gap?
#   - UNSTABLE:   format(format(x)) != format(x) — formatter not idempotent
#   - OK:         clean and stable
#
# Usage:
#   tools/realworld-test.sh                 # default options profile
#   PROFILE="--indent-string=\t" tools/realworld-test.sh   # custom flags
#
# Results land in realworld-results/ (gitignored): summary.txt plus per-file
# outputs for everything flagged, so failures can be eyeballed and turned
# into InputSql test cases.

set -u
cd "$(dirname "$0")/.."
ROOT="$(pwd)"
WORK="$ROOT/realworld-results"
CORPUS="$WORK/corpus"
OUT="$WORK/flagged"
PROFILE="${PROFILE:-}"

echo "== Building CLI (Release) =="
dotnet build -c Release RightWaySqlFormatter.CmdLine/RightWaySqlFormatter.CmdLine.csproj || exit 1
FMT="$ROOT/RightWaySqlFormatter.CmdLine/bin/Release/net10.0/SqlFormatter"
[ -x "$FMT" ] || { echo "CLI binary not found at $FMT"; exit 1; }

mkdir -p "$CORPUS" "$OUT"

clone() { # name url
    if [ ! -d "$CORPUS/$1" ]; then
        echo "== Fetching $1 =="
        git clone --depth 1 --quiet "$2" "$CORPUS/$1" || echo "WARN: could not clone $1 (offline?)"
    fi
}

clone first-responder-kit  https://github.com/BrentOzarULTD/SQL-Server-First-Responder-Kit.git
clone maintenance-solution https://github.com/olahallengren/sql-server-maintenance-solution.git
clone sp_whoisactive       https://github.com/amachanic/sp_whoisactive.git
clone darling-data         https://github.com/erikdarlingdata/DarlingData.git
clone tsqlt                https://github.com/tSQLt-org/tSQLt.git

SUMMARY="$WORK/summary.txt"
: > "$SUMMARY"
total=0; ok=0; warn=0; fatal=0; unstable=0

echo "== Formatting corpus (profile: '${PROFILE:-default}') =="
find "$CORPUS" -name '*.sql' -type f | sort | while read -r f; do
    rel="${f#"$CORPUS"/}"
    total=$((total+1))
    out1="$("$FMT" $PROFILE "$f" 2>/dev/null)"; code=$?
    case $code in
        0)
            # idempotency: format the formatted output again
            out2="$(printf '%s' "$out1" | "$FMT" $PROFILE 2>/dev/null)"
            if [ "$out1" = "$out2" ]; then
                ok=$((ok+1))
            else
                unstable=$((unstable+1))
                echo "UNSTABLE   $rel" >> "$SUMMARY"
                safe="$(echo "$rel" | tr '/' '_')"
                printf '%s' "$out1" > "$OUT/$safe.pass1"
                printf '%s' "$out2" > "$OUT/$safe.pass2"
            fi
            ;;
        5)
            warn=$((warn+1))
            echo "PARSE-WARN $rel" >> "$SUMMARY"
            safe="$(echo "$rel" | tr '/' '_')"
            printf '%s' "$out1" > "$OUT/$safe.parsewarn"
            ;;
        *)
            fatal=$((fatal+1))
            echo "FATAL($code)  $rel" >> "$SUMMARY"
            ;;
    esac
    # progress dot per 25 files
    [ $((total % 25)) -eq 0 ] && printf '.' >&2
    # write running totals (subshell-safe)
    printf '%s %s %s %s %s' "$total" "$ok" "$warn" "$fatal" "$unstable" > "$WORK/.counts"
done
echo >&2

read -r total ok warn fatal unstable < "$WORK/.counts"
{
    echo ""
    echo "===================================================="
    echo "Profile:     ${PROFILE:-default}"
    echo "Total files: $total"
    echo "OK:          $ok"
    echo "PARSE-WARN:  $warn   (exit 5 — inspect $OUT/*.parsewarn)"
    echo "FATAL:       $fatal  (crashes — always bugs)"
    echo "UNSTABLE:    $unstable (non-idempotent — diff pass1 vs pass2 in $OUT)"
    echo "===================================================="
} | tee -a "$SUMMARY"

echo "Full details: $SUMMARY"
