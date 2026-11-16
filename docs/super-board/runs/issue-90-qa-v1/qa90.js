// Browser QA for issue #90: bases taken on a wild pitch or passed ball. Usage: node qa90.js <baseUrl> <outDir>
// Game 1 holds six at bats saved before AddWildPitchesPassedBalls existed (applied on app startup over them).
const { chromium } = require('playwright-core');
const fs = require('fs');
const path = require('path');
const [base, out] = process.argv.slice(2);
const results = [], consoleErrors = [], pageErrors = [], bad = [];
function check(ac, name, ok, detail = '') {
  results.push({ ac, name, ok: !!ok, detail });
  console.log(`${ok ? 'PASS' : 'FAIL'} ${ac} ${name} ${detail}`);
}
const id = (side, s, n) => `#${side}-slot-${s}-inning-${n}-note`;
async function ready(page) {
  await page.goto(`${base}/games/1`, { waitUntil: 'networkidle' });
  await page.waitForSelector(id('away', 1, 1));
  for (let i = 0; i < 40; i++) {
    await page.click(id('away', 9, 9));
    if (await page.waitForSelector('#away-at-bat-dialog', { timeout: 1000 }).catch(() => null)) {
      await page.keyboard.press('Escape');
      await page.waitForSelector('#away-at-bat-dialog', { state: 'detached' });
      return;
    }
    await page.waitForTimeout(250);
  }
  throw new Error('circuit never became interactive');
}
const part = (page, sel, cls) => page.$eval(`${sel} .${cls}`, e => e.textContent.trim()).catch(() => null);
const segs = (page, sel) => page.locator(`${sel} svg.bases-diamond line`).count();
const has = async (page, sel) => (await page.locator(sel).count()) > 0;
const ticked = (page, side, set) => page.evaluate(([s, k]) =>
  [...document.querySelectorAll(`#${s}-at-bat-${k} input[type=checkbox]`)].filter(c => c.checked).map(c => c.value).join(','), [side, set]);
async function open(page, side, s, n) { await page.click(id(side, s, n)); await page.waitForSelector(`#${side}-at-bat-dialog`); }
async function closed(page, side) { await page.waitForSelector(`#${side}-at-bat-dialog`, { state: 'detached' }); }
async function pick(page, side, code) { await page.locator(`#${side}-at-bat-dialog .at-bat-results input[value="${code}"]`).check(); }
async function bases(page, side, n) { await page.locator(`#${side}-at-bat-bases input[value="${n}"]`).check(); }
async function csBase(page, side, n) { await page.locator(`#${side}-at-bat-cs input[value="${n}"]`).check(); }
async function tick(page, side, set, n) { await page.locator(`#${side}-at-bat-${set} input[value="${n}"]`).click(); }
async function typeInto(page, sel, text) { await page.click(sel); await page.keyboard.press('Control+A'); await page.keyboard.press('Backspace'); await page.keyboard.type(text); }
async function waitPart(page, sel, cls, text) {
  await page.waitForFunction(([s, c, t]) => (document.querySelector(`${s} .${c}`)?.textContent.trim() ?? null) === t, [sel, cls, text], { timeout: 5000 }).catch(() => null);
}
// One at bat: { code, b, sb:[], wp:[], pb:[], cs, csf }
async function enter(page, side, s, n, a) {
  await open(page, side, s, n);
  await pick(page, side, a.code);
  if (a.b !== undefined) await bases(page, side, a.b);
  for (const x of a.sb || []) await tick(page, side, 'sb', x);
  for (const x of a.wp || []) await tick(page, side, 'wp', x);
  for (const x of a.pb || []) await tick(page, side, 'pb', x);
  if (a.cs) { await csBase(page, side, a.cs); await typeInto(page, `#${side}-at-bat-cs-fielders`, a.csf || '25'); }
}
const cellOf = async (page, sel) => [await part(page, sel, 'at-bat-note'), await part(page, sel, 'at-bat-sb'),
  await part(page, sel, 'at-bat-wp'), await part(page, sel, 'at-bat-pb'), await part(page, sel, 'at-bat-cs'), await segs(page, sel)].map(String).join('/');
