# QA v2 — issue #7, head `e1e74e8`

Re-verify after the rebase pass: `staging` `1a2be9f` merged in, R1 (`StartupMigrationTests` lists AddPlayers) and R2 (`PlayerService.SaveAsync` returns `PlayerRemovedMessage`).

## Results
- Verify command: 91/91 pass, 0 warnings (`verify.log`)
- Browser on `dotnet run` against `SCORECARD_TEST_DB` (app migrates on startup): 15/15 checks (`browser-run.log`)
- R2 regression test `Editing_a_player_that_was_removed_returns_a_message_and_saves_nothing`: mutation `return PlayerRemovedMessage` to `return null` turns it red; restored, green

## AC ledger
| AC | Service integration (Postgres) | Component (bUnit + Postgres) | Browser (live app) |
|---|---|---|---|
| 1 list "#N First Last", sorted | `PlayerServiceTests.List_sorts_by_number_value_then_by_text` | `RosterPlayersFormDbTests.Roster_page_lists_its_players_as_number_first_last_sorted_by_number` | PASS, cold reload |
| 2 add saves to that roster | `Add_saves_the_player_to_that_roster`, `Add_trims_the_fields` | `Adding_a_player_through_the_form_saves_it_and_lists_it` | PASS |
| 3 edit saves, remove deletes | edit, same-number edit, remove, removed-player tests | edit and remove form tests | PASS, cold reload |
| 4 validation, uniqueness | blank, format, duplicate add/edit, other roster, unique index tests | validation + duplicate message tests | PASS, nothing saved after 6 invalid tries |
| 5 tests against SCORECARD_TEST_DB + bUnit | yes | yes | n/a |

## Test gaps (not written)
- Medium: the unique-violation `catch` in `PlayerService.SaveAsync` (race path) still has no test through the service.
- Low: after the removed-player message, the stale list in that tab still shows the removed player until reload (`r2-removed-player-message-desktop.png`).
- Low: removing the player loaded in the edit form resets it; no test.

## Not this ticket
- `/favicon.ico` 404 (console error), pre-existing.
