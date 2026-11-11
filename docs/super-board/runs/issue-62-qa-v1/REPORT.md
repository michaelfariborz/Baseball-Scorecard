# Issue #62 QA v1: pass

Head tested: `26b98c1`. Throwaway Postgres DB, migrated to `AddBasesReached`, seeded with old at bats (K, 6-3, GO, BB, E). Then the branch app started and applied `AddFielders` itself.

| AC | Result | Proof |
|---|---|---|
| AC1 GO/FO/SF/FC: fielders + none/DP/TP · E: one fielder, no DP/TP · others: neither | ✅ | `browser.log` AC1 lines (all 13 results) · `AtBatDialogFormDbTests` |
| AC2 only 1–9 joined by hyphens; anything else refused with a message | ✅ | `browser.log` AC2 lines (6/3, 0-3, 8-, SS, 10, E 0, E x refused; 8, 6-3, 4-6-3 accepted) · `BattingGridServiceTests` |
| AC3 `GO 4-6-3 DP` and `E6` in the cell, after reload, on reopen | ✅ | `browser.log` AC3 lines · `db-after.txt` |
| AC4 switching result or Clear removes fielders and DP/TP; old at bats load | ✅ | `browser.log` AC4 lines · `db-before.txt` / `db-after.txt` |

- `dotnet restore && dotnet build --no-restore && dotnet test --no-build`: 351/351 pass.
- Browser: typed into the box and clicked Save without leaving it first (what a scorer does). 0 console errors, 0 page errors, no 5xx/401/403.

## Test gaps (not written)
- Low: a long sequence (`1-2-3-4-5-6-7-8-9`) widens its inning column. Readable, no overflow (`ac3-after-reload-desktop.png`).
- Low: DP/TP with blank fielders saves as `GO TP`. Allowed by design (fielders optional); no AC covers it.
- Low: no automated test that Cancel after editing fielders keeps the saved value. Checked in the browser only.
