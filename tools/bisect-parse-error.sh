#!/bin/bash
# Isolates parse errors in a big SQL file by splitting it on GO batch
# separators and formatting each batch independently.
#
# Usage: tools/bisect-parse-error.sh <file.sql>
#
# Output: per-batch verdicts with line ranges; failing batches print their
# diagnostics. A batch that fails on its own contains a real parser gap;
# a batch that only failed as part of the whole file was collateral damage.

set -u
[ $# -eq 1 ] || { echo "usage: $0 <file.sql>"; exit 2; }
SRC="$1"
[ -f "$SRC" ] || { echo "no such file: $SRC"; exit 2; }

cd "$(dirname "$0")/.."
FMT="PoorMansTSqlFormatterCmdLine/bin/Release/net10.0/SqlFormatter"
[ -x "$FMT" ] || { echo "Build first: dotnet build -c Release PoorMansTSqlFormatterCmdLine/PoorMansTSqlFormatterCmdLine.csproj"; exit 1; }

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

# Split on lines that are exactly GO (optionally with trailing whitespace/;)
awk -v dir="$TMP" '
    BEGIN { n = 1; start = 1 }
    /^[Gg][Oo][ \t;]*$/ {
        print start ":" NR-1 > (dir "/" sprintf("%04d", n) ".range")
        close(dir "/" sprintf("%04d", n) ".range")
        n++; start = NR + 1; next
    }
    { print > (dir "/" sprintf("%04d", n) ".sql") }
    END {
        print start ":" NR > (dir "/" sprintf("%04d", n) ".range")
    }
' "$SRC"

fails=0
for b in "$TMP"/*.sql; do
    base="${b%.sql}"
    range="$(cat "$base.range" 2>/dev/null || echo '?')"
    num="$(basename "$base")"
    # skip whitespace-only batches
    if ! grep -q '[^[:space:]]' "$b"; then continue; fi
    out="$("$FMT" "$b" 2>/dev/null)"; code=$?
    if [ $code -eq 5 ]; then
        fails=$((fails+1))
        echo "BATCH $num (lines $range): PARSE ERROR"
        printf '%s\n' "$out" | grep '^--   ' | sed 's/^--   /    /'
    elif [ $code -ne 0 ]; then
        echo "BATCH $num (lines $range): FATAL exit $code"
    fi
done

if [ $fails -eq 0 ]; then
    echo "No batch fails on its own — the error only appears with batches combined"
    echo "(suggests a GO-handling issue itself, or an unterminated construct spanning GO)."
fi
