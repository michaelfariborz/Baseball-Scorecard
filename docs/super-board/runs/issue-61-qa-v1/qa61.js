// Browser QA for issue #61: bases reached on the diamond. Usage: node qa61.js <baseUrl> <outDir>
const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');
const [base, out] = process.argv.slice(2);
const results = [], consoleErrors = [], pageErrors = [], bad = [];
function check(ac, name, ok, detail = '') {
  results.push({ ac, name, ok: !!ok, detail });
  console.log(`${ok ? 'PASS' : 'FAIL'} ${ac} ${name} ${detail}`);
}
const DIAMOND = ['10,18-18,10', '18,10-10,2', '10,2-2,10', '2,10-10,18'];
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
const segs = (page, sel) => page.$$eval(`${sel} svg.bases-diamond line`, ls => ls.map(l =>
  `${l.getAttribute('x1')},${l.getAttribute('y1')}-${l.getAttribute('x2')},${l.getAttribute('y2')}`));
const note = (page, sel) => page.$eval(`${sel} .at-bat-note`, e => e.textContent.trim()).catch(() => null);
const basesChecked = (page, side) => page.evaluate(s => {
  const r = document.querySelector(`#${s}-at-bat-bases input[type=radio]:checked`); return r ? r.value : null; }, side);
async function open(page, side, s, n) { await page.click(id(side, s, n)); await page.waitForSelector(`#${side}-at-bat-dialog`); }
async function closed(page, side) { await page.waitForSelector(`#${side}-at-bat-dialog`, { state: 'detached' }); }
async function pickResult(page, side, code) { await page.locator(`#${side}-at-bat-dialog .at-bat-results input[value="${code}"]`).check(); }
async function pickBases(page, side, n) { await page.locator(`#${side}-at-bat-bases input[value="${n}"]`).check(); }
async function waitSegs(page, sel, n) { await page.waitForFunction(([s, n]) => document.querySelectorAll(`${s} svg.bases-diamond line`).length === n, [sel, n]); }

