# QA v1 issue 46
Command: dotnet restore && dotnet build --no-restore && dotnet test --no-build
Result: Passed 269, Failed 0
AC1: GameBattingGridFormDbTests asserts headers [#, Name, Pos, 1..7]
AC2: inning-header class asserted on inning th only; app.css sets text-align:center; app.css linked in App.razor
AC3: Line score and pitchers files untouched (diff touches 3 files)
