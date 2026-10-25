# QA v1 · issue #8 · starting lineups with positions

Head tested: `90d1171` · verdict ✅ pass

## Verify command
`dotnet restore && dotnet build --no-restore && dotnet test --no-build` · 113/113 passed (`verify.log`).

## AC ledger
| AC | Unit / service | Component (bUnit, real DB) | Browser (Chrome, real app) |
|---|---|---|---|
| 1 nine slots, roster players, "1 · Pitcher" … "10 · Designated Hitter" | `PositionTests.cs:9` | `GameLineupFormDbTests.cs:82` | `browser-run.log` AC1 x3 |
| 2 empty / same player / same position named, nothing saved | `LineupServiceTests.cs:102,120,131,142` | `GameLineupFormDbTests.cs:125,139` | AC2 x4 (reload proves nothing stored) |
| 3 DH: Pitcher disabled, Starting pitcher required, non-batting roster only, saved | `LineupServiceTests.cs:167,179,190,199,210,221` | `GameLineupFormDbTests.cs:160` | AC3 x4 |
| 4 reload in batting order | `LineupServiceTests.cs:59` | `GameLineupFormDbTests.cs:102` | AC4 x2 (away DH + pitcher, home custom order) |
| 5 integration tests on SCORECARD_TEST_DB + unit test pins 1–10 | all of the above run against SCORECARD_TEST_DB | | |

## Test the tests (mutants, never committed)
All six killed: drop Pitcher-with-DH check · picker lists every player · drop pitcher-in-order check · keep pitcher without DH · drop `disabled` on Pitcher · drop off-roster check.

## Test gaps (not written)
- Medium: two first saves of the same lineup at once hit the unique (GameId, RosterId) index; the second throws instead of showing a message. Witness: double-click Save on a fresh lineup over a slow link.
- Medium: `TeamNotInGameMessage` path (roster not in the game) has no test.
- Low: a slot already set to Pitcher keeps showing "1 · Pitcher" after DH is picked elsewhere; save rejects it with the slot named, so no bad data.

## Notes
- The only browser console error is the existing `/favicon.ico` 404 (also in issue-6 QA).
- Browser run used the throwaway test DB (`SCORECARD_TEST_DB`) seeded with game 491; no app or live DB touched.
