# QA v1 - issue 16 (double-click Save)

Local tests: `dotnet restore && dotnet build --no-restore && dotnet test --no-build` -> 94 passed, 0 failed.

Mutation checks on `NewGame.razor` (restored after each, not committed):

| Mutation | Result |
|---|---|
| remove `disabled="@saving"` and `if (saving) return;` | `Save_is_disabled_while_a_save_is_running` and `Submitting_twice_..._creates_exactly_one_game` fail |
| comment out `saving = false;` in the failure branch | `Save_is_enabled_again_after_a_save_fails_validation` fails |

No screenshots: the ACs are asserted by bUnit tests against a real Postgres fixture; no browser run.
