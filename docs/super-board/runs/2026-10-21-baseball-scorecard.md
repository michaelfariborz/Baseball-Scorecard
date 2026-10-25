# Run 2026-10-21 · baseball-scorecard

| Card | Lanes | Final | Column | Detail |
|---|---|---|---|---|
| #2 | build:advanced → qa:advanced → review:bounced | bounced | Ready | R1: unanchored `bin/` in .gitignore hides .claude/bin and .agents/bin; AC1–5 hold; PR #3 |
| #2 | build:advanced → qa:advanced → review:human-gate | human-gate | Blocked | R1 fixed; AC1–5 met; merge gate exit 7: 812 changed lines > 400 cap; PR #3 |
| #2 | (human merge) | merged | Done | PR #3 merged by owner at reviewed head 00bd9f4 → 2939242 on staging; issue closed |
| #4 | build:advanced → qa:advanced → review:human-gate | human-gate | Blocked | AC1–5 met; 14/14 tests; truth-check 90; nit R1 (BOM in csproj); merge gate exit 7: 459 changed lines > 400 cap; PR #9 |
| #4 | (human merge) | merged | Done | PR #9 merged by owner at reviewed head ff7ef21 → d7a0d89 on staging; issue closed |
| #5 | build:advanced → qa:advanced → review:human-gate | human-gate | Blocked | tests 31/31; truth-check 88; nits R1–R5; merge gate exit 7: 693 changed lines > 400 + staging migration not in allowed_envs; PR #10 |
| #5 | (human merge) | merged | Done | PR #10 merged by owner at reviewed head 2fcac19 → c59b04d on staging; issue closed; migration step owner-run |
| #6 | build:blocked | blocked | Blocked | ❓ built at 0dfbd17, 5/5 AC, 54/54 green, but 651 lines > 400 (235 product + 416 test); draft PR #11 with split proposal A (table+service ~291) / B (pages ~360); 3 stray #4 author notes on PR #11 patched to "Superseded" |
| #7 | preflight:blocked | blocked | Blocked | sequenced behind #6: both edit DbContext + migration + ModelSnapshot; blocked-by: 6 |
| #6 | build:advanced → qa:advanced → review:human-gate | human-gate | Blocked | 55/55 tests, 5/5 AC, truth-check 90; merge gate exit 8: AddGames migration needs human apply (no migrate command); PR #11 |
| #12 | preflight:blocked | blocked | Blocked | sequenced behind #6: PR #11 edits Program.cs + adds AddGames migration; blocked-by: 6 |
| #6 | review:advanced | merged | Done | owner done cleared migration gate; 55/55 tests; PR #11 squash 232145b on staging; nit R2 open (double-click Save can insert two games) |
| #12 | build:advanced → qa:advanced → review:advanced | merged | Done | 61/61 tests; truth-check 90; PR #13 merged 1a2be9f on staging; nit R1 (ScorecardDbMigrator.cs:44); wave resumed after usage-limit interruption |
| #7 | build:advanced → qa:advanced → review:bounced | bounced | Ready | 84/84 on branch, but merge gate exit 2: with staging 1a2be9f two StartupMigrationTests fail (AllMigrations lacks AddPlayers); label loop:rebase; PR #14 |
| #7 | build:advanced → qa:advanced → review:blocked | human-gate | Blocked | rebased on 1a2be9f; R1+R2 fixed; 91/91 tests; truth-check 90; merge gate exit 8: AddPlayers migration needs owner done; PR #14 |
| #7 | review:advanced | merged | Done | owner done; head bc342d3 unchanged; 91/91; R1–R3 fixed; PR #14 merged 8486a94 on staging |
| #8 | build:advanced → qa:advanced → review:bounced | bounced | Ready | 113/113, 5/5 AC, truth-check 85; R1 bug: concurrent first saves hit unique index uncaught (LineupService.cs:124); R2: TeamNotInGameMessage untested; loop:rebuild-1; PR #15 |
| #8 | build:advanced → qa:failed | failed | QA | QA v2 passed (115/115, R1+R2 fixed @ 66c9ab9) but resolveReviewThread on R2 thread denied by session permission classifier; card held in QA; PR #15 |
| #16 | build:advanced → qa:advanced → review:advanced | merged | Done | 94/94; truth-check 90; PR #19 squash 7d2c15d on staging |
| #17 | build:advanced → qa:advanced → review:bounced | bounced | Ready | 63/63; AC2 fails R1: ICO link sizes="any" (App.razor:12) makes Chrome skip the SVG; R2 test gap; loop:rebuild-1; PR #20 |
| #18 | preflight:blocked | blocked | Blocked | sequenced behind #16 on NewGame.razor; blocked-by: 16 |
| #8 | (owner resolved R2 thread) | advanced | Review | QA v2 pass stands at 66c9ab9; moved QA → Review by orchestrator |
| #17 | build:advanced → qa:advanced → review:advanced | merged | Done | round 2: R1 (ICO sizes) + R2 fixed; 64/64; PR #20 squash 1ccae3f on staging |
| #18 | build:advanced → qa:advanced → review:advanced | merged | Done | 96/96; no findings; one flaky run from shared test DB across worktrees; PR #21 squash 10614ca on staging |
| #8 | review:interrupted | interrupted | Review | session exited mid-review (wave 12, wf_d05b9a26-58d); no review output; claim swept |
| #8 | review:human-gate | human-gate | Blocked | round 2 clean; R1+R2 fixed; 115/115; truth-check 88; merge gate exit 7: 2054 lines > 800 cap + migration step; PR #15 |
| #8 | review:advanced | merged | Done | owner done; head 66c9ab9 unchanged; 115/115; R1–R3 fixed; PR #15 squash 9833123 on staging |
