# QA v1 · issue 38 · head fcee91f

Halt file redirected to a temp path; `gh` replaced by a stub that records any call.

| AC | Result | Proof |
|---|---|---|
| PR-files shape in READ_SHAPES, passes `json` check on a real PR | pass | `--kind json -- api 'repos/{owner}/{repo}/pulls/50/files?per_page=100' --jq '[...]'` exit 0, 2 files returned |
| `--slurp` + `--jq` exits 64, no `gh` call, no halt file | pass | stub not called, exit 64, no halt file (`--jq .`, `-q .`, `--jq=.`) |
| `--slurp` + `--template` refused | pass | exit 64, stub not called, no halt file |
| `--paginate --slurp` without `--jq` still works | pass | issue 38 comments read, exit 0 |
| `node --check` wrapper passes | pass | exit 0 |

Test gaps (not written): no automated test file for the helper (repo has no Python test harness); Low.
