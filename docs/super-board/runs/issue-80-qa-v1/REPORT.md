# QA v1 for #80 (light/dark toggle)

Run against the branch head with the app on localhost and Chrome driven by Playwright (`qa.js`, `focus.js`).
Pages checked: `/not-found` and `/Error` (the data pages need a database login the QA host does not have; they share `MainLayout`).

| AC | Result | Proof |
|---|---|---|
| 1 Toggle on every page, named for target mode | pass | label "Switch to dark mode" / "Switch to light mode" on both pages |
| 2 Click switches tokens, no reload | pass | body bg rgb(250,247,240) to rgb(15,26,18); `window` marker survived the click |
| 3 Kept after reload and on other pages; OS default otherwise | pass | dark kept on reload and on `/Error`; OS dark with no saved choice is dark; saved light beats OS dark |
| 4 First paint, no flash | pass | with `theme.js` and Blazor blocked, saved dark still renders dark: the head script alone sets it |
| 5 AA contrast, 3px focus ring, no scroll at 375px | pass | toggle text on header 12.88:1 dark, 9.75:1 light; keyboard focus ring 3px solid in both; scrollWidth equals clientWidth (375) in both modes |

`dotnet test`: 476 passed.
