# QA v1 · issue #22 · head f43eca7

| AC | Result | Proof |
|---|---|---|
| 1 Own database per run, used by every fixture test, dropped at the end | ✅ | `PostgresFixtureIsolationTests.cs:46` · full suite 125/125 (`full-suite.log`) · 0 run databases left |
| 2 Two `dotnet test` runs at once both pass | ✅ | `concurrent-runs.sh`: A 125/125, B 125/125, 6 run databases at peak, 0 left (`concurrent-runs.log`) |
| 3 Unset variable fails naming it | ✅ | `DatabaseConnectionTests.cs:24-26` · `ac3-unset-var.log`: 10/10 fixture tests fail with "SCORECARD_TEST_DB is not set" |
| 4 PROJECT.md names CREATEDB | ✅ | `docs/super-board/PROJECT.md:46` |

Mutations (`mutations.log`, never committed): the shared database in place of the run one turns the isolation test red. So does a no-op `DisposeAsync`. Restored, it is green.

Test gaps (not written):
- Low: no test for the `Database` missing fallback (`scorecard_test_<suffix>`) in `RunConnectionString`.
- Low: a base name over 50 chars, which Postgres truncates at 63. It is still unique, and the base name is fixed in the docs.
