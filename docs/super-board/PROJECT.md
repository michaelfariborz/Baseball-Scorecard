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

Update this file as the app takes shape: purpose, main projects, data storage, how to run it locally.
