# QA #29 · v2 · head `bee359d`

Verdict: ✅ pass, 4/4 AC. Reviewer round 1 findings: R1 still flaky at `bee359d` and fixed here (tests only), R2 fixed by the builder and confirmed, R3 written.

## How it ran
- Suite: `dotnet restore && dotnet build --no-restore && dotnet test --no-build`. 188/188 after this pass's test changes.
- Live app: `dotnet run` against a throwaway Postgres DB (`baseball_scorecard_qa29v2`, dropped after the run). Startup migrations applied `AddBattingGrid` cleanly. Seed: `seed.sql` (Cubs @ Sox, 9 innings, only the Cubs lineup saved). The seed first had a DH with no starting pitcher, so the on-page lineup save was refused. That was a seed mistake, not an app bug: slot 4 was changed to Pitcher and the run repeated.
- Browser: headless Chrome via playwright-core, script `qa29v2.browser.js`, output `browser-run.log`. 18/18 checks PASS. No console errors.

## AC results
| AC | Result | Proof |
|---|---|---|
| 1 · 9 rows in batting order, then columns 1..Innings | ✅ | headers `Number,Name,Position,1..9`; rows in lineup order · `GameBattingGridFormDbTests.cs:106` |
| 2 · note ≤ 20 chars saves, survives reload, clear removes it | ✅ | `6-3`, `K`, a 20-character note reloaded; cleared cell blank after reload; `maxlength=20` · `GameBattingGridFormDbTests.cs:169`, `BattingGridServiceTests.cs:73` |
| 3 · HITS + LOB per inning, whole numbers ≥ 0, bad entry refused with message | ✅ | 9 + 9 boxes; `1/2` and `0/0` reloaded; `-1`, `two`, `1.5` showed the message, HITS still `1` after reload · `GameBattingGridFormDbTests.cs:204`, `:228`, `BattingGridServiceTests.cs:126` |
| 4 · no saved lineup shows "Save a lineup to start scoring." | ✅ | Sox section shows the message and no table · `GameBattingGridFormDbTests.cs:127` |

## Reviewer findings
- R1 (flaky page tests): at `bee359d` the full suite failed 4 of 35 runs, every time in `GameLineupFormDbTests` (`Dh_disables_pitcher…`: stale `onchange` handler, and `Same_player_or_position_twice…`: 10 s wait timeout). `origin/staging` failed 0 of 15. Removing `OnSaved` did not stop it. Cause: `GameLineupFormDbTests.RenderGame` started picking before the two batting grids finished loading. Fix (tests only): it now waits for both grids, as the grid tests do. After: 0 of 25 runs failed, and 0 of 8 under 4 busy CPU loops.
- R2 (refused text left looking saved): fixed. Live: LOB `x` refused with the message, then a good note save put the box back to blank and hid the message (`r2-after-good-save.png`). Test `GameBattingGridFormDbTests.cs:249`.
- R3 (note stays with its slot): written as `A_note_stays_with_its_batting_slot_when_the_lineup_changes`, `GameBattingGridFormDbTests.cs:183`. Seen red by temporarily having a lineup save drop slot 1's notes, then green once that change was undone. Live: slots 1 and 2 swapped on the page, and `6-3` stayed on slot 1 under the new batter.

## Test gaps (not written)
- Low · at 375 px the page is 747 px wide and scrolls sideways (the line score and grid are both wide).
