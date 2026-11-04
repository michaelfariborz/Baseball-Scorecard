# QA v2 · #40 · head 97b999e

| AC | Result | Proof |
|---|---|---|
| NewGamePageTests passes 20/20 loaded, with full suite | pass | stress-runs.log: 20 of 20 rc=0, 164/164 each, 4 burners on 4 cores, load 8.8-13.4 |
| HomePageTests passes the same 20 runs | pass | same 20 runs |
| No assert deleted, skipped, weakened | pass | tests diff vs staging: 1 line removed (a service registration moved into a helper), no Skip |

Mutation check: removing the `if (load != loadNumber) return;` guard in Home.razor and NewGame.razor fails exactly the two new held-load tests (162/164). Guard restored.
No screenshots: test-only ACs, no UI change.
Local tests: dotnet restore && dotnet build --no-restore && dotnet test --no-build
