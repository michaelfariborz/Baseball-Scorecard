// Browser QA for issue #71: pinch hitters, pinch runners and defensive subs. Usage: node qa71.js <baseUrl> <outDir>
// Seed: away roster players 1-9 start (slot 6 = RF, slot 9 = SS), bench 10 Bench Hitter, 11 Bench Runner, 12 Bench Glove.
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
// Rows of one slot: [number, name, pos, inn] for the starter and each substitute, in order.
const slotRows = (page, s) => page.$$eval(`#away-batting-grid tr[data-slot="${s}"]`, trs => trs.map(tr => {
  const tds = [...tr.querySelectorAll('td')].slice(0, 4);
  return tds.map(td => (td.querySelector('.player-cell') ?? td).textContent.replace('×', '').trim()).join('|');
}));
const errorText = page => page.$eval('#away-batting-grid-error', e => e.textContent.trim()).catch(() => null);
async function dialogTitle(page, s, n) {
  await page.click(cell(s, n));
  await page.waitForSelector('#away-at-bat-dialog');
  const t = await page.$eval('#away-at-bat-title', e => e.textContent.trim());
  await page.keyboard.press('Escape');
  await page.waitForSelector('#away-at-bat-dialog', { state: 'detached' });
  return t;
}
// Fill the add-substitute form. force: set a disabled option through the DOM (what a stale page could send).
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
const rowCount = page => page.locator('#away-batting-grid tr.substitute').count();

