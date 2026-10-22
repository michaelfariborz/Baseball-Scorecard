# QA report: issue #2 v2

Head tested: `3fe496b` (rebuild 1, R1 fix) · Verdict: pass (5/5 AC)

| AC | Result | Proof |
|---|---|---|
| 1 solution + projects | pass | `dotnet sln BaseballScorecard.slnx list` shows both projects; both csproj `net10.0`; `Program.cs:6-7,25-26` registers Interactive Server; tests csproj has xunit 2.9.3 + bunit 2.12.5 and references the app (`BaseballScorecard.Tests.csproj:23`) |
| 2 verify command | pass | `verify.log`: exit 0, 1/1 passed |
| 3 bUnit Home h1 | pass | `tests/BaseballScorecard.Tests/HomePageTests.cs:13`; `mutation.log`: h1 "Baseball Scorecards" goes red, restored goes green |
| 4 dotnet run serves / | pass | `http-check.txt`: `/` 200 with `<h1>Baseball Scorecard</h1>`, every referenced asset 200, `/nope` 404; `run.log` only the https-port warning of the http profile; screenshots at 1920x1080, 1024x768, 375x667 |
| 5 .gitignore bin/ obj/ | pass | `gitignore-check.txt`: `.gitignore:12-15` ignore bin/obj in both projects, real build output shows `!!`; no bin/obj tracked |

## R1 recheck (reviewer round 1)
Fixed. New files in `.claude/bin/` and `.agents/bin/` are no longer ignored (`gitignore-check.txt`).

## Test gaps (not written)
- Low: bin/obj rules are scoped to `/src/` and `/tests/`; a future project outside those folders (witness `tools/X/bin/`) would not be ignored. Matches the reviewer's suggested fix.
- Low: AC4 has no automated test (would need `WebApplicationFactory` and `public partial class Program`); covered here by the HTTP check.
- Low: Home renders statically (no `@rendermode`); Interactive Server is registered but no page uses it yet.
