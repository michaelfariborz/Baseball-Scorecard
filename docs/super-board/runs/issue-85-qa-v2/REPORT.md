# QA #85 v2 — extra inning column

Head tested: `3b4e904` · verdict: ✅ pass

## Setup
- DB `baseball_scorecard_qa85v2` migrated to `AddStolenBases`, seeded with `seed.sql` (game 1: 5 old at bats; game 2: away slot 1 inning 3 = 2B).
- Branch app (`dotnet run --no-build`, port 5186) applied `AddInningColumns` on startup; old rows got Column 1 (`db-before.txt`, `db-after.txt`).
- `node qa85.js` (Playwright, Chrome) · `browser.log`, `browser-results.json`. 23/23 checks pass, no console or page errors, no 5xx.
- Verify command: `dotnet restore && dotnet build --no-restore && dotnet test --no-build` · 525/525 · `dotnet-test.log`.

## Per AC
| AC | Result | Proof |
|---|---|---|
| AC1 add a column after the inning, header repeats the inning | ✅ | headers `1,2,3,3,4…`; inning 7 too · `ac1-extra-column-added-desktop.png` · `ExtraColumnsFormDbTests.cs:86` |
| AC2 extra-column at bat saved apart, both show after reload | ✅ | 2B col 1 + GO 6-3 col 2, separate rows · `ac2-after-reload-desktop.png` · `ExtraColumnsFormDbTests.cs:107` |
| AC3 one team's grid in one game | ✅ | home grid and game 1 keep 9 inning headers · `ExtraColumnsFormDbTests.cs:86`, `:219` |
| AC4 empty extra column removable / gone after reload; one with at bats stays | ✅ | v1 repro now passes: empty column 2 before a scored column 3 is removed with no error (`ac4-empty-middle-removed-desktop.png`) and is gone after a reload (`ac4-empty-middle-gone-after-reload-desktop.png`); a column holding an at bat is refused · `ExtraColumnsFormDbTests.cs:135`, `:154`, `:172`, `:201` |
| AC5 HITS/LOB one per inning spanning columns; old games unchanged | ✅ | colspan 2, 9 HITS boxes, 4/2 kept; pre-migration at bats show and edit as before · `ac5-old-game-desktop.png` · `ExtraColumnsFormDbTests.cs:245`, `StartupMigrationTests.cs:168` |

## Test-gap check
- Every AC has a bUnit + Postgres test and a browser check. No High gap.
- A packed column edits its saved column (col 3 shown second): `ExtraColumnsFormDbTests.cs:201` and DB rows `db-after.txt`.

## Test gaps (not written)
- Low: `SaveNoteAsync` has no upper bound on `column` (service accepts column 50; the UI cannot send it).
