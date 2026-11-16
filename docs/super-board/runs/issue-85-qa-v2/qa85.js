// Browser QA for issue #85 (v2): extra inning column when a team bats around. Usage: node qa85.js <baseUrl> <outDir>
// Seed (seed.sql): game 1 holds at bats saved before AddInningColumns; game 2 has away slot 1 inning 3 = 2B.
const { chromium } = require('playwright-core');
const fs = require('fs');
const path = require('path');
const [base, out] = process.argv.slice(2);
const results = [], consoleErrors = [], pageErrors = [], bad = [];
function check(ac, name, ok, detail = '') {
  results.push({ ac, name, ok: !!ok, detail });
  console.log(`${ok ? 'PASS' : 'FAIL'} ${ac} ${name} ${detail}`);
}
const cell = (side, s, n, c = 1) => c === 1 ? `#${side}-slot-${s}-inning-${n}-note` : `#${side}-slot-${s}-inning-${n}-col-${c}-note`;
async function ready(page, game) {
  await page.goto(`${base}/games/${game}`, { waitUntil: 'networkidle' });
  await page.waitForSelector(cell('away', 1, 1));
  for (let i = 0; i < 40; i++) {
    await page.click(cell('away', 9, 9));
    if (await page.waitForSelector('#away-at-bat-dialog', { timeout: 1000 }).catch(() => null)) {
      await page.keyboard.press('Escape');
      await page.waitForSelector('#away-at-bat-dialog', { state: 'detached' });
      return;
    }
    await page.waitForTimeout(250);
  }
  throw new Error('circuit never became interactive');
}
const headers = (page, side) => page.$$eval(`#${side}-batting-grid thead th`, ths => ths.map(t => t.textContent.trim()).join(','));
const text = (page, sel) => page.$eval(`${sel} .at-bat-note`, e => e.textContent.trim()).catch(() => null);
const has = async (page, sel) => (await page.locator(sel).count()) > 0;
const gridError = page => page.$eval('#away-batting-grid-error', e => e.textContent.trim()).catch(() => null);
async function setInning(page, side, n) {
  await page.fill(`#${side}-extra-inning`, String(n));
  await page.dispatchEvent(`#${side}-extra-inning`, 'change');
}
async function addCol(page, side, n, waitFor) {
  await setInning(page, side, n); await page.click(`#${side}-extra-add`);
  if (waitFor) await page.waitForSelector(waitFor, { timeout: 5000 }).catch(() => null);
}
async function removeCol(page, side, n) { await setInning(page, side, n); await page.click(`#${side}-extra-remove`); await page.waitForTimeout(400); }
async function open(page, sel, side = 'away') { await page.click(sel); await page.waitForSelector(`#${side}-at-bat-dialog`); }
async function closed(page, side = 'away') { await page.waitForSelector(`#${side}-at-bat-dialog`, { state: 'detached' }); }
async function pick(page, code, side = 'away') { await page.locator(`#${side}-at-bat-dialog .at-bat-results input[value="${code}"]`).check(); }
async function waitNote(page, sel, t) {
  await page.waitForFunction(([s, x]) => (document.querySelector(`${s} .at-bat-note`)?.textContent.trim() ?? null) === x, [sel, t], { timeout: 5000 }).catch(() => null);
}
const PLAIN = '#,Name,Pos,Inn,1,2,3,4,5,6,7,8,9';

