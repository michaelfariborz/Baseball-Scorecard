# QA v2 · #42 · branch `issue-42-hide-saved-lineup-picker` (staging merged in at `f87f0e6`)

Fixes reviewer findings R1 (no browser evidence) and R2 (first-save guard test lost).

| AC | Result | Browser proof (Chromium, app on a throwaway Postgres DB) | Test |
|---|---|---|---|
| AC1 no saved lineups: both pickers, no Edit lineup | pass | `ac1-no-lineups-*.png` | `GameLineupFormDbTests.Game_with_no_saved_lineups_shows_both_pickers_and_no_edit_button` |
| AC2 save away: away picker gone, Edit lineup for away, home picker stays | pass | `ac2-away-saved-*.png` | `Saving_a_lineup_hides_that_teams_picker_and_shows_Edit_lineup_while_the_other_stays` |
| AC3 both saved: no pickers, two Edit lineup buttons | pass | `ac3-both-saved-*.png` | `Game_with_both_lineups_saved_opens_with_only_the_two_Edit_lineup_buttons` |
| AC4 Edit lineup shows saved players + positions; saving hides it | pass | `ac4-edit-open-*.png`, `ac4-after-resave-desktop.png` | `Edit_lineup_brings_back_the_picker_with_saved_values_and_saving_hides_it_again` |
| AC5 batting grid keeps 9 saved rows while picker hidden | pass | `ac5-away-grid-desktop.png` | save-hides-picker test |

- Browser run: `browser-run.js` → `browser-run.log` / `browser-results.json`: 13/13 checks pass, 0 console or page errors.
- R2: added `GameBattingGridFormDbTests.First_save_of_one_lineup_keeps_the_other_teams_unsaved_picks` (no lineup saved; pick home slot 1, first-save away, home pick survives). Mutation: forcing both editors to re-create on save (`@key` bumped in `OnSaved`) fails it and the re-save variant; restored.
- Full suite: 274/274 (`dotnet-verify.log`).

Test gaps (not written): Medium · a saved slot whose player left the roster is hidden behind Edit lineup (reviewer R3, builder-owned) · Low · no cancel in edit mode (R4, ACs don't ask).

Local tests: dotnet restore && dotnet build --no-restore && dotnet test --no-build
