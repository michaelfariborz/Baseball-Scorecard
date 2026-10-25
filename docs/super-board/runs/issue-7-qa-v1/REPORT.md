# QA v1 — issue #7, head `27d96fd`

## AC ledger
| AC | Service integration (Postgres) | Component (bUnit + Postgres) | Browser (live app) |
|---|---|---|---|
| 1 list "#N First Last", sorted | `PlayerServiceTests.List_sorts_by_number_value_then_by_text` | `RosterPlayersFormDbTests.Roster_page_lists_its_players_as_number_first_last_sorted_by_number` | PASS, cold reload |
| 2 add saves to that roster | `Add_saves_the_player_to_that_roster`, `Add_trims_the_fields` | `Adding_a_player_through_the_form_saves_it_and_lists_it` | PASS |
| 3 edit saves, remove deletes | `Edit_saves_the_changed_fields`, `Edit_keeping_the_same_number_is_allowed`, `Remove_deletes_the_player` | `Editing_a_player_through_the_form_saves_the_change`, `Removing_a_player_deletes_it` | PASS, cold reload |
| 4 validation, uniqueness | `Blank_field_*`, `Number_that_is_not_one_or_two_digits_*`, `Number_already_used_*_on_add/_on_edit`, `Same_number_on_a_different_roster_is_allowed`, `Database_unique_index_rejects_*` | `Invalid_player_shows_a_validation_message_and_saves_nothing`, `Number_used_on_the_same_roster_shows_the_duplicate_message_and_saves_nothing` | PASS (3+ digits stopped by `maxlength=2`; service rejects "123") |
| 5 tests against SCORECARD_TEST_DB + bUnit | yes | yes | n/a |

## Test gaps (not written)
- Medium: the unique-violation `catch` in `PlayerService.SaveAsync` (race path) has no test through the service; the index itself is tested directly.
- Medium: editing a player that another tab already removed throws `InvalidOperationException` in `SaveAsync`, which ends the Blazor circuit instead of showing a message.
- Low: removing the player currently loaded in the edit form resets the form; no test.

## Not this ticket
- `/favicon.ico` returns 404 (console error); it did before this PR.
