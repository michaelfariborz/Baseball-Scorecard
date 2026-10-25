#!/usr/bin/env bash
# Heavier AC2 check: BATCHES (default 10) rounds of K (default 4) `dotnet test` runs at once.
# Before the R3 fix this reproduced `Removing_a_player_deletes_it` failing within 5 rounds.
# Run from the repo root after `dotnet build`. Needs SCORECARD_TEST_DB.
set -u
BATCHES=${1:-10}; K=${2:-4}; tmp=$(mktemp -d); fails=0
for b in $(seq 1 "$BATCHES"); do
  for k in $(seq 1 "$K"); do dotnet test --no-build > "$tmp/$b-$k.log" 2>&1 & done; wait
  n=$(grep -l 'Failed!' "$tmp"/$b-*.log 2>/dev/null | wc -l); ok=$(grep -l 'Passed!' "$tmp"/$b-*.log | wc -l)
  echo "round $b/$BATCHES: $ok/$K runs passed"
  if (( n > 0 || ok != K )); then fails=$((fails+1)); grep -h '\[FAIL\]' "$tmp"/$b-*.log; fi
done
rm -rf "$tmp"
echo "TOTAL: $((BATCHES-fails))/$BATCHES rounds clean"; [[ $fails -eq 0 ]]
