# QA report: issue #2 v1

Head tested: `cbe00c5` · Verdict: pass (5/5 AC)

| AC | Result | Proof |
|---|---|---|
| 1 solution + projects | pass | `dotnet sln BaseballScorecard.slnx list` shows both projects; app csproj `net10.0`, `Program.cs` adds Interactive Server; tests csproj has xunit + bunit and references the app |
| 2 verify command | pass | `verify.log`: exit 0, 1/1 passed |
| 3 bUnit Home h1 | pass | `tests/BaseballScorecard.Tests/HomePageTests.cs:13`; `mutation.log`: h1 changed to "Baseball Scorecards" goes red, restored goes green |
| 4 dotnet run serves / | pass | `http-check.txt`: `/` 200 with the h1, every referenced asset 200, `/nope` 404; `run.log` 0 fail lines; screenshots at 1920x1080, 1024x768, 375x667 |
| 5 .gitignore bin/ obj/ | pass | `git check-ignore -v` matches `.gitignore:12-13` for both projects |

## Test gaps (not written)
- Medium: `bin/` in `.gitignore` is unanchored, so new files under `.claude/bin/` and `.agents/bin/` (super-board helpers) are ignored. Witness: `git check-ignore -v .claude/bin/new-helper.sh` matches `.gitignore:12`. Fix: `!.claude/bin/` and `!.agents/bin/` exceptions, or anchor to `**/src/**/bin/`.
- Low: AC4 has no automated test (would need `WebApplicationFactory` and `public partial class Program`); covered here by the HTTP check.
- Low: Home renders statically (no `@rendermode`); Interactive Server is registered but no page uses it yet.

Note: `dotnet run --no-launch-profile` (Production env) returns 500 for fingerprinted static assets because static web assets load only in Development; expected for an unpublished build, not part of AC4.