(async () => {
  fs.mkdirSync(out, { recursive: true });
  const browser = await chromium.launch();
  const ctx = await browser.newContext({ viewport: { width: 1920, height: 1080 } });
  const page = await ctx.newPage();
  page.on('console', m => { if (m.type() === 'error') consoleErrors.push(m.text()); });
  page.on('pageerror', e => pageErrors.push(String(e)));
  page.on('response', r => { if (r.status() >= 500 || r.status() === 401 || r.status() === 403) bad.push(`${r.status()} ${r.url()}`); });
  await ready(page);

  // AC4 (old rows): lineup and at bats saved before the migration show unchanged; Inn header exists, blank for starters
  const header = await page.$$eval('#away-batting-grid thead th', ths => ths.slice(0, 5).map(t => t.textContent.trim()).join(','));
  check('AC1', 'grid header has the new Inn column after Pos', header === '#,Name,Pos,Inn,1', header);
  const s6 = await slotRows(page, 6);
  const oldNotes = [await note(page, 1, 1), await note(page, 2, 1), await note(page, 3, 1), await note(page, 6, 2), await note(page, 6, 5), await note(page, 6, 7)].join('|');
  check('AC4', 'pre-migration lineup shows starters only, Inn blank', JSON.stringify(s6) === JSON.stringify(['6|Cub Player6|RF|']) && (await rowCount(page)) === 0, JSON.stringify(s6));
  check('AC4', 'pre-migration at bats show unchanged', oldNotes === 'K|1B|GO 6-4-3 DP|2B|BB|1B', oldNotes);
  await page.screenshot({ path: path.join(out, 'ac4-before-subs-desktop.png'), fullPage: true });

  // AC1: add PH, PR and defensive subs with bench players
  await addSub(page, 6, 10, 'PinchHitter', 7);
  await addSub(page, 2, 11, 'PinchRunner', 1);
  await addSub(page, 9, 12, 'Defensive', 4);
  const r6 = await slotRows(page, 6), r2 = await slotRows(page, 2), r9 = await slotRows(page, 9);
  check('AC1', 'PH: slot 6 shows starter then Bench Hitter, Pos PH, Inn 7', JSON.stringify(r6) === JSON.stringify(['6|Cub Player6|RF|', '10|Bench Hitter|PH|7']), JSON.stringify(r6));
  check('AC1', 'PR: slot 2 shows starter then Bench Runner, Pos PR, Inn 1', JSON.stringify(r2) === JSON.stringify(['2|Cub Player2|C|', '11|Bench Runner|PR|1']), JSON.stringify(r2));
  check('AC1', 'Defensive: slot 9 shows starter then Bench Glove, Pos SS (replaced player), Inn 4', JSON.stringify(r9) === JSON.stringify(['9|Cub Player9|SS|', '12|Bench Glove|SS|4']), JSON.stringify(r9));
  check('AC1', 'no error after valid adds', (await errorText(page)) === null, String(await errorText(page)));
  const span = await page.$eval(`#away-batting-grid tr.batter[data-slot="6"] td:nth-child(5)`, td => td.rowSpan);
  check('AC1', 'slot 6 at-bat cells span both rows (one cell per inning)', span === 2, `rowspan=${span}`);
  await page.screenshot({ path: path.join(out, 'ac1-subs-added-desktop.png'), fullPage: true });

  // AC2: who owns each cell, as named by the at-bat dialog
  const t = {
    ph6: await dialogTitle(page, 6, 6), ph7: await dialogTitle(page, 6, 7), ph9: await dialogTitle(page, 6, 9),
    pr1: await dialogTitle(page, 2, 1), pr2: await dialogTitle(page, 2, 2),
    d3: await dialogTitle(page, 9, 3), d4: await dialogTitle(page, 9, 4),
  };
  check('AC2', 'PH in inning 7: inning 6 is the starter, 7 and 9 are the PH', t.ph6 === 'Cub Player6, inning 6' && t.ph7 === 'Bench Hitter, inning 7' && t.ph9 === 'Bench Hitter, inning 9', `${t.ph6} / ${t.ph7} / ${t.ph9}`);
  check('AC2', 'PR in inning 1: inning 1 stays with the batter, inning 2 is the PR', t.pr1 === 'Cub Player2, inning 1' && t.pr2 === 'Bench Runner, inning 2', `${t.pr1} / ${t.pr2}`);
  check('AC2', 'Defensive in inning 4: inning 3 starter, inning 4 the sub', t.d3 === 'Cub Player9, inning 3' && t.d4 === 'Bench Glove, inning 4', `${t.d3} / ${t.d4}`);
  check('AC2', 'existing inning-7 at bat in slot 6 stays on its cell (1B)', (await note(page, 6, 7)) === '1B', await note(page, 6, 7));
  await page.click(cell(6, 8)); await page.waitForSelector('#away-at-bat-dialog');
  await page.screenshot({ path: path.join(out, 'ac2-dialog-names-ph-desktop.png') });
  await page.keyboard.press('Escape'); await page.waitForSelector('#away-at-bat-dialog', { state: 'detached' });

  // AC3: refusals with a message; nothing is added
  const before = await rowCount(page);
  const disabled = await page.$$eval('#away-sub-player option[disabled]', os => os.map(o => o.value).sort((a, b) => a - b).join(','));
  check('AC3', 'players already in the game are disabled in the picker', disabled === '1,10,11,12,2,3,4,5,6,7,8,9'.split(',').sort((a, b) => a - b).join(','), disabled);
  await addSub(page, 7, 1, 'PinchHitter', 5, true);
  const eStarter = await errorText(page);
  check('AC3', 'a starter (in the lineup) is refused with a message', eStarter === "That player is already in this team's lineup or has left the game." && (await rowCount(page)) === before, String(eStarter));
  await page.screenshot({ path: path.join(out, 'ac3-in-lineup-refused-desktop.png'), fullPage: true });
  await addSub(page, 7, 10, 'PinchHitter', 8, true);
  const eSub = await errorText(page);
  check('AC3', 'a substitute already in the game is refused', eSub === "That player is already in this team's lineup or has left the game." && (await rowCount(page)) === before, String(eSub));
  // A player taken out: remove-and-retake is not this; the replaced starter Cub Player6 is refused for another slot.
  await addSub(page, 8, 6, 'Defensive', 8, true);
  const eOut = await errorText(page);
  check('AC3', 'a player taken out of the game (replaced starter) is refused', eOut === "That player is already in this team's lineup or has left the game." && (await rowCount(page)) === before, String(eOut));
  // Inning outside 1..9 needs a bench player: free one by removing nobody; use player 12's slot? Use a fresh roster player: none left,
  // so check inning refusal first (it runs before the player check in the service).
  for (const inn of [0, 10, null]) {
    await addSub(page, 7, 1, 'PinchHitter', inn, true);
    const e = await errorText(page);
    check('AC3', `inning ${inn === null ? 'blank' : inn} is refused with a message`, e === 'The inning must be from 1 to 9.' && (await rowCount(page)) === before, String(e));
  }
  await page.screenshot({ path: path.join(out, 'ac3-inning-refused-desktop.png'), fullPage: true });

  // AC4: subs persist on reload; removing a sub puts the slot back to its earlier player
  await ready(page);
  const afterReload = JSON.stringify([await slotRows(page, 6), await slotRows(page, 2), await slotRows(page, 9)]);
  check('AC4', 'subs still show after a reload', afterReload === JSON.stringify([['6|Cub Player6|RF|', '10|Bench Hitter|PH|7'], ['2|Cub Player2|C|', '11|Bench Runner|PR|1'], ['9|Cub Player9|SS|', '12|Bench Glove|SS|4']]), afterReload);
  await page.click('#away-batting-grid tr.substitute[data-slot="6"] .sub-remove');
  await page.waitForTimeout(600);
  const r6b = await slotRows(page, 6);
  const own7 = await dialogTitle(page, 6, 7);
  check('AC4', 'removing the PH puts slot 6 back to the starter (rows and inning-7 owner)', JSON.stringify(r6b) === JSON.stringify(['6|Cub Player6|RF|']) && own7 === 'Cub Player6, inning 7', `${JSON.stringify(r6b)} / ${own7}`);
  const freed = await page.$eval('#away-sub-player option[value="10"]', o => o.disabled);
  check('AC4', 'the removed sub is offered again in the picker', freed === false, `disabled=${freed}`);
  await ready(page);
  const r6c = await slotRows(page, 6);
  const notesAfter = [await note(page, 1, 1), await note(page, 2, 1), await note(page, 3, 1), await note(page, 6, 2), await note(page, 6, 5), await note(page, 6, 7)].join('|');
  check('AC4', 'after reload the removal stuck and old at bats are unchanged', JSON.stringify(r6c) === JSON.stringify(['6|Cub Player6|RF|']) && notesAfter === 'K|1B|GO 6-4-3 DP|2B|BB|1B', `${JSON.stringify(r6c)} ${notesAfter}`);
  await page.screenshot({ path: path.join(out, 'ac4-ph-removed-desktop.png'), fullPage: true });

  // Re-add the PH so viewport shots show all three kinds of sub
  await addSub(page, 6, 10, 'PinchHitter', 7);
  for (const [name, w, h] of [['desktop', 1920, 1080], ['tablet', 1024, 768], ['mobile', 375, 667]]) {
    await page.setViewportSize({ width: w, height: h });
    await page.waitForTimeout(300);
    await page.locator('#away-batting-grid').screenshot({ path: path.join(out, `${name}.png`) });
    // The page already scrolls sideways at 375px from the line score and the 9-inning grid (before this change);
    // here the new add-substitute form must fit inside the viewport.
    const formRight = await page.$eval('#away-add-sub', e => Math.round(e.getBoundingClientRect().right));
    check('UI', `${name}: add-substitute form fits the viewport`, formRight <= w, `formRight=${formRight}px viewport=${w}px`);
  }

  check('Console', 'no console errors, page errors, or 5xx/401/403', !consoleErrors.length && !pageErrors.length && !bad.length,
    JSON.stringify({ consoleErrors, pageErrors, bad }));
  fs.writeFileSync(path.join(out, 'browser-results.json'), JSON.stringify({ results, consoleErrors, pageErrors, bad }, null, 2));
  await browser.close();
  process.exit(results.every(r => r.ok) ? 0 : 1);
})().catch(e => { console.error(e); process.exit(2); });
