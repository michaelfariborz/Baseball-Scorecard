# QA v1 - issue 17 favicon (PR 20, head 681a2dc)

| Check | Result |
|---|---|
| dotnet test | 63 passed, 0 failed |
| GET /favicon.ico | 200 image/x-icon (16 and 32 px PNG entries) |
| GET /favicon.svg | 200 image/svg+xml |
| GET /rosters head | link rel=icon svg + link rel=icon ico present |
| Server log, loading / and /rosters | 0 responses with 404 |
| Icon source | original SVG, no external URL |

Not run: a real browser tab/console view; evidence is HTTP-level (every linked icon resolves 200).
Test gaps (not written): Low - no test asserts the SVG contains red stitching colour.
