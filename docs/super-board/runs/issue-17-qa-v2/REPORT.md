# QA v2 - issue 17 favicon (PR 20, head 71a0d10)

| Check | Result |
|---|---|
| dotnet restore, build, test | 64 passed, 0 failed |
| GET /favicon.ico | 200 image/x-icon |
| GET /favicon.svg | 200 image/svg+xml |
| GET /rosters head | `<link rel="icon" type="image/svg+xml" href="/favicon.svg">` then `<link rel="icon" sizes="32x32" href="/favicon.ico">` |
| Real Chrome (google-chrome, net-log) on /rosters | requests `/favicon.svg` only, never `/favicon.ico`; 8 responses, all 200, 0 404 |
| Server log | 0 responses with 404 |
| Icon source | original SVG and ICO committed, local hrefs only |

R1 (Chrome picked the ICO because of `sizes="any"`) is fixed: the browser now fetches the SVG.
Test gaps (not written): Low - no test asserts the SVG contains the red stitching colour.
