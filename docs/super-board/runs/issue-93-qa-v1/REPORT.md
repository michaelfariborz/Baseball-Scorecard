# QA report · issue #93 · v1

Head under test: `6f5d2d8` (PR #94). Base: `origin/staging`.

## Verdict
✅ Pass · 2/2 AC

## AC checks
| AC | Check | Result |
|---|---|---|
| 1 · cell key + one input record | `SaveNoteAsync(int gameId, int rosterId, int battingOrder, int inning, BattingNoteInput input, int column = 1)` at `src/BaseballScorecard/Data/BattingGridService.cs:93`; record at `:13`; one production caller `BattingGrid.razor:678` | ✅ |
| 2 · existing cases pass, call sites only, messages same | `verify.log`: build 0 warnings, 572/572 pass. `test-list.txt` is identical to staging's list (572 cases; BattingGridServiceTests 143, AtBatDialogFormDbTests 61, StartupMigrationTests 9 on both). Test diff: every changed line is a `SaveNoteAsync` call or its argument continuation. All 27 `*Message =` lines in `BattingGridService*.cs` hash the same on staging and branch | ✅ |

## Test-gap check
- Mutant: swapped `WildPitchBases` / `PassedBallBases` in the dialog's `BattingNoteInput` (`BattingGrid.razor:677`). 6 `AtBatDialogFormDbTests` went red. Reverted, never committed.
- No High gaps. No new tests needed: behaviour is unchanged and the existing suite pins the record mapping.

## Test gaps (not written)
- Low: `SaveNoteAsync` does not guard a null `input`; the only caller always builds one and nullable reference types flag a null at compile time.

Screenshots: none. The change has no UI effect (signature-only refactor).

Local tests: `dotnet restore && dotnet build --no-restore && dotnet test --no-build`
