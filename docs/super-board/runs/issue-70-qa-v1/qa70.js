// Browser QA for issue #70: caught stealing on an at bat. Usage: node qa70.js <baseUrl> <outDir>
const { chromium } = require('playwright');
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
const note = (page, sel) => page.$eval(`${sel} .at-bat-note`, e => e.textContent.trim()).catch(() => null);
const cs = (page, sel) => page.$eval(`${sel} .at-bat-cs`, e => e.textContent.trim()).catch(() => null);
const segs = (page, sel) => page.locator(`${sel} svg.bases-diamond line`).count();
const has = async (page, sel) => (await page.locator(sel).count()) > 0;
const checked = (page, side, fs) => page.evaluate(([s, f]) => {
  const r = document.querySelector(`#${s}-at-bat-${f} input[type=radio]:checked`); return r ? r.value : null; }, [side, fs]);
async function open(page, side, s, n) { await page.click(id(side, s, n)); await page.waitForSelector(`#${side}-at-bat-dialog`); }
async function closed(page, side) { await page.waitForSelector(`#${side}-at-bat-dialog`, { state: 'detached' }); }
async function pick(page, side, code) { await page.locator(`#${side}-at-bat-dialog .at-bat-results input[value="${code}"]`).check(); }
async function bases(page, side, n) { await page.locator(`#${side}-at-bat-bases input[value="${n}"]`).check(); }
async function csBase(page, side, n) { await page.locator(`#${side}-at-bat-cs input[value="${n}"]`).check(); }
async function waitNote(page, sel, text) { await page.waitForFunction(([s, t]) => (document.querySelector(`${s} .at-bat-note`)?.textContent.trim() ?? '') === t, [sel, text], { timeout: 5000 }).catch(() => null); }
// What a scorer does: click into the box, type, then click Save (no blur first).
async function typeInto(page, sel, text) { await page.click(sel); await page.keyboard.press('Control+A'); await page.keyboard.press('Backspace'); await page.keyboard.type(text); }

