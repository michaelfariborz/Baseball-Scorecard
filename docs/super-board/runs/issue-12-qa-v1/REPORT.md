# QA v1 · issue #12 · apply pending EF migrations on startup

Head under test: `30f00aa` (builder) plus one QA test.
Database: scratch Postgres databases named off `SCORECARD_TEST_DB`, dropped after the run.

| AC | Result | Proof |
|---|---|---|
| 1 · empty DB gets every migration, none pending | ✅ | `StartupMigrationTests.Empty_database_gets_every_migration_and_each_is_logged`, `Starting_the_app_host_migrates_an_empty_database` · `live-run1.log` |
| 2 · each applied migration logged at Information; current DB logs nothing to apply | ✅ | `Empty_database_…`, `Current_database_logs_that_there_is_nothing_to_apply` · `live-run1.log`, `live-run2.log` |
| 3 · `Database:MigrateOnStartup=false` applies nothing | ✅ | `MigrateOnStartup_false_applies_nothing` · `live-off.log` (0 public tables after start) |
| 4 · failing migration stops the app with an error naming it | ✅ | `A_failing_migration_stops_with_an_error_naming_it`, new `A_failing_later_migration_is_named_and_earlier_ones_stay_applied_and_logged` · `live-fail.log` (exit 134, never listened, history = AddRosters only) |
| 5 · PROJECT.md "Run locally" explains it and the off switch | ✅ | `docs/super-board/PROJECT.md` "Run locally" paragraph + variable table row |

Suite: `dotnet restore && dotnet build --no-restore && dotnet test --no-build` · 61/61 · `verify.log`.
Live `/rosters` after migrating an empty DB: HTTP 200 · `live-http.txt`.

## QA-written test (High gap)
AC4 was only tested with the first migration failing, where the fallback `pending[0]` gives the
same name. The new test fails `AddGames` after `AddRosters` applies. Red-checked by mutating
`ScorecardDbMigrator.cs:44` to `var failed = pending[0];` (fails: name not found), then restored.

## Test gaps (not written)
- Medium: no app-host test that a failing migration stops `WebApplicationFactory` start; covered live only (`live-fail.log`).
- Low: an unparseable `Database:MigrateOnStartup` value (`"nope"`) throws from `GetValue<bool>` at startup; no test, behaviour is a clear crash.
- Low: on an empty DB, EF logs two `fail:` lines probing the missing `__EFMigrationsHistory` table before migrating (`live-run1.log`); EF's own noise, not this change.
