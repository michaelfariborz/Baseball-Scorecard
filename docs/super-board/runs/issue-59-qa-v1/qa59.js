// Browser QA for issue #59: at-bat dialog. Usage: node qa59.js <baseUrl> <outDir>
const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');
const [base, out] = process.argv.slice(2);
const results = [];
const consoleErrors = [];
const pageErrors = [];
const bad = [];
function check(ac, name, ok, detail = '') {
  results.push({ ac, name, ok: !!ok, detail });
  console.log(`${ok ? 'PASS' : 'FAIL'} ${ac} ${name} ${detail}`);
}

async function ready(page) {
  await page.goto(`${base}/games/1`, { waitUntil: 'networkidle' });
  await page.waitForSelector('#away-slot-1-inning-1-note');
  // Wait for the interactive circuit: clicking must open the dialog.
  for (let i = 0; i < 40; i++) {
    await page.click('#away-slot-9-inning-9-note');
    if (await page.waitForSelector('#away-at-bat-dialog', { timeout: 1000 }).catch(() => null)) {
      await page.keyboard.press('Escape');
      await page.waitForSelector('#away-at-bat-dialog', { state: 'detached' });
      return;
    }
    await page.waitForTimeout(250);
  }
  throw new Error('circuit never became interactive');
}
const cell = (p, side, s, n) => p.locator(`#${side}-slot-${s}-inning-${n}-note`);
const checked = (p, side) => p.evaluate(s => {
  const r = document.querySelector(`#${s}-at-bat-dialog input[type=radio]:checked`);
  return r ? r.value : null;
}, side);
const focusedId = p => p.evaluate(() => document.activeElement && document.activeElement.id);