(async () => {
  fs.mkdirSync(out, { recursive: true });
  const browser = await chromium.launch();
  const ctx = await browser.newContext({ viewport: { width: 1920, height: 1080 } });
  const page = await ctx.newPage();
  page.on('console', m => { if (m.type() === 'error') consoleErrors.push(m.text()); });
  page.on('pageerror', e => pageErrors.push(String(e)));
  page.on('response', r => { if (r.status() >= 500 || r.status() === 401 || r.status() === 403) bad.push(`${r.status()} ${r.url()}`); });
  await ready(page);

  // AC4 (old rows): at bats saved before the migration still load and show unchanged, with no CS
  const cells = [['away', 1], ['away', 2], ['away', 3], ['home', 1], ['home', 2], ['home', 3]];
  const old = [];
  for (const [s, b] of cells) old.push(`${await note(page, id(s, b, 1))}/${await segs(page, id(s, b, 1))}/${await cs(page, id(s, b, 1))}`);
  check('AC4', 'pre-migration at bats show unchanged with no CS line', old.join('|') === 'K/0/null|GO 4-6-3 DP/0/null|1B/2/null|BB/1/null|E6/1/null|HR/4/null', old.join('|'));
  await page.screenshot({ path: path.join(out, 'ac4-old-at-bats-desktop.png') });
  await open(page, 'away', 3, 1);
  const oldCs = [await checked(page, 'away', 'cs'), await has(page, '#away-at-bat-cs-fielders')];
  await page.keyboard.press('Escape'); await closed(page, 'away');
  check('AC4', 'old 1B cell reopens with caught stealing none and no CS fielders box', oldCs[0] === '0' && !oldCs[1], JSON.stringify(oldCs));

  // AC1: dialog offers none/2nd/3rd/home; fielders box only once a base is picked
  await open(page, 'away', 4, 2);
  const labels = await page.locator('#away-at-bat-cs label').evaluateAll(es => es.map(e => e.textContent.trim()));
  const boxBefore = await has(page, '#away-at-bat-cs-fielders');
  await csBase(page, 'away', 2);
  await page.waitForSelector('#away-at-bat-cs-fielders');
  check('AC1', 'caught stealing choice none/2nd/3rd/home; fielders box appears only after picking a base', labels.join(',') === 'none,2nd,3rd,home' && !boxBefore, `labels=${labels}`);
  // 1B, reached 1st, CS 2nd 26
  await pick(page, 'away', '1B');
  await bases(page, 'away', 1);
  await typeInto(page, '#away-at-bat-cs-fielders', '26');
  await page.screenshot({ path: path.join(out, 'ac1-cs-2nd-26-entered-desktop.png') });
  await page.click('#away-at-bat-save');
  await closed(page, 'away');
  await waitNote(page, id('away', 4, 2), '1B');
  // 2B, reached 2nd, CS 3rd 25 ; 3B reached 3rd, CS home 2-5 (typed hyphenated)
  await open(page, 'away', 5, 2); await pick(page, 'away', '2B'); await bases(page, 'away', 2); await csBase(page, 'away', 3);
  await typeInto(page, '#away-at-bat-cs-fielders', '25'); await page.click('#away-at-bat-save'); await closed(page, 'away');
  await open(page, 'home', 4, 2); await pick(page, 'home', '3B'); await bases(page, 'home', 3); await csBase(page, 'home', 4);
  await typeInto(page, '#home-at-bat-cs-fielders', '1-2'); await page.click('#home-at-bat-save'); await closed(page, 'home');
  // reopen
  await open(page, 'away', 4, 2);
  const re = [await page.locator('#away-at-bat-dialog .at-bat-results input:checked').getAttribute('value'), await checked(page, 'away', 'bases'), await checked(page, 'away', 'cs'), await page.inputValue('#away-at-bat-cs-fielders')];
  await page.screenshot({ path: path.join(out, 'ac1-reopened-cs-2nd-26-desktop.png') });
  await page.keyboard.press('Escape'); await closed(page, 'away');
  check('AC1', 'reopen shows 1B, reached 1st, CS 2nd, fielders 2-6', re.join('|') === '1B|1|2|2-6', re.join('|'));

  // AC2: cell text + diamond, right away and after reload
  const ac2 = async () => [
    `${await note(page, id('away', 4, 2))}/${await cs(page, id('away', 4, 2))}/${await segs(page, id('away', 4, 2))}`,
    `${await note(page, id('away', 5, 2))}/${await cs(page, id('away', 5, 2))}/${await segs(page, id('away', 5, 2))}`,
    `${await note(page, id('home', 4, 2))}/${await cs(page, id('home', 4, 2))}/${await segs(page, id('home', 4, 2))}`].join('|');
  const want = '1B/CS 2-6/1|2B/CS 2-5/2|3B/CS 1-2/3';
  const now = await ac2();
  check('AC2', 'cells show 1B + CS 2-6 (diamond 1 segment), 2B + CS 2-5 (2), 3B + CS 1-2 (3)', now === want, now);
  const aria = await page.getAttribute(id('away', 4, 2), 'aria-label');
  check('AC2', 'cell accessible name names the caught stealing', /caught stealing 2nd 2-6/.test(aria), `aria="${aria}"`);
  await page.reload({ waitUntil: 'networkidle' });
  await page.waitForSelector(id('away', 4, 2));
  const after = await ac2();
  check('AC2', 'after reload cells still show the same CS text and diamonds', after === want, after);
  await page.locator(id('away', 4, 2)).screenshot({ path: path.join(out, 'ac2-cell-1b-cs-2-6-desktop.png') });
  await page.screenshot({ path: path.join(out, 'ac2-after-reload-desktop.png') });
  await ready(page);

  // AC3: refusals
  const refusals = [
    ['K', 0, 2, '26', 'not on base'], ['HR', 4, 4, '26', 'scored'], ['1B', 1, 3, '26', 'skips 2nd'],
    ['2B', 2, 2, '26', 'same base'], ['1B', 1, 2, '20', 'fielder 0'], ['1B', 1, 2, '2/6', 'slash'],
    ['1B', 1, 2, 'C-SS', 'letters'], ['1B', 1, 2, '', 'blank fielders'], ['1B', 1, 2, '2-', 'trailing hyphen']];
  for (const [code, b, t, f, why] of refusals) {
    await open(page, 'away', 6, 3);
    await pick(page, 'away', code); await bases(page, 'away', b); await csBase(page, 'away', t);
    await typeInto(page, '#away-at-bat-cs-fielders', f);
    await page.click('#away-at-bat-save');
    const msg = await page.waitForSelector('#away-at-bat-error', { timeout: 4000 }).then(e => e.textContent()).catch(() => null);
    const stillOpen = await has(page, '#away-at-bat-dialog');
    if (why === 'skips 2nd') await page.screenshot({ path: path.join(out, 'ac3-wrong-base-refused-desktop.png') });
    if (why === 'not on base') await page.screenshot({ path: path.join(out, 'ac3-not-on-base-refused-desktop.png') });
    await page.keyboard.press('Escape'); await closed(page, 'away');
    const cell = await note(page, id('away', 6, 3));
    check('AC3', `${code} reached ${b} CS ${t} "${f}" (${why}) refused, dialog stays open, cell blank`, msg && msg.length > 10 && stillOpen && cell === '', `msg="${msg}" cell="${cell}"`);
  }
  // refusing on an existing saved cell leaves its saved value unchanged
  await open(page, 'away', 4, 2);
  await csBase(page, 'away', 3);
  await page.click('#away-at-bat-save');
  const keepMsg = await page.waitForSelector('#away-at-bat-error', { timeout: 4000 }).then(e => e.textContent()).catch(() => null);
  await page.keyboard.press('Escape'); await closed(page, 'away');
  check('AC3', 'refused edit on a saved cell keeps 1B / CS 2-6', keepMsg && (await cs(page, id('away', 4, 2))) === 'CS 2-6', `msg="${keepMsg}"`);

  // AC4: picking none drops CS; Clear removes it
  await open(page, 'away', 5, 2); await csBase(page, 'away', 0);
  await page.click('#away-at-bat-save'); await closed(page, 'away');
  await page.waitForTimeout(300);
  check('AC4', 'saving 2B with caught stealing none removes the CS line', (await note(page, id('away', 5, 2))) === '2B' && (await cs(page, id('away', 5, 2))) === null, `cs=${await cs(page, id('away', 5, 2))}`);
  await open(page, 'home', 4, 2); await page.click('#home-at-bat-clear'); await closed(page, 'home');
  await waitNote(page, id('home', 4, 2), '');
  await page.reload({ waitUntil: 'networkidle' });
  await ready(page);
  const clr = [await note(page, id('home', 4, 2)), await cs(page, id('home', 4, 2)), await segs(page, id('home', 4, 2))];
  await open(page, 'home', 4, 2);
  const clrRe = [await checked(page, 'home', 'cs'), await has(page, '#home-at-bat-cs-fielders')];
  await page.screenshot({ path: path.join(out, 'ac4-cleared-reopens-none-desktop.png') });
  await page.keyboard.press('Escape'); await closed(page, 'home');
  check('AC4', 'Clear on 3B / CS 1-2: blank cell, no diamond after reload; reopens CS none', clr.map(String).join('|') === '|null|0' && clrRe[0] === '0' && !clrRe[1], `${clr.map(String).join('|')} reopen=${JSON.stringify(clrRe)}`);
  const old2 = [];
  for (const [s, b] of cells) old2.push(`${await note(page, id(s, b, 1))}/${await segs(page, id(s, b, 1))}/${await cs(page, id(s, b, 1))}`);
  check('AC4', 'pre-migration at bats still unchanged after all edits and reloads', old2.join('|') === old.join('|'), old2.join('|'));

  // Viewports: grid and dialog
  for (const [name, w, h] of [['desktop', 1920, 1080], ['tablet', 1024, 768], ['mobile', 375, 667]]) {
    await page.setViewportSize({ width: w, height: h });
    await ready(page);
    await page.locator(id('away', 4, 2)).scrollIntoViewIfNeeded();
    await page.screenshot({ path: path.join(out, `${name}.png`) });
    await open(page, 'away', 4, 2);
    await page.screenshot({ path: path.join(out, `${name}-dialog.png`) });
    await page.keyboard.press('Escape');
    await closed(page, 'away');
  }

  check('green', 'no console errors / page errors / 5xx / 401 / 403', !consoleErrors.length && !pageErrors.length && !bad.length, JSON.stringify({ consoleErrors, pageErrors, bad }));
  fs.writeFileSync(path.join(out, 'browser-results.json'), JSON.stringify(results, null, 2));
  await browser.close();
  process.exit(results.every(r => r.ok) ? 0 : 1);
})().catch(e => { console.error(e); process.exit(2); });