(async () => {
  fs.mkdirSync(out, { recursive: true });
  const browser = await chromium.launch({ executablePath: '/usr/bin/google-chrome' });
  const ctx = await browser.newContext({ viewport: { width: 1920, height: 1080 } });
  const page = await ctx.newPage();
  page.on('console', m => { if (m.type() === 'error') consoleErrors.push(m.text()); });
  page.on('pageerror', e => pageErrors.push(String(e)));
  page.on('response', r => { if (r.status() >= 500) bad.push(`${r.status()} ${r.url()}`); });

  // AC5 (old games): game 1 at bats saved before the migration show as before
  await ready(page, 1);
  const old = [];
  for (const [s, b] of [['away', 1], ['away', 2], ['away', 3], ['home', 1], ['home', 2]]) {
    const sel = cell(s, b, 1);
    old.push(`${await text(page, sel)}/${await page.locator(`${sel} svg.bases-diamond line`).count()}`);
  }
  const wantOld = 'K/0|GO 4-6-3 DP/0|1B/3|BB/1|HR/4';
  check('AC5', 'pre-migration at bats show unchanged in their cells', old.join('|') === wantOld, old.join('|'));
  check('AC5', 'pre-migration game shows one column per inning on both grids',
    (await headers(page, 'away')) === PLAIN && (await headers(page, 'home')) === PLAIN, await headers(page, 'away'));
  await page.screenshot({ path: path.join(out, 'ac5-old-game-desktop.png'), fullPage: true });
  // edit an old at bat: K -> BB reached 1st, then reload
  await open(page, cell('away', 1, 1)); await pick(page, 'BB');
  await page.locator('#away-at-bat-bases input[value="1"]').check();
  await page.click('#away-at-bat-save'); await closed(page);
  await ready(page, 1);
  check('AC5', 'old at bat edits in place (K to BB) and survives a reload', (await text(page, cell('away', 1, 1))) === 'BB', await text(page, cell('away', 1, 1)));

  // AC1: add an extra column to inning 3 of the away grid in game 2
  await ready(page, 2);
  await addCol(page, 'away', 3, cell('away', 1, 3, 2));
  const h1 = await headers(page, 'away');
  check('AC1', 'away headers gain a second "3" right after inning 3', h1 === '#,Name,Pos,Inn,1,2,3,3,4,5,6,7,8,9', h1);
  check('AC1', 'every slot gets an inning-3 column-2 cell', await has(page, cell('away', 9, 3, 2)));
  await page.locator('#away-batting-grid').screenshot({ path: path.join(out, 'ac1-extra-column-added-desktop.png') });
  await addCol(page, 'away', 7, cell('away', 1, 7, 2));
  check('AC1', 'any inning works: inning 7 also gains a column', (await headers(page, 'away')) === '#,Name,Pos,Inn,1,2,3,3,4,5,6,7,7,8,9', await headers(page, 'away'));
  await removeCol(page, 'away', 7);

  // AC3: other team's grid unchanged
  check('AC3', 'home grid of the same game keeps one column per inning', (await headers(page, 'home')) === PLAIN, await headers(page, 'home'));

  // AC2: score two second at bats in the extra column
  await open(page, cell('away', 1, 3, 2));
  const title = await page.$eval('#away-at-bat-title', e => e.textContent.trim());
  const ticked = await page.locator('#away-at-bat-dialog .at-bat-results input:checked').count();
  check('AC2', 'extra-column dialog opens empty and names inning 3, column 2', ticked === 0 && /inning 3, column 2/.test(title), `title="${title}" ticked=${ticked}`);
  await pick(page, 'GO'); await page.fill('#away-at-bat-fielders', '63');
  await page.screenshot({ path: path.join(out, 'ac2-dialog-column-2-desktop.png') });
  await page.click('#away-at-bat-save'); await closed(page);
  await waitNote(page, cell('away', 1, 3, 2), 'GO 6-3');
  await open(page, cell('away', 2, 3, 2)); await pick(page, '1B');
  await page.locator('#away-at-bat-bases input[value="1"]').check();
  await page.click('#away-at-bat-save'); await closed(page);
  await waitNote(page, cell('away', 2, 3, 2), '1B');
  const before = [await text(page, cell('away', 1, 3)), await text(page, cell('away', 1, 3, 2)), await text(page, cell('away', 2, 3, 2))].join('|');
  check('AC2', 'slot 1 shows 2B in column 1 and GO 6-3 in column 2; slot 2 shows 1B in column 2', before === '2B|GO 6-3|1B', before);

  // AC5: HITS / LOB span the inning's columns, one box each
  const span3 = await page.$eval('#away-inning-3-hits', e => e.parentElement.getAttribute('colspan'));
  const span4 = await page.$eval('#away-inning-4-hits', e => e.parentElement.getAttribute('colspan'));
  const lob3 = await page.$eval('#away-inning-3-lob', e => e.parentElement.getAttribute('colspan'));
  const hitBoxes = await page.locator('#away-batting-grid input[id$="-hits"]').count();
  check('AC5', 'HITS and LOB for inning 3 are one box spanning 2 columns; inning 4 spans 1; 9 HITS boxes', span3 === '2' && lob3 === '2' && span4 === '1' && hitBoxes === 9, `hits3=${span3} lob3=${lob3} hits4=${span4} boxes=${hitBoxes}`);
  await page.fill('#away-inning-3-hits', '4'); await page.dispatchEvent('#away-inning-3-hits', 'change');
  await page.fill('#away-inning-3-lob', '2'); await page.dispatchEvent('#away-inning-3-lob', 'change');
  await page.waitForTimeout(600);

  // AC4: an empty extra column can be removed; one holding at bats cannot
  await addCol(page, 'away', 5, cell('away', 1, 5, 2));
  const added5 = await has(page, cell('away', 1, 5, 2));
  await removeCol(page, 'away', 5);
  check('AC4', 'empty extra column in inning 5 is removed', added5 && !(await has(page, cell('away', 1, 5, 2))), `added=${added5}`);
  await removeCol(page, 'away', 3);
  const err = await gridError(page);
  check('AC4', 'removing inning 3 column 2 (holds at bats) is refused with a message, column stays',
    err === 'That column holds at-bats. Clear them before removing it.' && await has(page, cell('away', 1, 3, 2)), `msg="${err}"`);
  await page.locator('#away-batting-grid').screenshot({ path: path.join(out, 'ac4-remove-refused-desktop.png') });
  await addCol(page, 'away', 5, cell('away', 1, 5, 2));

  // reload: saved at bats stay, empty extra column goes
  await ready(page, 2);
  const after = [await text(page, cell('away', 1, 3)), await text(page, cell('away', 1, 3, 2)), await text(page, cell('away', 2, 3, 2))].join('|');
  check('AC2', 'after a reload both inning-3 at bats show in their own columns', after === '2B|GO 6-3|1B', after);
  check('AC4', 'after a reload the column holding at bats still shows and the empty inning-5 one is gone',
    (await headers(page, 'away')) === '#,Name,Pos,Inn,1,2,3,3,4,5,6,7,8,9' && !(await has(page, cell('away', 1, 5, 2))), await headers(page, 'away'));
  const hl = await page.$eval('#away-inning-3-hits', e => e.value) + '/' + await page.$eval('#away-inning-3-lob', e => e.value);
  check('AC5', 'inning 3 HITS 4 / LOB 2 kept after reload as one value each', hl === '4/2', hl);
  check('AC3', 'home grid still one column per inning after reload', (await headers(page, 'home')) === PLAIN, await headers(page, 'home'));
  await page.screenshot({ path: path.join(out, 'desktop.png'), fullPage: true });
  await page.locator('#away-batting-grid').screenshot({ path: path.join(out, 'ac2-after-reload-desktop.png') });

  // AC3: another game is untouched
  await ready(page, 1);
  check('AC3', 'game 1 away and home grids keep one column per inning', (await headers(page, 'away')) === PLAIN && (await headers(page, 'home')) === PLAIN, await headers(page, 'away'));
  // AC4 (v1 failure): an empty column 2 before a scored column 3, removed during the visit
  await ready(page, 2);
  await addCol(page, 'away', 6, cell('away', 1, 6, 2));
  await addCol(page, 'away', 6, cell('away', 1, 6, 3));
  await open(page, cell('away', 4, 6, 3)); await pick(page, 'K'); await page.click('#away-at-bat-save'); await closed(page);
  await removeCol(page, 'away', 6);
  await page.waitForTimeout(500);
  const afterRemove = await headers(page, 'away');
  check('AC4', 'empty column 2 before a scored column 3 is removed, no error, K now in the second inning-6 column',
    afterRemove.includes(',6,6,7,') && !afterRemove.includes(',6,6,6,') && !(await gridError(page)) && (await text(page, cell('away', 4, 6, 2))) === 'K' && !(await has(page, cell('away', 1, 6, 3))),
    `headers=${afterRemove} err=${await gridError(page)} k=${await text(page, cell('away', 4, 6, 2))}`);
  await page.locator('#away-batting-grid').screenshot({ path: path.join(out, 'ac4-empty-middle-removed-desktop.png') });
  await ready(page, 2);
  check('AC4', 'after a reload inning 6 shows two columns with K kept', (await headers(page, 'away')).includes(',6,6,7,') && (await text(page, cell('away', 4, 6, 2))) === 'K', await headers(page, 'away'));

  // AC4 (v1 failure): an empty column 2 before a scored column 3, left in place and reloaded
  await addCol(page, 'away', 8, cell('away', 1, 8, 2));
  await addCol(page, 'away', 8, cell('away', 1, 8, 3));
  await open(page, cell('away', 2, 8, 3)); await pick(page, 'BB'); await page.click('#away-at-bat-save'); await closed(page);
  await ready(page, 2);
  const reloaded = await headers(page, 'away');
  check('AC4', 'empty column 2 before a scored column 3 is gone after a reload (inning 8 shows two columns)',
    reloaded.includes(',8,8,9') && !reloaded.includes(',8,8,8') && (await text(page, cell('away', 2, 8, 2))) === 'BB', `headers=${reloaded}`);
  await page.locator('#away-batting-grid').screenshot({ path: path.join(out, 'ac4-empty-middle-gone-after-reload-desktop.png') });
  await removeCol(page, 'away', 8);
  check('AC4', 'the remaining inning-8 extra column holds an at bat, so remove is refused', (await gridError(page)) === 'That column holds at-bats. Clear them before removing it.' && (await has(page, cell('away', 2, 8, 2))), `msg="${await gridError(page)}"`);
  const db = { k: await text(page, cell('away', 4, 6, 2)), bb: await text(page, cell('away', 2, 8, 2)) };
  check('AC2', 'both packed at bats still open their own saved data', db.k === 'K' && db.bb === 'BB', JSON.stringify(db));

  // viewports
  for (const [name, w, h] of [['tablet', 1024, 768], ['mobile', 375, 667]]) {
    await page.setViewportSize({ width: w, height: h });
    await ready(page, 2);
    await page.locator('#away-batting-grid').scrollIntoViewIfNeeded();
    await page.screenshot({ path: path.join(out, `${name}.png`) });
  }

  check('ALL', 'no page errors, no console errors, no 5xx', !pageErrors.length && !consoleErrors.length && !bad.length,
    JSON.stringify({ pageErrors, consoleErrors, bad }));
  fs.writeFileSync(path.join(out, 'browser-results.json'), JSON.stringify(results, null, 2));
  await browser.close();
  process.exit(results.some(r => !r.ok) ? 1 : 0);
})().catch(e => { console.error(e); process.exit(2); });
