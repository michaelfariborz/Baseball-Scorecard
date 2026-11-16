# QA #85 v1 — extra inning column

Head tested: `c3f9d1c` · verdict: ❌ fail (AC4)

## Setup
- DB `baseball_scorecard_qa85` migrated to `AddStolenBases`, seeded with `seed.sql` (game 1: 5 old at bats; game 2: away slot 1 inning 3 = 2B).
- Branch app started on it (`dotnet run`, port 5185); it applied `AddInningColumns` on startup. Old rows got Column 1 (`db-before.txt`, `db-after.txt`).
- `node qa85.js` (Playwright, Chrome) · `browser.log`, `browser-results.json`.
- Verify command: `dotnet restore && dotnet build --no-restore && dotnet test --no-build` · 522/522 · `dotnet-test.log`.

## Per AC
| AC | Result | Proof |
|---|---|---|
| AC1 add a column after the inning, header repeats the inning | ✅ | headers `1,2,3,3,4…`; inning 7 too · `ac1-extra-column-added-desktop.png` |
| AC2 extra-column at bat saved apart, both show after reload | ✅ | 2B col 1 + GO 6-3 col 2; 1B reached 1st col 2; DB has separate rows · `ac2-after-reload-desktop.png` |
| AC3 one team's grid in one game | ✅ | home grid and game 1 grids keep 9 inning headers |
| AC4 empty extra column removable / gone after reload; one with at bats stays | ❌ | empty last column removed ✅; refused for a column with at bats ✅; **empty column 2 before a saved column 3 is kept after a reload and cannot be removed** · `ac4-fail-empty-column-2-kept-desktop.png`, `empty-middle.log` |
| AC5 HITS/LOB one per inning spanning columns; old games unchanged | ✅ | colspan 2 on inning 3, 9 HITS boxes, 4/2 kept after reload; old cells `K/GO 4-6-3 DP/1B SB/BB/HR` unchanged and editable |

## AC4 repro
1. Open `/games/2`, away grid.
2. Batted around: inning 6, Add column twice (headers `6,6,6`).
3. Score slot 4, inning 6, column 3 as K. Leave column 2 empty.
4. Reload. Headers still `6,6,6`; column 2 is empty.
5. Inning 6, Remove empty column: "That column holds at-bats. Clear them before removing it." Column 2 stays.

Cause: `Columns()` (`BattingGrid.razor:388`) shows up to the highest saved column, and `RemoveColumn()` (`BattingGrid.razor:427-433`) only looks at the last column.

## Test gaps (not written)
- Medium: AC3 "any other game" has no automated test (browser only). Witness: add a column in game A, render game B, assert 9 inning headers.
- Low: `SaveNoteAsync` has no upper bound on `column` (service accepts column 50; UI cannot send it).
