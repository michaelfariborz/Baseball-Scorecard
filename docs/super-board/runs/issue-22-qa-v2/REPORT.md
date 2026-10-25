# QA v2 · issue #22 · rebuild after review round 1

Base head `22fbcdc`. Reviewer findings R1 and R2 are both `[qa]`.

| AC | Result | Proof |
|---|---|---|
| 1 Own database per run, used by every fixture test, dropped at the end | ✅ | full suite 125/125 (`full-suite.log`) · 0 run databases left after every rerun (`concurrent-runs.log`) |
| 2 Two `dotnet test` runs at once both pass | ✅ | `repeat-concurrent-runs.sh 8`: 8/8 PASS, A and B 125/125 each time, 6 run databases at peak, 0 left (`concurrent-runs.log`); a second batch of 8 before the log format fix also went 8/8 |
| 3 Unset variable fails naming it | ✅ | unchanged since v1 (`../issue-22-qa-v1/ac3-unset-var.log`); `PostgresFixture.cs` not touched in this round |
| 4 PROJECT.md names CREATEDB | ✅ | `docs/super-board/PROJECT.md:46`, unchanged |

R1 fix: `RosterPlayersFormDbTests.cs:126-131` now reads `#player-number` and `#player-first` inside `cut.WaitForAssertion` after the `.edit-player` click.
Before the fix, at `22fbcdc`: `concurrent-runs.sh` went red 1 of 3 times on this machine, the same test and the same `Expected "14" Actual ""` (reviewer saw 3 of 4).
After: 16 of 16 concurrent reruns green.

R2 fix: AC2 proof is now 8 consecutive reruns in one script (`repeat-concurrent-runs.sh`), which fails if any one rerun fails.

Other post-`Click()` reads checked: every database-backed one is already inside `WaitForAssertion`. `RosterEditPageTests.cs:33` reads right after `Submit()` but renders without the database, so it is not part of this race.

Test gaps (not written), carried from v1:
- Low: no test for the `Database` missing fallback in `RunConnectionString`.
- Low: a base name over 50 chars, which Postgres truncates at 63.
