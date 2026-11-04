# QA v1 issue 42

Command: dotnet restore && dotnet build --no-restore && dotnet test --no-build
Result: 0 errors, Passed 260, Failed 0.

AC map (GameLineupFormDbTests, bUnit):
- no saved lineups, both pickers, no Edit: Game_with_no_saved_lineups_shows_both_pickers_and_no_edit_button
- save away hides away picker, home stays: Saving_a_lineup_hides_that_teams_picker_and_shows_Edit_lineup_while_the_other_stays
- both saved, only Edit buttons: Game_with_both_lineups_saved_opens_with_only_the_two_Edit_lineup_buttons
- Edit shows saved values, save hides: Edit_lineup_brings_back_the_picker_with_saved_values_and_saving_hides_it_again
- batting grid rows while hidden: asserted in the save-hides test (#away-batting-grid table tbody tr)
Browser screenshots: not captured (bUnit component tests only).
