# issue-25 QA v1 — Home link on every page

Head under test: `ba55aad` (branch `issue-25-home-link`). Verdict: PASS, 3/3 AC.

## Commands
- `dotnet restore && dotnet build --no-restore && dotnet test --no-build` — 131/131 pass (`verify.log`)
- `node browser-check.mjs http://localhost:5191 <out>` against the app on the dev DB — 16/16 checks pass (`browser-run.log`, `app.log`)

## AC ledger
| AC | Unit / component (bUnit) | Browser (Chrome headless) |
|---|---|---|
| 1 · MainLayout renders "Home" → `/` above the body | `MainLayoutTests.cs:13` (text, href, order before body) | link y=8 above h1 y=47 on all 4 pages; click from `/rosters` lands on `/` |
| 2 · `/rosters`, `/rosters/new`, `/games/new`, `/games/{id}` show it | `MainLayoutTests.cs:24` (no page overrides the layout) + `MainLayoutTests.cs:35` (router wraps pages in MainLayout) | each page shows the link at 1920×1080; `/rosters` also at 1024×768 and 375×667 |
| 3 · page content unchanged | existing page tests still green (131/131) | h1, "New roster" link, roster list, form controls present on each page |

## Test the tests
- Mutant: removed the `<nav><a href="/">Home</a></nav>` line from `MainLayout.razor`.
  `dotnet test --filter MainLayoutTests` went red (2 failed: layout test and router test). Restored; green. Not committed.

## Test gaps (not written)
- Low: the `<nav>` has no accessible name (`aria-label`); one nav on the page, so screen readers still announce it.
- Low: `/rosters/{id}/edit` checked through the `RosterEdit` type (same component as `/rosters/new`), not opened in the browser.
- Low: no page errors or 4xx responses seen at any viewport.
