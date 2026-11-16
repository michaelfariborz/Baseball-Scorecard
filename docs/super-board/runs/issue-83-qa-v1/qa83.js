// Browser QA for issue #83: stolen bases on an at bat. Usage: node qa83.js <baseUrl> <outDir>
// Game 1 holds six at bats saved before AddStolenBases existed (applied on app startup over them).
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
const steals = (page, side) => page.evaluate(s =>
  [...document.querySelectorAll(`#${s}-at-bat-sb input[type=checkbox]`)].filter(c => c.checked).map(c => c.value).join(','), side);
async function open(page, side, s, n) { await page.click(id(side, s, n)); await page.waitForSelector(`#${side}-at-bat-dialog`); }
async function closed(page, side) { await page.waitForSelector(`#${side}-at-bat-dialog`, { state: 'detached' }); }
async function pick(page, side, code) { await page.locator(`#${side}-at-bat-dialog .at-bat-results input[value="${code}"]`).check(); }
async function bases(page, side, n) { await page.locator(`#${side}-at-bat-bases input[value="${n}"]`).check(); }
async function csBase(page, side, n) { await page.locator(`#${side}-at-bat-cs input[value="${n}"]`).check(); }
async function steal(page, side, n) { await page.locator(`#${side}-at-bat-sb input[value="${n}"]`).click(); }
async function typeInto(page, sel, text) { await page.click(sel); await page.keyboard.press('Control+A'); await page.keyboard.press('Backspace'); await page.keyboard.type(text); }
async function waitPart(page, sel, cls, text) {
  await page.waitForFunction(([s, c, t]) => (document.querySelector(`${s} .${c}`)?.textContent.trim() ?? null) === t, [sel, cls, text], { timeout: 5000 }).catch(() => null);
}
const oldCells = [['away', 1], ['away', 2], ['away', 3], ['home', 1], ['home', 2], ['home', 3]];
async function oldState(page) {
  const r = [];
  for (const [s, b] of oldCells) {
    const sel = id(s, b, 1);
    r.push(`${await part(page, sel, 'at-bat-note')}/${await segs(page, sel)}/${await part(page, sel, 'at-bat-sb')}/${await part(page, sel, 'at-bat-cs')}`);
  }
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

  // AC4 (old rows): pre-migration at bats load and show unchanged, with no SB line
  const wantOld = 'K/0/null/null|GO 4-6-3 DP/0/null/null|1B/2/null/null|BB/1/null/CS 2-6|E6/1/null/null|HR/4/null/null';
  const old = await oldState(page);
  check('AC4', 'pre-migration at bats show unchanged with no SB line', old === wantOld, old);
  await page.screenshot({ path: path.join(out, 'ac4-old-at-bats-desktop.png') });
  await open(page, 'away', 3, 1);
  const oldSb = await steals(page, 'away');
  await page.keyboard.press('Escape'); await closed(page, 'away');
  check('AC4', 'old 1B (reached 2nd) cell reopens with no stolen base ticked', oldSb === '', `ticked="${oldSb}"`);

  // AC1: dialog offers 2nd/3rd/home; 1B reached 3rd, steals 2nd and 3rd
  await open(page, 'away', 4, 2);
  const labels = await page.locator('#away-at-bat-sb label').evaluateAll(es => es.map(e => e.textContent.trim()));
  const legend = await page.locator('#away-at-bat-sb legend').textContent();
  check('AC1', 'dialog has a "Stolen bases" choice of 2nd, 3rd, home, none ticked', legend.trim() === 'Stolen bases' && labels.join(',') === '2nd,3rd,home' && (await steals(page, 'away')) === '', `legend=${legend} labels=${labels}`);
  await pick(page, 'away', '1B'); await bases(page, 'away', 3); await steal(page, 'away', 3); await steal(page, 'away', 2);
  await page.screenshot({ path: path.join(out, 'ac1-1b-steals-2nd-3rd-entered-desktop.png') });
  await page.click('#away-at-bat-save'); await closed(page, 'away');
  await waitPart(page, id('away', 4, 2), 'at-bat-sb', 'SB 2,3');
  // 1B reached 2nd, SB 2, CS 3rd 3-6
  await open(page, 'home', 4, 2); await pick(page, 'home', '1B'); await bases(page, 'home', 2); await steal(page, 'home', 2);
  await csBase(page, 'home', 3); await typeInto(page, '#home-at-bat-cs-fielders', '36');
  await page.click('#home-at-bat-save'); await closed(page, 'home');
  await waitPart(page, id('home', 4, 2), 'at-bat-cs', 'CS 3-6');
  // BB reached home, steals 2nd, 3rd, home; 2B reached 3rd, steals 3rd
  await open(page, 'away', 5, 2); await pick(page, 'away', 'BB'); await bases(page, 'away', 4);
  await steal(page, 'away', 2); await steal(page, 'away', 3); await steal(page, 'away', 4);
  await page.click('#away-at-bat-save'); await closed(page, 'away');
  await open(page, 'away', 6, 2); await pick(page, 'away', '2B'); await bases(page, 'away', 3); await steal(page, 'away', 3);
  await page.click('#away-at-bat-save'); await closed(page, 'away');
  await waitPart(page, id('away', 6, 2), 'at-bat-sb', 'SB 3');
  // untick: a steal ticked then unticked saves none
  await open(page, 'home', 5, 2); await pick(page, 'home', '1B'); await bases(page, 'home', 2); await steal(page, 'home', 2); await steal(page, 'home', 2);
  await page.click('#home-at-bat-save'); await closed(page, 'home');
  await waitPart(page, id('home', 5, 2), 'at-bat-note', '1B');
  // reopen
  await open(page, 'away', 4, 2);
  const re1 = [await page.locator('#away-at-bat-dialog .at-bat-results input:checked').getAttribute('value'), await steals(page, 'away')].join('|');
  await page.screenshot({ path: path.join(out, 'ac1-reopened-steals-2nd-3rd-desktop.png') });
  await page.keyboard.press('Escape'); await closed(page, 'away');
  await open(page, 'home', 4, 2); const re2 = await steals(page, 'home'); await page.keyboard.press('Escape'); await closed(page, 'home');
  await open(page, 'home', 5, 2); const re3 = await steals(page, 'home'); await page.keyboard.press('Escape'); await closed(page, 'home');
  check('AC1', 'reopen shows saved steals: 1B ticks 2nd+3rd; 1B/CS ticks 2nd; ticked-then-unticked ticks none', re1 === '1B|2,3' && re2 === '2' && re3 === '', `${re1} / ${re2} / "${re3}"`);

  // AC2: cell text, order and diamond, right away and after reload
  const cellsNow = async () => {
    const r = [];
    for (const [s, b] of [['away', 4], ['home', 4], ['away', 5], ['away', 6], ['home', 5]]) {
      const sel = id(s, b, 2);
      r.push(`${await part(page, sel, 'at-bat-note')}/${await part(page, sel, 'at-bat-sb')}/${await part(page, sel, 'at-bat-cs')}/${await segs(page, sel)}`);
    }
    return r.join('|');
  };
  const want = '1B/SB 2,3/null/3|1B/SB 2/CS 3-6/2|BB/SB 2,3,4/null/4|2B/SB 3/null/3|1B/null/null/2';
  const now = await cellsNow();
  check('AC2', 'cells show 1B+SB 2,3; 1B+SB 2+CS 3-6; BB+SB 2,3,4; 2B+SB 3; 1B alone; diamonds still to bases reached', now === want, now);
  const order = await page.locator(`${id('home', 4, 2)} span`).evaluateAll(es => es.map(e => e.className).join(','));
  check('AC2', 'cell order is result, SB, CS', order === 'at-bat-note,at-bat-sb,at-bat-cs', order);
  const aria = await page.getAttribute(id('home', 4, 2), 'aria-label');
  check('AC2', 'cell accessible name names the steal before the caught stealing', /reached 2nd, stole 2nd, caught stealing 3rd 3-6/.test(aria), `aria="${aria}"`);
  await page.reload({ waitUntil: 'networkidle' });
  await page.waitForSelector(id('away', 4, 2));
  const after = await cellsNow();
  check('AC2', 'after reload cells show the same SB / CS text and diamonds', after === want, after);
  await page.locator(id('home', 4, 2)).screenshot({ path: path.join(out, 'ac2-cell-1b-sb-2-cs-3-6-zoom.png') });
  await page.locator(id('away', 4, 2)).screenshot({ path: path.join(out, 'ac2-cell-1b-sb-2-3-zoom.png') });
  await page.screenshot({ path: path.join(out, 'ac2-after-reload-desktop.png') });
  await ready(page);

  // AC3: refusals (code, bases, steals, csBase, why)
  const refusals = [
    ['1B', 1, [2], 0, 'above bases reached'], ['1B', 2, [2, 3], 0, '3rd above bases reached 2nd'],
    ['K', 0, [2], 0, 'not on base'], ['2B', 2, [2], 0, '2nd on a double'], ['2B', 3, [2, 3], 0, '2nd on a double with 3rd'],
    ['3B', 4, [3], 0, '3rd on a triple'], ['HR', 4, [4], 0, 'home on a home run'],
    ['1B', 2, [2, 3], 3, 'steal at the CS base'], ['1B', 3, [4], 4, 'home at CS home']];
  for (const [code, b, sb, cs, why] of refusals) {
    await open(page, 'away', 7, 3);
    await pick(page, 'away', code); await bases(page, 'away', b);
    for (const s of sb) await steal(page, 'away', s);
    if (cs) { await csBase(page, 'away', cs); await typeInto(page, '#away-at-bat-cs-fielders', '25'); }
    await page.click('#away-at-bat-save');
    const msg = await page.waitForSelector('#away-at-bat-error', { timeout: 4000 }).then(e => e.textContent()).catch(() => null);
    const stillOpen = await has(page, '#away-at-bat-dialog');
    if (why === 'above bases reached') await page.screenshot({ path: path.join(out, 'ac3-above-bases-reached-refused-desktop.png') });
    if (why === '2nd on a double') await page.screenshot({ path: path.join(out, 'ac3-2nd-on-a-double-refused-desktop.png') });
    if (why === 'steal at the CS base') await page.screenshot({ path: path.join(out, 'ac3-steal-at-cs-base-refused-desktop.png') });
    await page.keyboard.press('Escape'); await closed(page, 'away');
    const cell = await part(page, id('away', 7, 3), 'at-bat-note');
    check('AC3', `${code} reached ${b} SB ${sb} CS ${cs} (${why}) refused, dialog stays open, cell blank`, msg && msg.length > 10 && stillOpen && cell === '', `msg="${msg}" cell="${cell}"`);
  }
  // refused edit on a saved cell keeps its saved steals
  await open(page, 'away', 4, 2); await bases(page, 'away', 2);
  await page.click('#away-at-bat-save');
  const keepMsg = await page.waitForSelector('#away-at-bat-error', { timeout: 4000 }).then(e => e.textContent()).catch(() => null);
  await page.keyboard.press('Escape'); await closed(page, 'away');
  check('AC3', 'lowering bases reached below a saved steal is refused and keeps 1B / SB 2,3', keepMsg && (await part(page, id('away', 4, 2), 'at-bat-sb')) === 'SB 2,3', `msg="${keepMsg}"`);

  // AC4: Clear removes steals with the rest of the cell
  await open(page, 'away', 5, 2); await page.click('#away-at-bat-clear'); await closed(page, 'away');
  await waitPart(page, id('away', 5, 2), 'at-bat-note', '');
  await page.reload({ waitUntil: 'networkidle' });
  await ready(page);
  const clr = [await part(page, id('away', 5, 2), 'at-bat-note'), await part(page, id('away', 5, 2), 'at-bat-sb'), await segs(page, id('away', 5, 2))];
  await open(page, 'away', 5, 2);
  const clrRe = await steals(page, 'away');
  await page.screenshot({ path: path.join(out, 'ac4-cleared-reopens-no-steals-desktop.png') });
  await page.keyboard.press('Escape'); await closed(page, 'away');
  check('AC4', 'Clear on BB / SB 2,3,4: blank cell, no SB, no diamond after reload; reopens with no steal ticked', clr.map(String).join('|') === '|null|0' && clrRe === '', `${clr.map(String).join('|')} reopen="${clrRe}"`);
  const old2 = await oldState(page);
  check('AC4', 'pre-migration at bats still unchanged after all edits and reloads', old2 === wantOld, old2);

  // Viewports: grid and dialog
  for (const [name, w, h] of [['desktop', 1920, 1080], ['tablet', 1024, 768], ['mobile', 375, 667]]) {
    await page.setViewportSize({ width: w, height: h });
    await ready(page);
    await page.locator(id('home', 4, 2)).scrollIntoViewIfNeeded();
    await page.screenshot({ path: path.join(out, `${name}.png`) });
    await open(page, 'home', 4, 2);
    await page.locator('#home-at-bat-sb').scrollIntoViewIfNeeded();
    await page.screenshot({ path: path.join(out, `${name}-dialog.png`) });
    await page.keyboard.press('Escape');
    await closed(page, 'home');
  }

  check('green', 'no console errors / page errors / 5xx / 401 / 403', !consoleErrors.length && !pageErrors.length && !bad.length, JSON.stringify({ consoleErrors, pageErrors, bad }));
  fs.writeFileSync(path.join(out, 'browser-results.json'), JSON.stringify(results, null, 2));
  await browser.close();
  process.exit(results.every(r => r.ok) ? 0 : 1);
})().catch(e => { console.error(e); process.exit(2); });
