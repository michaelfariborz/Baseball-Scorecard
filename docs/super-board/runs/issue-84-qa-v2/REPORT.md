# QA v2 · issue #84 · PR #86 @ `f26caa9`

Rebase pass re-verify. Planner logic is unchanged since v1 (`e98a6cf..f26caa9` touches only
header comments in the two scripts plus staging's roster test fix `7b8666d`).

| AC | Result | Proof |
|---|---|---|
| 1 Ready + `blocked-by: -` planned with lane | ✅ | `wave-plan-human-gate.test.mjs:63` · mutant A red |
| 2 Blocked + `blocked-by: -` out of sweep and cards | ✅ | `wave-plan-human-gate.test.mjs:69` · mutant B red |
| 3 Ready + `blocked-by: 12` (#12 open) out of cards | ✅ | `wave-plan-human-gate.test.mjs:74` · guarded by deps `runnable: false` |
| 4 offline payloads + existing tests green | ✅ | node 5/5 + 18/18 · `dotnet test` 475/475 (configured verify command) |

## Commands
- `node --test .agents/bin/tests/*.test.mjs && node --test .agents/workflows/tests/*.test.mjs` → `node-tests.log`
- `dotnet restore && dotnet build --no-restore && dotnet test --no-build` → `dotnet-verify.log`
- `bash -n` on both scripts: clean
- Mutants A–D → `mutation-checks.log` (C, D equivalent; see notes there)

## Test gaps (not written)
- Medium · a 🙋 (`needsYou`) card dragged to Ready is planned as `build`, not `resume` (review R2, product call for Eric)
- Low · unparseable `blocked-by:` line on a Ready card, and the `qa`-label lane, not pinned in this test file

Non-visual tooling change: no screenshots by design.
