# QA v1 issue 57
- dotnet test: 277 passed, 0 failed (incl. BattingGridBorderStyleTests x3)
- AC1 table border: .batting-grid border 1px solid currentColor
- AC2 th (incl. HITS / LOB scope=row th): .batting-grid th border 1px solid
- AC3 td incl. totals row: .batting-grid td border 1px solid
- AC4 border-collapse: collapse on .batting-grid
- AC5 away and home: GamePage renders the same BattingGrid component twice; no scoped css overrides
- Not done: live browser screenshot (no seeded game fixture)
