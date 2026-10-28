# QA v2 · issue 27 · head 07618e2

Rerun after staging was merged into the branch (round 2).

- `dotnet restore && dotnet build --no-restore && dotnet test --no-build`: 140 passed, 0 failed
- `HomePageTests` and `GameServiceTests` cover AC1-AC5 and all pass
- `Home.razor` and the list queries in `GameService.cs` are unchanged since v1 (`b8bd46c`); the only src diff is staging's own changes
- `MainLayoutTests.Routed_page_is_wrapped_in_the_layout` now passes (the stale failure from #25)
- Live screenshots: see `../issue-27-qa-v1/`