const oldCells = [['away', 1], ['away', 2], ['away', 3], ['home', 1], ['home', 2], ['home', 3]];
async function oldState(page) {
  const r = [];
  for (const [s, b] of oldCells) r.push(await cellOf(page, id(s, b, 1)));
  return r.join('|');
}

(async () => {
  fs.mkdirSync(out, { recursive: true });
  const browser = await chromium.launch({ executablePath: '/usr/bin/google-chrome' });
  const ctx = await browser.newContext({ viewport: { width: 1920, height: 1080 } });
  const page = await ctx.newPage();
  page.on('console', m => { if (m.type() === 'error') consoleErrors.push(m.text()); });
  page.on('pageerror', e => pageErrors.push(String(e)));
  page.on('response', r => { if (r.status() >= 500 || r.status() === 401 || r.status() === 403) bad.push(`${r.status()} ${r.url()}`); });
  await ready(page);

  // AC5 (old rows): pre-migration at bats load and show unchanged, with no WP / PB line
  const wantOld = 'K/null/null/null/null/0|1B/SB 2,3/null/null/null/3|GO 6-3/null/null/null/null/0|BB/null/null/null/CS 2-6/1|HR/null/null/null/null/4|ꓘ/null/null/null/null/0';
  const old = await oldState(page);
  check('AC5', 'pre-migration at bats show unchanged with no WP / PB line', old === wantOld, old);
  await page.screenshot({ path: path.join(out, 'ac5-old-at-bats-desktop.png') });
  await open(page, 'away', 2, 1);
  const oldTicks = `${await ticked(page, 'away', 'sb')}|${await ticked(page, 'away', 'wp')}|${await ticked(page, 'away', 'pb')}`;
  await page.keyboard.press('Escape'); await closed(page, 'away');
  check('AC5', 'old 1B / SB 2,3 cell reopens with steals ticked and no WP / PB ticked', oldTicks === '2,3||', oldTicks);

  // AC1: dialog offers Wild pitch and Passed ball with 1st, 2nd, 3rd, home
  await open(page, 'away', 4, 2);
  const fs1 = async k => [(await page.locator(`#away-at-bat-${k} legend`).textContent()).trim(),
    (await page.locator(`#away-at-bat-${k} label`).evaluateAll(es => es.map(e => e.textContent.trim()))).join(',')].join(':');
  const wpFs = await fs1('wp'), pbFs = await fs1('pb');
  check('AC1', 'dialog has "Wild pitch" and "Passed ball" choices of 1st, 2nd, 3rd, home, none ticked',
    wpFs === 'Wild pitch:1st,2nd,3rd,home' && pbFs === 'Passed ball:1st,2nd,3rd,home' && (await ticked(page, 'away', 'wp')) === '' && (await ticked(page, 'away', 'pb')) === '', `${wpFs} / ${pbFs}`);
  await page.keyboard.press('Escape'); await closed(page, 'away');

  const saves = [
    ['away', 4, { code: '1B', b: 2, wp: [2] }],                         // 1B, WP 2
    ['away', 5, { code: '1B', b: 4, sb: [2], pb: [3, 4] }],             // 1B, SB 2, PB 3,4
    ['away', 6, { code: 'BB', b: 3, wp: [2], pb: [3] }],                // BB, WP 2, PB 3 (both on one runner)
    ['home', 4, { code: '1B', b: 2, wp: [2], cs: 3, csf: '25' }],       // 1B, WP 2, CS 3 2-5
    ['home', 5, { code: 'K', b: 1, wp: [1] }],                          // K WP
    ['home', 6, { code: 'ꓘ', b: 1, pb: [1] }],                          // ꓘ PB
    ['home', 7, { code: 'K', b: 2, pb: [1], wp: [2] }],                 // K PB, WP 2
    ['home', 8, { code: 'ꓘ', b: 1, wp: [1] }],                          // ꓘ WP
    ['away', 8, { code: '1B', b: 1, wp: [2] }, true],                   // ticked then unticked -> none
  ];
  for (const [side, s, a, untick] of saves) {
    await enter(page, side, s, 2, a);
    if (untick) await tick(page, side, 'wp', 2);
    if (side === 'away' && s === 5) await page.screenshot({ path: path.join(out, 'ac1-1b-sb2-pb3-4-entered-desktop.png') });
    if (side === 'home' && s === 5) await page.screenshot({ path: path.join(out, 'ac3-k-wp-entered-desktop.png') });
    await page.click(`#${side}-at-bat-save`);
    const err = await page.waitForSelector(`#${side}-at-bat-error`, { timeout: 1500 }).then(e => e.textContent()).catch(() => null);
    if (err) { check('AC1', `save ${side} ${s} ${JSON.stringify(a)}`, false, `refused: ${err}`); await page.keyboard.press('Escape'); }
    await closed(page, side);
  }
  await waitPart(page, id('away', 6, 2), 'at-bat-pb', 'PB 3');

  // AC1: reopen shows saved choices
  const reopen = async (side, s) => { await open(page, side, s, 2); const r = `${await ticked(page, side, 'sb')}|${await ticked(page, side, 'wp')}|${await ticked(page, side, 'pb')}`;
    if (side === 'away' && s === 5) await page.screenshot({ path: path.join(out, 'ac1-reopened-sb2-pb3-4-desktop.png') });
    if (side === 'home' && s === 7) await page.screenshot({ path: path.join(out, 'ac3-reopened-k-pb-wp2-desktop.png') });
    await page.keyboard.press('Escape'); await closed(page, side); return r; };
  const re = [await reopen('away', 4), await reopen('away', 5), await reopen('away', 6), await reopen('away', 8)].join(' ; ');
  check('AC1', 'reopen ticks: WP 2; SB 2 + PB 3,4; WP 2 + PB 3; ticked-then-unticked none', re === '|2| ; 2||3,4 ; |2|3 ; ||', re);
  const re3 = [await reopen('home', 5), await reopen('home', 6), await reopen('home', 7)].join(' ; ');
  check('AC3', 'reopen ticks: K WP 1st; ꓘ PB 1st; K PB 1st + WP 2nd', re3 === '|1| ; ||1 ; |2|1', re3);

  // AC2 + AC3: cell text, order and diamond, right away and after reload
  const cells = [['away', 4], ['away', 5], ['away', 6], ['home', 4], ['away', 8], ['home', 5], ['home', 6], ['home', 7], ['home', 8]];
  const cellsNow = async () => { const r = []; for (const [s, b] of cells) r.push(await cellOf(page, id(s, b, 2))); return r; };
  const want = ['1B/null/WP 2/null/null/2', '1B/SB 2/null/PB 3,4/null/4', 'BB/null/WP 2/PB 3/null/3', '1B/null/WP 2/null/CS 2-5/2', '1B/null/null/null/null/1',
    'K WP/null/null/null/null/1', 'ꓘ PB/null/null/null/null/1', 'K PB/null/WP 2/null/null/2', 'ꓘ WP/null/null/null/null/1'];
  const now = await cellsNow();
  check('AC2', 'cells: 1B+WP 2; 1B+SB 2+PB 3,4; BB+WP 2+PB 3; 1B+WP 2+CS; 1B alone; diamonds still to bases reached', now.slice(0, 5).join('|') === want.slice(0, 5).join('|'), now.slice(0, 5).join('|'));
  check('AC3', 'cells: K WP; ꓘ PB; K PB + WP 2; ꓘ WP', now.slice(5).join('|') === want.slice(5).join('|'), now.slice(5).join('|'));
  const order = await page.locator(`${id('away', 5, 2)} span`).evaluateAll(es => es.map(e => e.className).join(','));
  const order2 = await page.locator(`${id('home', 4, 2)} span`).evaluateAll(es => es.map(e => e.className).join(','));
  check('AC2', 'cell order is result, SB, WP/PB, CS', order === 'at-bat-note,at-bat-sb,at-bat-pb' && order2 === 'at-bat-note,at-bat-wp,at-bat-cs', `${order} / ${order2}`);
  const aria = await page.getAttribute(id('away', 6, 2), 'aria-label');
  check('AC2', 'cell accessible name names the wild pitch and passed ball', /reached 3rd, wild pitch 2nd, passed ball 3rd/.test(aria), `aria="${aria}"`);
  await page.reload({ waitUntil: 'networkidle' });
  await page.waitForSelector(id('away', 4, 2));
  const after = await cellsNow();
  check('AC2', 'after reload cells show the same WP / PB / K WP text and diamonds', after.join('|') === want.join('|'), after.join('|'));
  for (const [s, b, f] of [['away', 4, 'ac2-cell-1b-wp2-zoom'], ['away', 5, 'ac2-cell-1b-sb2-pb3-4-zoom'], ['away', 6, 'ac2-cell-bb-wp2-pb3-zoom'],
    ['home', 5, 'ac3-cell-k-wp-zoom'], ['home', 6, 'ac3-cell-kl-pb-zoom'], ['home', 7, 'ac3-cell-k-pb-wp2-zoom']])
    await page.locator(id(s, b, 2)).screenshot({ path: path.join(out, `${f}.png`) });
  await page.screenshot({ path: path.join(out, 'ac2-after-reload-desktop.png') });
  await ready(page);

  // AC4: refusals
  const refusals = [
    [{ code: '1B', b: 1, wp: [2] }, 'WP above bases reached', 'ac4-above-bases-reached-refused-desktop'],
    [{ code: '1B', b: 2, pb: [3] }, 'PB 3rd above bases reached 2nd'],
    [{ code: 'K', b: 0, wp: [1] }, 'K WP 1st but bases reached 0'],
    [{ code: '1B', b: 1, wp: [1] }, '1st on a single', 'ac4-1st-on-single-refused-desktop'],
    [{ code: 'BB', b: 1, pb: [1] }, '1st on a walk'],
    [{ code: '1B', b: 2, pb: [1] }, 'PB 1st on a single'],
    [{ code: '2B', b: 2, wp: [2] }, '2nd on a double', 'ac4-2nd-on-a-double-refused-desktop'],
    [{ code: '3B', b: 4, pb: [3] }, '3rd on a triple'],
    [{ code: 'HR', b: 4, wp: [4] }, 'home on a home run'],
    [{ code: '1B', b: 2, wp: [3], cs: 3 }, 'WP at the CS base', 'ac4-wp-at-cs-base-refused-desktop'],
    [{ code: '1B', b: 3, pb: [4], cs: 4 }, 'PB home at CS home'],
    [{ code: '1B', b: 2, wp: [4], cs: 3 }, 'WP past the CS base'],
    [{ code: '1B', b: 3, sb: [2], wp: [2] }, 'stolen and WP on 2nd', 'ac4-stolen-and-wp-refused-desktop'],
    [{ code: '1B', b: 3, sb: [3], pb: [3] }, 'stolen and PB on 3rd'],
    [{ code: '1B', b: 3, wp: [3], pb: [3] }, 'WP and PB on 3rd'],
    [{ code: 'K', b: 1, wp: [1], pb: [1] }, 'K WP and PB on 1st'],
  ];
  for (const [a, why, shot] of refusals) {
    await enter(page, 'away', 7, 3, a);
    await page.click('#away-at-bat-save');
    const msg = await page.waitForSelector('#away-at-bat-error', { timeout: 4000 }).then(e => e.textContent()).catch(() => null);
    const stillOpen = await has(page, '#away-at-bat-dialog');
    if (shot) await page.screenshot({ path: path.join(out, `${shot}.png`) });
    await page.keyboard.press('Escape'); await closed(page, 'away');
    const cell = await part(page, id('away', 7, 3), 'at-bat-note');
    check('AC4', `${JSON.stringify(a)} (${why}) refused, dialog stays open, cell blank`, msg && msg.length > 10 && stillOpen && cell === '', `msg="${msg}" cell="${cell}"`);
  }
  // refused edit on a saved cell keeps its saved marks
  await open(page, 'away', 5, 2); await bases(page, 'away', 3);
  await page.click('#away-at-bat-save');
  const keepMsg = await page.waitForSelector('#away-at-bat-error', { timeout: 4000 }).then(e => e.textContent()).catch(() => null);
  await page.keyboard.press('Escape'); await closed(page, 'away');
  check('AC4', 'lowering bases reached below a saved PB 4 is refused and keeps 1B / SB 2 / PB 3,4', keepMsg && (await cellOf(page, id('away', 5, 2))) === '1B/SB 2/null/PB 3,4/null/4', `msg="${keepMsg}"`);
  // changing a K WP to a single is refused (1st only on a strikeout)
  await open(page, 'home', 5, 2); await pick(page, 'home', '1B');
  await page.click('#home-at-bat-save');
  const kMsg = await page.waitForSelector('#home-at-bat-error', { timeout: 4000 }).then(e => e.textContent()).catch(() => null);
  await page.keyboard.press('Escape'); await closed(page, 'home');
  check('AC4', 'changing a saved K WP to 1B with WP 1st still ticked is refused and keeps K WP', kMsg && (await cellOf(page, id('home', 5, 2))) === 'K WP/null/null/null/null/1', `msg="${kMsg}"`);

  // AC5: Clear removes WP and PB with the rest of the cell
  for (const [s, b] of [['away', 6], ['home', 7]]) { await open(page, s, b, 2); await page.click(`#${s}-at-bat-clear`); await closed(page, s); await waitPart(page, id(s, b, 2), 'at-bat-note', ''); }
  await page.reload({ waitUntil: 'networkidle' });
  await ready(page);
  const clr = [await cellOf(page, id('away', 6, 2)), await cellOf(page, id('home', 7, 2))].join('|');
  await open(page, 'away', 6, 2);
  const clrRe = `${await ticked(page, 'away', 'wp')}|${await ticked(page, 'away', 'pb')}`;
  await page.screenshot({ path: path.join(out, 'ac5-cleared-reopens-no-marks-desktop.png') });
  await page.keyboard.press('Escape'); await closed(page, 'away');
  await open(page, 'home', 7, 2);
  const clrRe2 = `${await ticked(page, 'home', 'wp')}|${await ticked(page, 'home', 'pb')}`;
  await page.keyboard.press('Escape'); await closed(page, 'home');
  check('AC5', 'Clear on BB/WP 2/PB 3 and K PB/WP 2: blank cells, no WP/PB, no diamond after reload; reopen with nothing ticked',
    clr === '/null/null/null/null/0|/null/null/null/null/0' && clrRe === '|' && clrRe2 === '|', `${clr} reopen="${clrRe}" "${clrRe2}"`);
  const old2 = await oldState(page);
  check('AC5', 'pre-migration at bats still unchanged after all edits and reloads', old2 === wantOld, old2);

  // Viewports: grid and dialog
  for (const [name, w, h] of [['desktop', 1920, 1080], ['tablet', 1024, 768], ['mobile', 375, 667]]) {
    await page.setViewportSize({ width: w, height: h });
    await ready(page);
    await page.locator(id('away', 5, 2)).scrollIntoViewIfNeeded();
    await page.screenshot({ path: path.join(out, `${name}.png`) });
    await open(page, 'away', 5, 2);
    await page.locator('#away-at-bat-pb').scrollIntoViewIfNeeded();
    await page.screenshot({ path: path.join(out, `${name}-dialog.png`) });
    await page.keyboard.press('Escape');
    await closed(page, 'away');
  }

  check('green', 'no console errors / page errors / 5xx / 401 / 403', !consoleErrors.length && !pageErrors.length && !bad.length, JSON.stringify({ consoleErrors, pageErrors, bad }));
  fs.writeFileSync(path.join(out, 'browser-results.json'), JSON.stringify(results, null, 2));
  await browser.close();
  process.exit(results.every(r => r.ok) ? 0 : 1);
})().catch(e => { console.error(e); process.exit(2); });
