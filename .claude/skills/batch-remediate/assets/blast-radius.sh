#!/bin/sh
# blast-radius.sh — consumer-impact check for a changed shared symbol.
#
# Answers: "I changed the timing/ordering/failure contract of SYMBOL.
#           Does any existing caller invoke it inside a loop?"
#
# A caller inside an iteration depends on per-call cost. Making such a call
# cheaper (sync -> async, blocking -> fire-and-forget) removes pacing the
# loop never declared, converting N sequential operations into N concurrent
# ones. Cause and effect land in different files, so diff review cannot see it.
#
# Usage:  sh blast-radius.sh <symbol> [search-root]
# Example: sh blast-radius.sh sendUILog symphonyModernization/SymphonyWebApplication/wwwroot
#
# Exit 1 if any call site is loop-nested (fail the gate), 0 otherwise.

SYM="$1"
ROOT="${2:-.}"

[ -z "$SYM" ] && { echo "usage: sh blast-radius.sh <symbol> [search-root]" >&2; exit 2; }

echo "Blast radius: $SYM"
echo "Root:         $ROOT"
echo

TOTAL=0
FILES=0
FLAGGED=0
TMP_FLAG=$(mktemp)

for f in $(grep -rl "$SYM" "$ROOT" --include=*.html --include=*.js --include=*.ts 2>/dev/null); do
  n=$(grep -c "$SYM" "$f" 2>/dev/null)
  [ "$n" = "0" ] && continue
  TOTAL=$((TOTAL + n))
  FILES=$((FILES + 1))

  # Report call sites whose enclosing block is a loop body.
  # Tracks brace depth from each loop header; a hit while depth > 0 is inside it.
  awk -v sym="$SYM" -v file="$f" '
    /for[ \t]*\(|while[ \t]*\(|\.forEach[ \t]*\(|\$\.each[ \t]*\(/ {
      if (!inloop) { inloop = 1; depth = 0; lstart = NR; kind = ($0 ~ /forEach|\$\.each/ ? "callback" : "loop") }
    }
    inloop {
      o = gsub(/{/, "{"); c = gsub(/}/, "}")
      depth += o - c
      if (depth <= 0 && NR > lstart) inloop = 0
    }
    index($0, sym) && inloop {
      line = $0
      sub(/^[ \t]+/, "", line)
      printf "  %s:%d  (inside %s opened at line %d)\n    %.90s\n", file, NR, kind, lstart, line
      hits++
    }
    END { if (hits) print "FLAGGED" > "/dev/stderr" }
  ' "$f" 2>>"$TMP_FLAG"
done

FLAGGED=$(grep -c "FLAGGED" "$TMP_FLAG" 2>/dev/null || echo 0)
rm -f "$TMP_FLAG"

echo
echo "call sites: $TOTAL across $FILES file(s)"

if [ "$FLAGGED" -gt 0 ] 2>/dev/null; then
  echo "loop-nested: $FLAGGED file(s)  -- REVIEW REQUIRED"
  echo
  echo "For each site above, confirm in the REVIEW file:"
  echo "  1. Does the loop have explicit pacing that does not depend on $SYM?"
  echo "  2. How many concurrent operations does the loop now issue at realistic input size?"
  echo "  3. Does anything else in the loop (progress UI, completion, unblock) assume per-iteration cost?"
  echo "Any site that cannot answer all three gets a test or a recorded risk."
  exit 1
fi

echo "loop-nested: none"
exit 0
