# QA v1 · issue 27 · head d73e101

Local tests: dotnet restore && dotnet build --no-restore && dotnet test --no-build  (134 passed, 0 failed)

| AC | Proof |
|---|---|
| 1 empty and list, newest first, links | live: empty-desktop.png "No games yet."; list-desktop.png 3 rows newest first, hrefs games/3, games/2, games/1; bUnit + GameService tests |
| 2 Year filter | HomePageTests.Year_filter_shows_only_that_year_and_all_shows_every_year |
| 3 Team filter, home or away, any year, plus Year | HomePageTests.Team_filter_matches_home_or_away_in_any_year_and_combines_with_year |
| 4 dropdowns only list years/teams with games | live prerender: years 2026, 2025; teams Cardinals, Cubs, Mets; HomePageTests.Dropdowns_list_only_... |
| 5 Rosters and New game links | live screenshots; HomePageTests.Home_shows_the_app_heading_and_the_rosters_and_new_game_links |

Live run used a scratch database (since dropped), seeded with Cubs 2025/2026, Cardinals 2025, Mets 2026 and 3 games. Filter clicks were not driven in a browser; they are covered by the bUnit tests.
