# QA #29 · v1 · head `36940fe`

Verdict: ✅ pass, 4/4 AC.

## How it ran
- Suite: `dotnet restore && dotnet build --no-restore && dotnet test --no-build`. 186/186 passed, 0 failed.
- Live app: `dotnet run` against a throwaway Postgres DB (`baseball_scorecard_qa29`, dropped after the run). Startup migrations applied `AddBattingGrid` cleanly. Seed: Cubs @ Sox, 9 innings, only the Cubs lineup saved.
- Browser: headless Chrome via playwright-core, script `qa29.browser.js`, output `browser-run.log`.
- `browser-run.log` line "FAIL AC1 9 rows in batting order" is a script bug, not an app bug: the script expected `LF`, and the app labels positions `7 · Left Field`. The same line shows all 9 rows in the saved order.
- Before shots: `origin/staging` @ `33a06ba` on the same DB, with no batting grid.

## AC results
| AC | Result | Proof |
|---|---|---|
| 1 · 9 rows in batting order, then columns 1..Innings | ✅ | headers `Number,Name,Position,1..9`; rows in lineup order (8 Ian Happ … 34 Carson Kelly) · `GameBattingGridFormDbTests.cs:106` |
| 2 · note ≤ 20 chars saves, survives reload, clear removes it | ✅ | `6-3`, `K`, a 20-character note all reloaded; cleared cell blank after reload; `maxlength=20` · `GameBattingGridFormDbTests.cs:169`, `BattingGridServiceTests.cs:73` |
| 3 · HITS + LOB per inning, whole numbers ≥ 0, bad entry refused with message | ✅ | 9 HITS + 9 LOB boxes; `1/2` and `0/0` reloaded; `-1`, `two`, `1.5` showed the message and HITS stayed `1` after reload · `GameBattingGridFormDbTests.cs:215`, `BattingGridServiceTests.cs:126` |
| 4 · no saved lineup shows "Save a lineup to start scoring." | ✅ | Sox section shows the message and no table · `GameBattingGridFormDbTests.cs:127` |

## Test gaps (not written)
- Medium · a refused HITS/LOB entry keeps its text on screen, but the next good save in any other cell clears the message. Witness: LOB inning 3 = `x`, then note `F8` in slot 5 inning 3. The box still shows `x` and no message is shown (`BattingGrid.razor:144-150`, one shared `error`).
- Medium · no test that a note stays with its slot after the lineup is changed and saved again (a Fix line, not an AC). The schema has no FK from notes to the lineup, so there is no data-loss path today.
- Low · at 375 px the grid makes the page 758 px wide, so it scrolls sideways (the line score is wide too).

Console errors: none.
