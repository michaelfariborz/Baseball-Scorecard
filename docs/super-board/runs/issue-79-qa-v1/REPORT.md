# QA v1 · issue #79 · head e3b915f

| AC | Result | Proof |
|---|---|---|
| Year select labelled "Year", All first, years newest first, empty years absent | pass | options All, 2026, 2025 in a browser run; `RostersPageTests.Year_select_is_labelled_starts_on_all_and_lists_roster_years_newest_first` |
| Picking a year filters, sorted by team name; All restores newest first | pass | 2026 shows Aardvarks, Cubs, Mets only; All shows 4 rosters; `Picking_a_year_shows_only_that_years_rosters_and_all_shows_every_roster` |
| Opens on All; "No rosters yet." when none | pass | selected option All on load; `With_no_rosters_at_all_the_page_says_so_and_the_select_has_only_all` |
| Shared styles, no horizontal scroll at 375px | pass | computed select style matches Home's year filter; scrollWidth 375 = viewport 375 |

Local tests: dotnet restore && dotnet build --no-restore && dotnet test --no-build (475 passed, 0 failed)
