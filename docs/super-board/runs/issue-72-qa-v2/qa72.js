// Browser QA v2 for issue #72 (position changes during a game), after the R1/R2 rebuild at 34b25d4.
// Usage: NODE_PATH=<dir with playwright-core> node qa72.js <baseUrl> <outDir>
// Seed (seed.sql): away Cubs, players 1-9 start (slot 1 CF, slot 6 RF), bench 10 Bench Hitter, 11 Bench Spare.
// The script adds Bench Hitter as a PH for slot 6 in the 7th through the substitute form, then tests the ACs.
const { chromium } = require('playwright-core');
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
const slotRows = (page, s) => page.$$eval(`#away-batting-grid tr[data-slot="${s}"]`, trs => trs.map(tr => {
  const tds = [...tr.querySelectorAll('td')].slice(0, 4);
  return tds.map(td => (td.querySelector('.player-cell') ?? td).textContent.replace('×', '').trim()).join('|');
}));
const errorText = page => page.$eval('#away-batting-grid-error', e => e.textContent.trim()).catch(() => null);
const changeCount = page => page.locator('#away-pos-changes li').count();
// Picks a value in a select; with force, adds the option first (a value the form does not offer).
async function pick(page, sel, value, force) {
  if (force) {
    await page.$eval(sel, (el, v) => {
      if (![...el.options].some(o => o.value === v)) { const o = document.createElement('option'); o.value = v; o.textContent = v; el.appendChild(o); }
      el.value = v; el.dispatchEvent(new Event('change', { bubbles: true }));
    }, String(value));
  } else {
    await page.selectOption(sel, String(value));
  }
}
async function setInning(page, sel, inning) {
  await page.fill(sel, inning === null ? '' : String(inning));
  await page.locator(sel).dispatchEvent('change');
}
async function addSub(page, slot, playerId, kind, inning) {
  await page.selectOption('#away-sub-slot', String(slot));
  await page.selectOption('#away-sub-player', String(playerId));
  await page.selectOption('#away-sub-kind', kind);
  await setInning(page, '#away-sub-inning', inning);
  await page.waitForFunction(() => !document.querySelector('#away-sub-add').disabled, null, { timeout: 3000 }).catch(() => null);
  await page.click('#away-sub-add');
  await page.waitForTimeout(600);
}
async function changePos(page, playerId, position, inning, { forcePlayer = false, forcePosition = false } = {}) {
  await pick(page, '#away-pos-player', playerId, forcePlayer);
  await pick(page, '#away-pos-position', position, forcePosition);
  await setInning(page, '#away-pos-inning', inning);
  await page.waitForFunction(() => !document.querySelector('#away-pos-add').disabled, null, { timeout: 3000 }).catch(() => null);
  await page.click('#away-pos-add');
  await page.waitForTimeout(600);
}
const eq = (a, b) => JSON.stringify(a) === JSON.stringify(b);
const gridShot = (page, name) => page.locator('#away-batting-grid').screenshot({ path: path.join(out, name) });

