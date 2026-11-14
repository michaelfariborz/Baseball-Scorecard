// Browser QA v2 for issue #71 after rebuild 1 (R1: two subs in one slot and inning, subs added out of order).
// Usage: node qa71v2.js <baseUrl> <outDir>
// Seed: away roster players 1-9 start (slot n plays position n: scorebook numbers: slot 4 = 2B, slot 5 = 3B, slot 6 = SS),
// bench 10 HitterA, 11 GloveA, 12 RunnerB, 13 GloveB, 14 HitterC, 15 RunnerC. Saved at bats: slot 4 inn 7 K,
// slot 5 inn 5 BB, slot 5 inn 6 2B, slot 6 inn 7 1B.
const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');
const [base, out] = process.argv.slice(2);
const results = [], consoleErrors = [], pageErrors = [], bad = [];
function check(ac, name, ok, detail = '') {
  results.push({ ac, name, ok: !!ok, detail });
  console.log(`${ok ? 'PASS' : 'FAIL'} ${ac} ${name} ${detail}`);
}
const cell = (s, n) => `#away-slot-${s}-inning-${n}-note`;
async function ready(page) {
  await page.goto(`${base}/games/1`, { waitUntil: 'networkidle' });
  await page.waitForSelector(cell(1, 1));
  for (let i = 0; i < 40; i++) {
    await page.click(cell(9, 9));
    if (await page.waitForSelector('#away-at-bat-dialog', { timeout: 1000 }).catch(() => null)) {
      await page.keyboard.press('Escape');
      await page.waitForSelector('#away-at-bat-dialog', { state: 'detached' });
      return;
    }
    await page.waitForTimeout(250);
  }
  throw new Error('circuit never became interactive');
}
const note = (page, s, n) => page.$eval(`${cell(s, n)} .at-bat-note`, e => e.textContent.trim()).catch(() => '');
const slotRows = (page, s) => page.$$eval(`#away-batting-grid tr[data-slot="${s}"]`, trs => trs.map(tr => {
  const tds = [...tr.querySelectorAll('td')].slice(0, 4);
  return tds.map(td => (td.querySelector('.player-cell') ?? td).textContent.replace('×', '').trim()).join('|');
}));
const errorText = page => page.$eval('#away-batting-grid-error', e => e.textContent.trim()).catch(() => null);
async function owner(page, s, n) {
  await page.click(cell(s, n));
  await page.waitForSelector('#away-at-bat-dialog');
  const t = await page.$eval('#away-at-bat-title', e => e.textContent.trim());
  await page.keyboard.press('Escape');
  await page.waitForSelector('#away-at-bat-dialog', { state: 'detached' });
  return t.replace(/, inning \d+$/, '');
}
async function addSub(page, slot, playerId, kind, inning, force = false) {
  await page.selectOption('#away-sub-slot', String(slot));
  if (force) {
    await page.$eval('#away-sub-player', (el, v) => { el.value = v; el.dispatchEvent(new Event('change', { bubbles: true })); }, String(playerId));
  } else {
    await page.selectOption('#away-sub-player', String(playerId));
  }
  await page.selectOption('#away-sub-kind', kind);
  await page.fill('#away-sub-inning', inning === null ? '' : String(inning));
  await page.locator('#away-sub-inning').dispatchEvent('change');
  await page.waitForFunction(() => !document.querySelector('#away-sub-add').disabled, null, { timeout: 3000 }).catch(() => null);
  await page.click('#away-sub-add');
  await page.waitForTimeout(600);
}
const eq = (a, b) => JSON.stringify(a) === JSON.stringify(b);
const shot = (page, name) => page.locator('#away-batting-grid').screenshot({ path: path.join(out, name) });

