#!/usr/bin/env bash
# AC2 check: two `dotnet test` runs started at the same time against the same
# SCORECARD_TEST_DB both pass, each in its own database, and both databases are dropped.
# Run from the repo root after `dotnet build`. Needs psql and SCORECARD_TEST_DB.
set -u
: "${SCORECARD_TEST_DB:?SCORECARD_TEST_DB is not set}"
field() { tr ';' '\n' <<<"$SCORECARD_TEST_DB" | awk -F= -v k="$1" 'tolower($1)==tolower(k){print $2}'; }
HOST=$(field Host); USER_=$(field Username); BASE=$(field Database)
list() { psql -h "$HOST" -U "$USER_" -d postgres -Atc "select datname from pg_database where datname like '${BASE}\_%' order by 1"; }

before=$(list)
echo "run databases before: $(wc -w <<<"$before")"

dotnet test --no-build > run-a.log 2>&1 & A=$!
dotnet test --no-build > run-b.log 2>&1 & B=$!

peak=0
while kill -0 $A 2>/dev/null || kill -0 $B 2>/dev/null; do
  n=$(comm -13 <(echo "$before") <(list) | grep -c . || true)
  (( n > peak )) && peak=$n
  sleep 0.2
done
wait $A; ea=$?; wait $B; eb=$?

after=$(list)
left=$(comm -13 <(echo "$before") <(echo "$after") | grep -c . || true)
echo "run A exit=$ea: $(grep -E 'Passed!|Failed!' run-a.log)"
echo "run B exit=$eb: $(grep -E 'Passed!|Failed!' run-b.log)"
echo "peak new run databases seen at once: $peak"
echo "new run databases left after both runs: $left"
[[ $ea -eq 0 && $eb -eq 0 && $peak -ge 2 && $left -eq 0 ]] && echo "RESULT: PASS" || { echo "RESULT: FAIL"; exit 1; }
