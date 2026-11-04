# QA v1 issue 43 @ 1578f00
- Command: dotnet restore && dotnet build --no-restore && dotnet test --no-build
- Result: 269 passed on rerun; first run had 1 unrelated flake (GamePitchersFormDbTests A_bad_IP, passed 2x on rerun)
- AC1: PositionTests.Each_position_has_its_scorecard_abbreviation (12 cases) pass
- AC2: GameBattingGridFormDbTests grid cells show SS (not "6 · Shortstop") pass
- AC3: LineupEditor uses Positions.All; picker test lists 11/12; Grid_shows_abbreviations_including_a_pinch_runner pass
- AC4: numbers 1-10 asserted unchanged; PR=11, PH=12 appended
- UI ACs verified through bUnit render; no browser screenshots taken
