# QA v1 · issue #40 · head 4604249

Command per run: `dotnet test --no-build` (after `dotnet restore && dotnet build --no-restore`), 20 runs in a row, full suite (163 tests).
Load: one busy-loop per core on all 4 cores, plus other board workers; 1-min load average 10.4–17.2 during runs (see `stress-runs.log`). `stress` is not installed here.

| AC | Result | Proof |
|---|---|---|
| NewGamePageTests 20/20 under load, alongside the full suite | ✅ pass | 0 NewGame failures in 20 runs · `stress-runs.log` |
| HomePageTests 20/20 under load | ❌ fail · 18/20 | runs 14 and 20: `Year_filter_shows_only_that_year_and_all_shows_every_year` at `HomePageTests.cs:127` · `run-14-failure.txt`, `run-20-failure.txt` |
| No assert deleted, skipped or weakened | ✅ pass | `git diff origin/staging...4604249 -- tests/` adds 24 lines, removes 0; no `Skip` added |

Mutation check: with `if (load != loadNumber) return;` removed from `NewGame.razor`, the new test `A_slow_first_load_...` fails at `NewGamePageTests.cs:165` (only `Cubs` listed); restored, it passes.

## Why Home still fails

Same race as New Game, in `src/BaseballScorecard/Components/Pages/Home.razor`.
- `OnInitializedAsync` loads years, then teams, then `LoadGamesAsync()` with the "All" filter
- The test waits for the year options only, then picks 2025, which starts a second `LoadGamesAsync()`
- If the first (All years) load finishes last, it overwrites `games` with every year's games
- bUnit shows "Check count: 1 · render count 4": no render came after the stale list, so a longer wait cannot help
- The 10 s `DefaultWaitTimeout` did not stop it: both failures waited the full 10 s

What fixed looks like: `Home.razor` drops a games load that a newer load overtook (same guard as `NewGame.razor`), with a gated-load test like `A_slow_first_load_...` that fails without the guard.
