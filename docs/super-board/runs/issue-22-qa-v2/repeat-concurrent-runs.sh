#!/usr/bin/env bash
# AC2 repeat check: runs concurrent-runs.sh N times (default 8) and passes only if every run passes.
# Run from the repo root after `dotnet build`. Needs psql and SCORECARD_TEST_DB.
set -u
N=${1:-8}; dir=$(dirname "$0"); fails=0
for i in $(seq 1 "$N"); do
  out=$(bash "$dir/concurrent-runs.sh" 2>&1); r=$(tail -1 <<<"$out")
  sum=$(grep -E '^run [AB]|^peak|^new run' <<<"$out" | sed -E 's/exit=[0-9]+: .*Failed: +([0-9]+), Passed: +([0-9]+).*Total: +([0-9]+).*/failed \1, passed \2\/\3/' | paste -sd';')
  echo "rerun $i/$N: $r · $sum"
  [[ $r == "RESULT: PASS" ]] || { fails=$((fails+1)); grep -h '\[FAIL\]' run-a.log run-b.log; }
done
rm -f run-a.log run-b.log
echo "TOTAL: $((N-fails))/$N passed"; [[ $fails -eq 0 ]]