(async () => {
  fs.mkdirSync(out, { recursive: true });
  const browser = await chromium.launch();
  const ctx = await browser.newContext({ viewport: { width: 1920, height: 1080 } });
  const page = await ctx.newPage();
  page.on('console', m => { if (m.type() === 'error') consoleErrors.push(m.text()); });
  page.on('pageerror', e => pageErrors.push(String(e)));
  page.on('response', r => { if (r.status() >= 500 || r.status() === 401 || r.status() === 403) bad.push(`${r.status()} ${r.url()}`); });
  await ready(page);

  // AC4: rows saved before the migration still show, no segments anywhere
  const old = [await note(page, id('away', 1, 1)), await note(page, id('away', 2, 1)), await note(page, id('home', 1, 1))];
  const anyLines = await page.locator('svg.bases-diamond line').count();
  const anySvg = await page.locator('svg.bases-diamond').count();
  check('AC4', 'pre-migration at bats K, 6-3, BB still show; no diamond drawn', old.join('|') === 'K|6-3|BB' && anyLines === 0 && anySvg === 0, `notes=${old.join('|')} lines=${anyLines} svg=${anySvg}`);
  await page.screenshot({ path: path.join(out, 'ac4-old-at-bats-no-diamond-desktop.png'), fullPage: true });

  // AC1: picker offers none..home, empty cell opens on none
  await open(page, 'away', 3, 2);
  const labels = await page.locator('#away-at-bat-bases label').evaluateAll(es => es.map(e => e.textContent.trim()));
  const values = await page.locator('#away-at-bat-bases input[type=radio]').evaluateAll(es => es.map(e => e.value));
  const legend = (await page.locator('#away-at-bat-bases legend').textContent()).trim();
  const pre = await basesChecked(page, 'away');
  check('AC1', 'dialog offers Bases reached none/1st/2nd/3rd/home, empty cell opens on none', legend === 'Bases reached' && labels.join(',') === 'none,1st,2nd,3rd,home' && values.join(',') === '0,1,2,3,4' && pre === '0', `legend=${legend} labels=${labels} values=${values} checked=${pre}`);
  await pickResult(page, 'away', '2B');
  await pickBases(page, 'away', 2);
  await page.screenshot({ path: path.join(out, 'ac1-dialog-2nd-picked-desktop.png') });
  await page.click('#away-at-bat-save');
  await closed(page, 'away');
  await waitSegs(page, id('away', 3, 2), 2);
  await open(page, 'away', 3, 2);
  const re = await basesChecked(page, 'away');
  await page.screenshot({ path: path.join(out, 'ac1-reopened-shows-2nd-desktop.png') });
  // Cancel after changing bases keeps the saved value
  await pickBases(page, 'away', 4);
  await page.click('#away-at-bat-cancel');
  await closed(page, 'away');
  const afterCancel = await segs(page, id('away', 3, 2));
  check('AC1', 'save keeps 2nd, reopening shows 2nd, cancel after picking home keeps 2nd', re === '2' && afterCancel.length === 2, `reopened=${re} segsAfterCancel=${afterCancel.length}`);
  // keyboard: arrow keys move through the bases group
  await open(page, 'away', 3, 2);
  await page.locator('#away-at-bat-bases input[value="2"]').focus();
  await page.keyboard.press('ArrowRight');
  const kb = await basesChecked(page, 'away');
  await page.keyboard.press('Escape');
  await closed(page, 'away');
  check('AC1', 'arrow key on the bases group moves 2nd to 3rd', kb === '3', `checked=${kb}`);
  // existing pre-migration BB cell: opens on none, set 1st with BB kept
  await open(page, 'home', 1, 1);
  const bbPre = await basesChecked(page, 'home');
  await pickBases(page, 'home', 1);
  await page.click('#home-at-bat-save');
  await closed(page, 'home');
  await waitSegs(page, id('home', 1, 1), 1);
  check('AC1', 'old BB cell opens on none; saving 1st keeps BB and draws one segment', bbPre === '0' && (await note(page, id('home', 1, 1))) === 'BB', `pre=${bbPre} note=${await note(page, id('home', 1, 1))}`);

  // AC2: N = 1..4 draws exactly first N segments in order
  for (let n = 1; n <= 4; n++) {
    await open(page, 'away', 5, n);
    await pickResult(page, 'away', n === 4 ? 'HR' : '1B');
    await pickBases(page, 'away', n);
    await page.click('#away-at-bat-save');
    await closed(page, 'away');
    await waitSegs(page, id('away', 5, n), n);
    const s = await segs(page, id('away', 5, n));
    const box = await page.locator(`${id('away', 5, n)} svg.bases-diamond`).boundingBox();
    const stroke = await page.locator(`${id('away', 5, n)} svg.bases-diamond line`).first().evaluate(e => getComputedStyle(e).stroke);
    check('AC2', `bases ${n} draws first ${n} segment(s) in running order, visible`, JSON.stringify(s) === JSON.stringify(DIAMOND.slice(0, n)) && box && box.width > 10 && box.height > 10 && stroke !== 'none', `segs=${s.join(' ')} box=${box && Math.round(box.width)}x${box && Math.round(box.height)} stroke=${stroke}`);
  }
  await page.locator('#away-batting-grid').screenshot({ path: path.join(out, 'ac2-diamonds-1-to-4-desktop.png') });
  const zoom = await browser.newContext({ viewport: { width: 1920, height: 1080 }, deviceScaleFactor: 3 });
  const zp = await zoom.newPage();
  await zp.goto(`${base}/games/1`, { waitUntil: 'networkidle' });
  await zp.waitForSelector(`${id('away', 5, 4)} svg.bases-diamond`);
  const r = await zp.locator(`${id('away', 5, 1)}`).boundingBox();
  const r4 = await zp.locator(`${id('away', 5, 4)}`).boundingBox();
  await zp.screenshot({ path: path.join(out, 'ac2-diamonds-zoom.png'), clip: { x: r.x - 4, y: r.y - 4, width: r4.x + r4.width - r.x + 8, height: r.height + 8 } });
  await zoom.close();

  // AC3: survives reload, stored, Clear resets to none
  await page.reload({ waitUntil: 'networkidle' });
  await page.waitForSelector(id('away', 5, 4));
  const afterReload = [];
  for (let n = 1; n <= 4; n++) afterReload.push((await segs(page, id('away', 5, n))).length);
  afterReload.push((await segs(page, id('away', 3, 2))).length, (await segs(page, id('home', 1, 1))).length);
  check('AC3', 'after reload segment counts stay 1,2,3,4 / 2 / 1', afterReload.join(',') === '1,2,3,4,2,1', afterReload.join(','));
  await page.screenshot({ path: path.join(out, 'ac3-after-reload-desktop.png'), fullPage: true });
  await ready(page);
  await open(page, 'away', 5, 3);
  const pre3 = await basesChecked(page, 'away');
  await page.click('#away-at-bat-clear');
  await closed(page, 'away');
  await waitSegs(page, id('away', 5, 3), 0);
  const clearedNote = await note(page, id('away', 5, 3));
  await open(page, 'away', 5, 3);
  const post3 = await basesChecked(page, 'away');
  await page.screenshot({ path: path.join(out, 'ac3-cleared-reopens-none-desktop.png') });
  await page.keyboard.press('Escape');
  await closed(page, 'away');
  await page.reload({ waitUntil: 'networkidle' });
  await page.waitForSelector(id('away', 5, 3));
  const cleared = (await segs(page, id('away', 5, 3))).length;
  check('AC3', 'Clear on a 3rd-base cell: opens on 3, clears note + diamond, reopens on none, stays cleared after reload', pre3 === '3' && clearedNote === '' && post3 === '0' && cleared === 0, `pre=${pre3} note="${clearedNote}" post=${post3} segsAfterReload=${cleared}`);

  // Viewports: grid with diamonds, and dialog
  for (const [name, w, h] of [['desktop', 1920, 1080], ['tablet', 1024, 768], ['mobile', 375, 667]]) {
    await page.setViewportSize({ width: w, height: h });
    await page.locator(id('away', 5, 1)).scrollIntoViewIfNeeded();
    await page.screenshot({ path: path.join(out, `${name}.png`) });
    await ready(page);
    await open(page, 'away', 5, 4);
    await page.screenshot({ path: path.join(out, `${name}-dialog.png`) });
    await page.keyboard.press('Escape');
    await closed(page, 'away');
  }

  check('green', 'no console errors / page errors / 5xx / 401 / 403', !consoleErrors.length && !pageErrors.length && !bad.length, JSON.stringify({ consoleErrors, pageErrors, bad }));
  fs.writeFileSync(path.join(out, 'browser-results.json'), JSON.stringify(results, null, 2));
  await browser.close();
  process.exit(results.every(r => r.ok) ? 0 : 1);
})().catch(e => { console.error(e); process.exit(2); });