(async () => {
  fs.mkdirSync(out, { recursive: true });
  const browser = await chromium.launch();
  const ctx = await browser.newContext({ viewport: { width: 1920, height: 1080 } });
  const page = await ctx.newPage();
  page.on('console', m => { if (m.type() === 'error') consoleErrors.push(m.text()); });
  page.on('pageerror', e => pageErrors.push(String(e)));
  page.on('response', r => { if (r.status() >= 500 || r.status() === 401 || r.status() === 403) bad.push(`${r.status()} ${r.url()}`); });

  await ready(page);

  // AC1 both grids, no text input in batter rows
  for (const [side, name] of [['away', 'Away Cub3'], ['home', 'Home Sox3']]) {
    const inputs = await page.locator(`#${side}-batting-grid tr.batter input`).count();
    const tag = await cell(page, side, 3, 4).evaluate(e => e.tagName);
    await cell(page, side, 3, 4).click();
    const dlg = page.locator(`#${side}-at-bat-dialog`);
    await dlg.waitFor();
    const title = await page.locator(`#${side}-at-bat-title`).textContent();
    check('AC1', `${side} cell opens dialog naming batter + inning`, inputs === 0 && tag === 'BUTTON' && title.includes(name) && title.includes('inning 4'), `title="${title}" inputs=${inputs} tag=${tag}`);
    await page.screenshot({ path: path.join(out, `ac1-${side}-dialog-desktop.png`) });
    // AC2 list
    const codes = await page.locator(`#${side}-at-bat-dialog input[type=radio]`).evaluateAll(es => es.map(e => e.value));
    const want = ['1B', '2B', '3B', 'HR', 'BB', 'HBP', 'K', 'ꓘ', 'GO', 'FO', 'SF', 'FC', 'E'];
    if (side === 'away') check('AC2', 'dialog offers exactly the 13 results', JSON.stringify(codes) === JSON.stringify(want), codes.join(','));
    await page.click(`#${side}-at-bat-cancel`);
    await dlg.waitFor({ state: 'detached' });
  }

  // AC2 pick + save shows code (use backwards K)
  await cell(page, 'away', 3, 2).click();
  await page.locator('#away-at-bat-dialog label', { hasText: 'strikeout looking' }).click();
  await page.click('#away-at-bat-save');
  await page.waitForSelector('#away-at-bat-dialog', { state: 'detached' });
  await page.waitForFunction(() => document.querySelector('#away-slot-3-inning-2-note').textContent.trim() === 'ꓘ');
  check('AC2', 'saving ꓘ shows it in the cell', (await cell(page, 'away', 3, 2).textContent()).trim() === 'ꓘ');
  await cell(page, 'home', 1, 1).click();
  await page.locator('#home-at-bat-dialog label', { hasText: 'single' }).click();
  await page.click('#home-at-bat-save');
  await page.waitForSelector('#home-at-bat-dialog', { state: 'detached' });
  await page.waitForFunction(() => document.querySelector('#home-slot-1-inning-1-note').textContent.trim() === '1B');
  check('AC2', 'home grid saving 1B shows it in the cell', true);
  await page.screenshot({ path: path.join(out, 'ac2-saved-codes-desktop.png'), fullPage: true });

  // AC3 filled cell (seeded BB on home 5/1) opens selected, change to HR, then clear
  await cell(page, 'home', 5, 1).click();
  await page.waitForSelector('#home-at-bat-dialog');
  const pre = await checked(page, 'home');
  await page.screenshot({ path: path.join(out, 'ac3-filled-cell-selected-desktop.png') });
  await page.locator('#home-at-bat-dialog label', { hasText: 'home run' }).click();
  await page.click('#home-at-bat-save');
  await page.waitForFunction(() => document.querySelector('#home-slot-5-inning-1-note').textContent.trim() === 'HR');
  await cell(page, 'home', 5, 1).click();
  await page.waitForSelector('#home-at-bat-dialog');
  const pre2 = await checked(page, 'home');
  await page.click('#home-at-bat-clear');
  await page.waitForSelector('#home-at-bat-dialog', { state: 'detached' });
  await page.waitForFunction(() => document.querySelector('#home-slot-5-inning-1-note').textContent.trim() === '');
  check('AC3', 'BB opens selected, changed to HR, reopened HR selected, cleared', pre === 'BB' && pre2 === 'HR', `first=${pre} second=${pre2}`);

  // AC4 cancel / close X / backdrop leave value
  const before = (await cell(page, 'away', 3, 2).textContent()).trim();
  for (const how of ['cancel', 'close', 'backdrop']) {
    await cell(page, 'away', 3, 2).click();
    await page.waitForSelector('#away-at-bat-dialog');
    await page.locator('#away-at-bat-dialog label', { hasText: 'triple' }).click();
    if (how === 'cancel') await page.click('#away-at-bat-cancel');
    else if (how === 'close') await page.click('#away-at-bat-close');
    else await page.mouse.click(5, 5);
    await page.waitForSelector('#away-at-bat-dialog', { state: 'detached' });
    const after = (await cell(page, 'away', 3, 2).textContent()).trim();
    check('AC4', `${how} leaves saved result`, after === before, `${before} -> ${after}`);
  }

  // AC5 HITS/LOB input still saves, then reload
  const hits = page.locator('#away-batting-grid input').first();
  const hitsId = await hits.getAttribute('id');
  await hits.fill('2');
  await hits.press('Tab');
  await page.waitForTimeout(800);
  await page.reload({ waitUntil: 'networkidle' });
  await page.waitForSelector('#away-slot-3-inning-2-note');
  const afterReload = (await cell(page, 'away', 3, 2).textContent()).trim();
  const home11 = (await cell(page, 'home', 1, 1).textContent()).trim();
  const hitsVal = await page.locator(`#${hitsId}`).inputValue();
  check('AC5', 'after reload cells keep ꓘ and 1B, HITS input kept', afterReload === 'ꓘ' && home11 === '1B' && hitsVal === '2', `away=${afterReload} home=${home11} ${hitsId}=${hitsVal}`);
  await page.screenshot({ path: path.join(out, 'ac5-after-reload-desktop.png'), fullPage: true });
  await ready(page);

  // AC6 older free text 6-3
  const oldText = (await cell(page, 'away', 2, 3).textContent()).trim();
  await cell(page, 'away', 2, 3).click();
  await page.waitForSelector('#away-at-bat-dialog');
  const sel = await checked(page, 'away');
  const saveDisabled = await page.locator('#away-at-bat-save').isDisabled();
  await page.screenshot({ path: path.join(out, 'ac6-free-text-nothing-selected-desktop.png') });
  await page.locator('#away-at-bat-dialog label', { hasText: 'ground out' }).click();
  await page.click('#away-at-bat-save');
  await page.waitForFunction(() => document.querySelector('#away-slot-2-inning-3-note').textContent.trim() === 'GO');
  check('AC6', '6-3 shows, opens with nothing selected, saving GO replaces', oldText === '6-3' && sel === null && saveDisabled, `text=${oldText} selected=${sel} saveDisabled=${saveDisabled}`);

  // AC7 keyboard: focus cell, Enter opens, focus inside dialog, Escape closes, focus back
  await cell(page, 'home', 4, 6).focus();
  await page.keyboard.press('Enter');
  await page.waitForSelector('#home-at-bat-dialog');
  await page.waitForFunction(() => document.activeElement && document.activeElement.closest('#home-at-bat-dialog'));
  const inDialog = await focusedId(page);
  await page.keyboard.press('Tab');
  await page.keyboard.press('Escape');
  await page.waitForSelector('#home-at-bat-dialog', { state: 'detached' });
  await page.waitForFunction(() => document.activeElement && document.activeElement.id === 'home-slot-4-inning-6-note').catch(() => {});
  const back = await focusedId(page);
  const val = (await cell(page, 'home', 4, 6).textContent()).trim();
  check('AC7', 'Enter opens, focus in dialog, Esc closes without saving, focus back on cell', inDialog === 'home-at-bat-dialog' && back === 'home-slot-4-inning-6-note' && val === '', `inDialog=${inDialog} back=${back} cell="${val}"`);

  // Viewport shots of the open dialog
  for (const [name, w, h] of [['desktop', 1920, 1080], ['tablet', 1024, 768], ['mobile', 375, 667]]) {
    await page.setViewportSize({ width: w, height: h });
    await cell(page, 'away', 1, 1).click();
    await page.waitForSelector('#away-at-bat-dialog');
    await page.screenshot({ path: path.join(out, `${name}.png`) });
    await page.keyboard.press('Escape');
    await page.waitForSelector('#away-at-bat-dialog', { state: 'detached' });
  }

  check('green', 'no console errors / page errors / 5xx / 401 / 403', !consoleErrors.length && !pageErrors.length && !bad.length, JSON.stringify({ consoleErrors, pageErrors, bad }));
  fs.writeFileSync(path.join(out, 'browser-results.json'), JSON.stringify(results, null, 2));
  await browser.close();
  process.exit(results.every(r => r.ok) ? 0 : 1);
})().catch(e => { console.error(e); process.exit(2); });
