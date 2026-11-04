# QA v1 · #48 · PR #52 · head 5ded3e1

Non-visual change (workflow script + skill docs): no screenshots.

## AC ledger (unit = node --test with stubbed agent/pipeline)
| AC | Unit | e2e (live wave) |
|---|---|---|
| AC1 two agents, own labels, verbatim brief | truth-check.test.mjs test 1 (brief pulled from super-review SKILL.md; mutant "reword brief line" goes red) | n/a: a lane agent has no Workflow tool |
| AC2 min score vs truth_threshold, agents named | tests 2, 8, 9 (boundary 69/70, config threshold 80, default 70; mutants >=→>, max-for-min, ignore-threshold go red) | n/a |
| AC3 no independent score → no merge | tests 3, 4, 5, 10, 11, 12 (null agent, insufficient_data, null gate, gate w/o PR, string confidence, halted gate) | n/a |
| AC4 skipped gate starts no truth agents | tests 6, 13 (gate rule text pinned; mutant ≥10→≥20 goes red) | n/a |

## Test gaps (not written)
- Medium: AC3 is enforced by the Reviewer prompt only; super-board-merge-gate.sh does not check for a truth score.
- Medium: the ≥10-line / label rule is applied by the gate agent (LLM), so only the prompt text is pinned.
- Low: no live wave run; labels are checked through the stubbed agent() opts.

## Commands
- node --test .agents/workflows/tests/*.mjs → 15/15 pass (node-tests.log)
- dotnet restore && dotnet build --no-restore && dotnet test --no-build → 256/256 pass (dotnet-verify.log)
- mutation.log: 8 mutants, each turned at least one test red, source restored
