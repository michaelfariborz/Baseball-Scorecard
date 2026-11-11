# Issue #61 QA v1: pass

Head tested: `8b461c9`. Throwaway Postgres DB, migrated to `AddPitchingLines`, seeded with old at bats (K, 6-3, BB). Then the branch app started and applied `AddBasesReached` itself.

| AC | Result | Proof |
|---|---|---|
| AC1 picker none to home, save, reopen | ✅ | `browser.log` AC1 lines · `AtBatDialogFormDbTests` |
| AC2 first N segments in order | ✅ | `browser.log` AC2 lines · `ac2-diamonds-zoom.png` |
| AC3 reload, stored, Clear to none | ✅ | `browser.log` AC3 lines · `db-after.txt` |
| AC4 no bases = no segments, old rows load | ✅ | `browser.log` AC4 line · `db-after.txt` (old rows `BasesReached` 0) |

- `dotnet restore && dotnet build --no-restore && dotnet test --no-build`: 301/301 pass.
- Browser: 0 console errors, 0 page errors, no 5xx/401/403.

## Test gaps (not written)
- Low: no automated test that Cancel after changing bases keeps the saved value. Checked in the browser only.
- Low: the diamond is drawn over the result code in the cell. Both stay readable at every viewport (`mobile.png`).
