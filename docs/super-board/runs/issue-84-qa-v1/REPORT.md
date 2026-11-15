# QA #84 · v1 · head `f380e88`

Non-visual change (board tooling script). No screenshots: there is no UI.

| AC | Result | Proof |
|---|---|---|
| 1. Ready card, newest comment `blocked-by: -`, no open numbered blockers, is in `cards` with its lane | ✅ | `wave-plan-human-gate.test.mjs:63` (#80 in `cards`, lane `build`); mutant A (Ready filter back to `free()`) turns it red |
| 2. Blocked card with `blocked-by: -` stays out of `sweep` and `cards` | ✅ | `wave-plan-human-gate.test.mjs:69`; mutant B (sweep uses `ready_free()`) turns it red |
| 3. Ready card with `blocked-by: 12`, #12 open, stays out of `cards` | ✅ | `wave-plan-human-gate.test.mjs:74`; mutant C (`ready_free` drops the open-blocker check) turns it red |
| 4. Tests use saved `--from` / `--items` payloads; existing deps and planner tests pass | ✅ | the test is offline (`--from`, `--items`, `--deps`); no deps/planner tests existed on `staging` before (`git ls-files` shows none); workflow tests 18/18; dotnet 475/475 |

## Commands
- `node --test .agents/bin/tests/*.test.mjs` 5/5 · `node-tests.log`
- `node --test .agents/workflows/tests/*.test.mjs` 18/18 · `node-tests.log`
- three mutants, each caught by exactly one test, file restored · `mutation-checks.log`
- `dotnet restore && dotnet build --no-restore && dotnet test --no-build` 475/475 · `dotnet-verify.log`

## Live board (read-only)
The live planner on `staging` and on this branch both pick #80 now. #80's newest
`blocked-by:` line has since changed to `75` (closed), so it is runnable without the
fix. The live board no longer reproduces the bug; the saved payloads are the proof.

## Test gaps (not written)
- Medium · a 🙋 (needs-you) card a human drags to Ready is now released too. deps says
  "a 🙋 card stays humanGated — only the resume path moves it". Witness: Ready card with
  `Reason tag: 🙋` and `blocked-by: -`. Planned as `build`, not `resume`. Ticket's Fix covers
  any `blocked-by: -` card, so not a fail; a reviewer should confirm the intent.
- Low · a Ready card with an unparseable line (`blocked-by: none`) stays out of `cards`
  and lands in `flag`. Behaviour unchanged, not pinned by a test.
- Low · a Ready `qa`-labelled human-gated card gets lane `qa`. `lane_of` unchanged, not pinned.
