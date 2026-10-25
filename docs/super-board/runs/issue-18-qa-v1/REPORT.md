# QA v1 · issue 18 · head 68521f6

Command: dotnet restore && dotnet build --no-restore && dotnet test --no-build
Result: 96 passed, 0 failed, 0 warnings

| AC | Test | Result |
|---|---|---|
| Clearing Year empties Away/Home and clears picked teams | NewGamePageTests.Clearing_the_year_empties_both_team_lists_and_clears_picked_teams_then_a_new_year_lists_rosters_again | pass |
| Blank Year save shows message, saves nothing | NewGamePageTests.Saving_with_a_blank_year_shows_a_message_and_saves_nothing | pass |
| Re-entering a valid Year lists rosters | same clear-then-re-enter test as AC1 | pass |

Mutation check (not committed): reverting the blank-year list to keep old rosters and removing the YearMessage made both tests fail (2 failed, 13 passed); restored, 96 pass.

Test gaps (not written): Year typed as non-numeric text or out of range (e.g. 0, 99999) is not pinned; it lists no rosters, so no team can be saved. Low.
UI screenshots: omitted on purpose; the ACs are covered by bUnit component tests against Postgres.