(async () => {
  fs.mkdirSync(out, { recursive: true });
  const browser = await chromium.launch({ channel: 'chrome' });
  const ctx = await browser.newContext({ viewport: { width: 1920, height: 1080 } });
  const page = await ctx.newPage();
  page.on('console', m => { if (m.type() === 'error') consoleErrors.push(m.text()); });
  page.on('pageerror', e => pageErrors.push(String(e)));
  page.on('response', r => { if (r.status() >= 500 || r.status() === 401 || r.status() === 403) bad.push(`${r.status()} ${r.url()}`); });
  await ready(page);

  // AC4 (old game): a game saved with no position changes shows its starting positions and no change list.
  const before = [await slotRows(page, 1), await slotRows(page, 6)];
  check('AC4', 'a game with no changes shows starting positions only', eq(before, [['1|Cub Player1|CF|'], ['6|Cub Player6|RF|']]), JSON.stringify(before));
  check('AC4', 'no change list when there are no changes', (await page.locator('#away-pos-changes').count()) === 0);
  await gridShot(page, 'before-desktop.png');

  // Setup: Bench Hitter (10) pinch hits for slot 6 in the 7th.
  await addSub(page, 6, 10, 'PinchHitter', 7);
  check('Setup', 'PH added for slot 6 in the 7th', eq(await slotRows(page, 6), ['6|Cub Player6|RF|', '10|Bench Hitter|PH|7']), JSON.stringify(await slotRows(page, 6)));

  // AC1: the PH stays in at CF from the 8th.
  await changePos(page, 10, 'CenterField', 8);
  const ac1 = await slotRows(page, 6);
  check('AC1', 'PH row shows "PH, CF"', eq(ac1, ['6|Cub Player6|RF|', '10|Bench Hitter|PH, CF|7']), JSON.stringify(ac1));
  check('AC1', 'no error after a valid change', (await errorText(page)) === null, String(await errorText(page)));
  await gridShot(page, 'ac1-ph-to-cf-desktop.png');

  // AC2: the starting CF moves to RF from the 8th.
  await changePos(page, 1, 'RightField', 8);
  const ac2 = await slotRows(page, 1);
  check('AC2', 'starting CF row shows "CF, RF"', eq(ac2, ['1|Cub Player1|CF, RF|']), JSON.stringify(ac2));
  check('AC2', 'change list shows both changes', (await changeCount(page)) === 2, `${await changeCount(page)}`);
  await gridShot(page, 'ac2-cf-to-rf-desktop.png');

  // AC3: refusals, each with a message and no new change.
  const refusals = [
    ['replaced starter (Player6) in the 8th', [6, 'CenterField', 8], 'That player is not in the game in that inning.'],
    ['bench player who never came in (Spare)', [11, 'LeftField', 8, { forcePlayer: true }], 'That player is not in the game in that inning.'],
    ['inning 10 in a 9-inning game', [2, 'LeftField', 10], 'The inning must be from 1 to 9.'],
    ['inning 0', [2, 'LeftField', 0], 'The inning must be from 1 to 9.'],
    ['PH before he came in (inning 6)', [10, 'LeftField', 6], 'That player came into the game in inning 7: pick inning 7 or later.'],
    ['position PH', [2, 'PinchHitter', 5, { forcePosition: true }], 'Pick a fielding position or DH: PH and PR are not positions to move to.'],
    ['position PR', [2, 'PinchRunner', 5, { forcePosition: true }], 'Pick a fielding position or DH: PH and PR are not positions to move to.'],
  ];
  const offered = await page.$$eval('#away-pos-position option', os => os.map(o => o.textContent.trim()));
  check('AC3', 'position list offers no PH or PR', !offered.includes('PH') && !offered.includes('PR'), JSON.stringify(offered));
  for (const [name, args, msg] of refusals) {
    await changePos(page, ...args);
    const e = await errorText(page);
    check('AC3', `refused: ${name}`, e === msg, String(e));
    check('AC3', `nothing saved: ${name}`, (await changeCount(page)) === 2, `${await changeCount(page)}`);
    if (name.startsWith('PH before')) await page.screenshot({ path: path.join(out, 'ac3-before-entered-refused-desktop.png'), fullPage: true });
    if (name.startsWith('replaced')) await page.screenshot({ path: path.join(out, 'ac3-not-in-game-refused-desktop.png'), fullPage: true });
  }
  check('AC3', 'Pos column unchanged after refusals', eq([await slotRows(page, 1), await slotRows(page, 6), await slotRows(page, 2)],
    [['1|Cub Player1|CF, RF|'], ['6|Cub Player6|RF|', '10|Bench Hitter|PH, CF|7'], ['2|Cub Player2|SS|']]));

  // AC4: reload keeps both; removing CF→RF puts Player1 back to CF, and that sticks after a reload.
  await ready(page);
  const reload = [await slotRows(page, 1), await slotRows(page, 6)];
  check('AC4', 'after reload both changes still show', eq(reload, [['1|Cub Player1|CF, RF|'], ['6|Cub Player6|RF|', '10|Bench Hitter|PH, CF|7']]), JSON.stringify(reload));
  await page.click('#away-pos-changes button[aria-label="Remove position change of Cub Player1 in inning 8"]');
  await page.waitForTimeout(600);
  check('AC4', 'removing the change puts Player1 back to CF', eq(await slotRows(page, 1), ['1|Cub Player1|CF|']), JSON.stringify(await slotRows(page, 1)));
  await ready(page);
  const after = [await slotRows(page, 1), await slotRows(page, 6)];
  check('AC4', 'removal sticks after reload; PH change kept', eq(after, [['1|Cub Player1|CF|'], ['6|Cub Player6|RF|', '10|Bench Hitter|PH, CF|7']]), JSON.stringify(after));
  await gridShot(page, 'ac4-removed-after-reload-desktop.png');

  // R1: a substitute is refused while the player he replaces has a position change from the sub's inning on.
  await changePos(page, 5, 'LeftField', 8);
  check('R1', 'slot 5 3B moves to LF from the 8th', eq(await slotRows(page, 5), ['5|Cub Player5|3B, LF|']), JSON.stringify(await slotRows(page, 5)));
  await addSub(page, 5, 11, 'PinchHitter', 7);
  const r1 = await errorText(page);
  check('R1', 'PH for slot 5 in the 7th refused, names inning 8', r1 === 'The player being replaced changes position in inning 8: remove that change first.', String(r1));
  check('R1', 'nothing saved: slot 5 unchanged', eq(await slotRows(page, 5), ['5|Cub Player5|3B, LF|']), JSON.stringify(await slotRows(page, 5)));
  await page.screenshot({ path: path.join(out, 'r1-sub-refused-desktop.png'), fullPage: true });
  await addSub(page, 5, 11, 'Defensive', 9);
  check('R1', 'defensive sub after that change (9th) is allowed and plays LF', eq(await slotRows(page, 5), ['5|Cub Player5|3B, LF|', '11|Bench Spare|LF|9']), JSON.stringify(await slotRows(page, 5)));
  await page.click('#away-batting-grid button[aria-label="Remove substitute Bench Spare"]');
  await page.waitForTimeout(600);
  check('R1', 'removing that sub puts slot 5 back', eq(await slotRows(page, 5), ['5|Cub Player5|3B, LF|']), JSON.stringify(await slotRows(page, 5)));

  // R2: a defensive sub starts at the position the player he replaces holds in his inning.
  await changePos(page, 3, 'FirstBase', 5);
  await addSub(page, 3, 11, 'Defensive', 7);
  const r2 = await slotRows(page, 3);
  check('R2', 'defensive sub for LF-to-1B starter plays 1B, not LF', eq(r2, ['3|Cub Player3|LF, 1B|', '11|Bench Spare|1B|7']), JSON.stringify(r2));
  await gridShot(page, 'r2-defensive-sub-desktop.png');
  await page.click('#away-pos-changes button[aria-label="Remove position change of Cub Player3 in inning 5"]');
  await page.waitForTimeout(600);
  check('R2', 'removing the starter change puts both back to LF', eq(await slotRows(page, 3), ['3|Cub Player3|LF|', '11|Bench Spare|LF|7']), JSON.stringify(await slotRows(page, 3)));
  await changePos(page, 3, 'FirstBase', 5);
  await ready(page);
  check('R2', 'after reload the sub still plays 1B', eq(await slotRows(page, 3), ['3|Cub Player3|LF, 1B|', '11|Bench Spare|1B|7']), JSON.stringify(await slotRows(page, 3)));

  // Put CF→RF back for the viewport shots.
  await changePos(page, 1, 'RightField', 8);
  for (const [name, w, h] of [['desktop', 1920, 1080], ['tablet', 1024, 768], ['mobile', 375, 667]]) {
    await page.setViewportSize({ width: w, height: h });
    await page.waitForTimeout(300);
    await gridShot(page, `${name}.png`);
    const formRight = await page.$eval('#away-change-pos', e => Math.round(e.getBoundingClientRect().right));
    check('UI', `${name}: change-position form fits the viewport`, formRight <= w, `formRight=${formRight}px viewport=${w}px`);
  }

  check('Console', 'no console errors, page errors, or 5xx/401/403', !consoleErrors.length && !pageErrors.length && !bad.length,
    JSON.stringify({ consoleErrors, pageErrors, bad }));
  fs.writeFileSync(path.join(out, 'browser-results.json'), JSON.stringify({ results, consoleErrors, pageErrors, bad }, null, 2));
  await browser.close();
  process.exit(results.every(r => r.ok) ? 0 : 1);
})().catch(e => { console.error(e); process.exit(2); });