(async () => {
  fs.mkdirSync(out, { recursive: true });
  const browser = await chromium.launch();
  const ctx = await browser.newContext({ viewport: { width: 1920, height: 1080 } });
  const page = await ctx.newPage();
  page.on('console', m => { if (m.type() === 'error') consoleErrors.push(m.text()); });
  page.on('pageerror', e => pageErrors.push(String(e)));
  page.on('response', r => { if (r.status() >= 500 || r.status() === 401 || r.status() === 403) bad.push(`${r.status()} ${r.url()}`); });
  await ready(page);

  const header = await page.$$eval('#away-batting-grid thead th', ths => ths.slice(0, 5).map(t => t.textContent.trim()).join(','));
  check('AC1', 'grid header has Inn after Pos', header === '#,Name,Pos,Inn,1', header);

  // R1 case a: PH in 7, then a defensive sub for him in 7 (slot 6)
  await addSub(page, 6, 10, 'PinchHitter', 7);
  await addSub(page, 6, 11, 'Defensive', 7);
  const a = await slotRows(page, 6);
  check('AC1', 'a: slot 6 lists starter, PH 7, defensive SS 7 in order', eq(a, ['6|Cub Player6|SS|', '10|Bench HitterA|PH|7', '11|Bench GloveA|SS|7']), JSON.stringify(a));
  const ao = [await owner(page, 6, 6), await owner(page, 6, 7), await owner(page, 6, 8), await owner(page, 6, 9)];
  check('AC2', 'a: inning 6 starter, 7 the PH, 8 and 9 the defensive sub', eq(ao, ['Cub Player6', 'Bench HitterA', 'Bench GloveA', 'Bench GloveA']), JSON.stringify(ao));
  check('AC2', 'a: saved inning-7 at bat stays on its cell (1B)', (await note(page, 6, 7)) === '1B', await note(page, 6, 7));
  await page.click(cell(6, 7)); await page.waitForSelector('#away-at-bat-dialog');
  await page.screenshot({ path: path.join(out, 'r1a-ph-then-defensive-dialog-inning7-desktop.png') });
  await page.keyboard.press('Escape'); await page.waitForSelector('#away-at-bat-dialog', { state: 'detached' });

  // R1 case b: PR in 7, then a defensive sub for him in 7 (slot 4)
  await addSub(page, 4, 12, 'PinchRunner', 7);
  await addSub(page, 4, 13, 'Defensive', 7);
  const b = await slotRows(page, 4);
  check('AC1', 'b: slot 4 lists starter, PR 7, defensive 2B 7 in order', eq(b, ['4|Cub Player4|2B|', '12|Bench RunnerB|PR|7', '13|Bench GloveB|2B|7']), JSON.stringify(b));
  const bo = [await owner(page, 4, 7), await owner(page, 4, 8)];
  check('AC2', 'b: inning 7 stays with the batter, 8 is the defensive sub', eq(bo, ['Cub Player4', 'Bench GloveB']), JSON.stringify(bo));

  // R1 case c: PH in 6 added before a PR in 5 (slot 5)
  await addSub(page, 5, 14, 'PinchHitter', 6);
  await addSub(page, 5, 15, 'PinchRunner', 5);
  const c = await slotRows(page, 5);
  check('AC1', 'c: slot 5 lists PR 5 before PH 6 though added after', eq(c, ['5|Cub Player5|3B|', '15|Bench RunnerC|PR|5', '14|Bench HitterC|PH|6']), JSON.stringify(c));
  const co = [await owner(page, 5, 5), await owner(page, 5, 6), await owner(page, 5, 9)];
  check('AC2', 'c: inning 5 the batter, 6 and 9 the PH', eq(co, ['Cub Player5', 'Bench HitterC', 'Bench HitterC']), JSON.stringify(co));
  check('AC1', 'no error after valid adds', (await errorText(page)) === null, String(await errorText(page)));
  await page.screenshot({ path: path.join(out, 'r1-all-cases-desktop.png'), fullPage: true });

  // AC3 regression: a sub already in the game and inning 10 are refused
  const before = await page.locator('#away-batting-grid tr.substitute').count();
  await addSub(page, 7, 11, 'PinchHitter', 8, true);
  const e1 = await errorText(page);
  check('AC3', 'a sub already in the game is refused', e1 === "That player is already in this team's lineup or has left the game.", String(e1));
  await addSub(page, 7, 1, 'PinchHitter', 10, true);
  const e2 = await errorText(page);
  check('AC3', 'inning 10 is refused with a message', e2 === 'The inning must be from 1 to 9.', String(e2));
  check('AC3', 'refusals add nothing', (await page.locator('#away-batting-grid tr.substitute').count()) === before, `${before}`);

  // AC4: reload keeps everything; removing the PR in slot 5 gives inning 5..6 back correctly
  await ready(page);
  const reload = [await slotRows(page, 6), await slotRows(page, 4), await slotRows(page, 5)];
  check('AC4', 'after reload all three slots list the same rows', eq(reload, [a, b, c]), JSON.stringify(reload));
  const notes = [await note(page, 4, 7), await note(page, 5, 5), await note(page, 5, 6), await note(page, 6, 7)].join('|');
  check('AC4', 'saved at bats unchanged', notes === 'K|BB|2B|1B', notes);
  await page.click('#away-batting-grid tr.substitute[data-slot="5"]:has-text("RunnerC") .sub-remove');
  await page.waitForTimeout(600);
  const c2 = await slotRows(page, 5);
  const co2 = [await owner(page, 5, 5), await owner(page, 5, 6)];
  check('AC4', 'removing the PR leaves starter then PH 6; owners 5 starter, 6 PH', eq(c2, ['5|Cub Player5|3B|', '14|Bench HitterC|PH|6']) && eq(co2, ['Cub Player5', 'Bench HitterC']), `${JSON.stringify(c2)} ${JSON.stringify(co2)}`);
  await page.click('#away-batting-grid tr.substitute[data-slot="6"]:has-text("GloveA") .sub-remove');
  await page.waitForTimeout(600);
  const ao2 = [await owner(page, 6, 7), await owner(page, 6, 8)];
  check('AC4', 'removing the defensive sub gives innings 7 and 8 back to the PH', eq(ao2, ['Bench HitterA', 'Bench HitterA']), JSON.stringify(ao2));
  await ready(page);
  const after = [await slotRows(page, 6), await slotRows(page, 5)];
  check('AC4', 'removals stick after reload', eq(after, [['6|Cub Player6|SS|', '10|Bench HitterA|PH|7'], ['5|Cub Player5|3B|', '14|Bench HitterC|PH|6']]), JSON.stringify(after));

  // Put the removed subs back for the viewport shots
  await addSub(page, 6, 11, 'Defensive', 7);
  await addSub(page, 5, 15, 'PinchRunner', 5);
  for (const [name, w, h] of [['desktop', 1920, 1080], ['tablet', 1024, 768], ['mobile', 375, 667]]) {
    await page.setViewportSize({ width: w, height: h });
    await page.waitForTimeout(300);
    await shot(page, `${name}.png`);
    const formRight = await page.$eval('#away-add-sub', e => Math.round(e.getBoundingClientRect().right));
    check('UI', `${name}: add-substitute form fits the viewport`, formRight <= w, `formRight=${formRight}px viewport=${w}px`);
  }

  check('Console', 'no console errors, page errors, or 5xx/401/403', !consoleErrors.length && !pageErrors.length && !bad.length,
    JSON.stringify({ consoleErrors, pageErrors, bad }));
  fs.writeFileSync(path.join(out, 'browser-results.json'), JSON.stringify({ results, consoleErrors, pageErrors, bad }, null, 2));
  await browser.close();
  process.exit(results.every(r => r.ok) ? 0 : 1);
})().catch(e => { console.error(e); process.exit(2); });
