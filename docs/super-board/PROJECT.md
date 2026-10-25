# Baseball-Scorecard

Greenfield project: the repo holds only a README so far. Planned stack: .NET / C#.

| Item | Value |
|---|---|
| Repo | github.com/michaelfariborz/Baseball-Scorecard |
| Board | github.com/users/michaelfariborz/projects/1 |
| Base branch | `staging` (main is promoted by hand) |
| Merge check | `dotnet restore && dotnet build --no-restore && dotnet test --no-build` from repo root |

## Expectations for the first cards

- The scaffold card creates one solution at the repo root (`*.sln` or `*.slnx`) with an app project and a test project, so the merge check above passes.
- Every later card keeps `dotnet test` green.

## Data storage

Postgres through EF Core + Npgsql. `ScorecardDbContext` (`src/BaseballScorecard/Data`) reads the connection string `ConnectionStrings:Scorecard`. It is never kept in `appsettings*.json`; a test fails if one is added there.

## Run locally

Needs the .NET 10 SDK and a local Postgres.

1. Create the app and test databases:

   ```bash
   createdb baseball_scorecard
   createdb baseball_scorecard_test
   ```

2. Store the app's connection string in user-secrets (dev only, outside the repo):

   ```bash
   dotnet user-secrets set "ConnectionStrings:Scorecard" "Host=localhost;Database=baseball_scorecard;Username=<you>" --project src/BaseballScorecard
   ```

   Anywhere else, set the env var `ConnectionStrings__Scorecard` instead.

3. Point the tests at the throwaway test database. The test fixture applies migrations to it itself and fails with a message naming the variable when it is unset:

   ```bash
   export SCORECARD_TEST_DB="Host=localhost;Database=baseball_scorecard_test;Username=<you>"
   ```

4. Build, test and run from the repo root:

   ```bash
   dotnet restore && dotnet build --no-restore && dotnet test --no-build
   dotnet run --project src/BaseballScorecard
   ```

   On startup the app applies any pending EF migrations to its database before it takes requests, and logs each one it applies (or that there is nothing to apply). No more `dotnet ef database update` after a pull. If a migration fails, the app stops with an error naming it. To turn this off, set `Database:MigrateOnStartup` to `false` (user-secrets, or the env var `Database__MigrateOnStartup=false`).

| Variable | Used by | Value |
|---|---|---|
| `ConnectionStrings:Scorecard` (user-secrets) | app, dev | app database connection string |
| `ConnectionStrings__Scorecard` (env var) | app, everywhere else | same |
| `SCORECARD_TEST_DB` (env var) | tests | `baseball_scorecard_test` connection string |
| `Database:MigrateOnStartup` / `Database__MigrateOnStartup` | app | `true` by default; `false` skips applying migrations on startup |

Update this file as the app takes shape: purpose, main projects, data storage, how to run it locally.
