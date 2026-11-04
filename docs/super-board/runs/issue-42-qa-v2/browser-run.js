// QA v2 browser run for issue #42. Usage: node qa42.js <baseUrl> <outDir>
const { chromium } = require('playwright-core');
const fs = require('fs');
const path = require('path');

const [base, out] = process.argv.slice(2);
fs.mkdirSync(out, { recursive: true });
const exe = path.join(process.env.HOME, '.cache/ms-playwright/chromium-1217/chrome-linux64/chrome');
const POS = ['CenterField', 'Shortstop', 'LeftField', 'FirstBase', 'RightField', 'ThirdBase', 'Catcher', 'SecondBase', 'Pitcher'];
const VIEWPORTS = { desktop: [1920, 1080], tablet: [1024, 768], mobile: [375, 667] };
const results = [];
const consoleErrors = [];

function check(ac, name, ok, detail = '') {
  results.push({ ac, name, ok, detail });
  console.log(`${ok ? 'PASS' : 'FAIL'} ${ac} ${name} ${detail}`);
}

async function open(page, id) {
  await page.goto(`${base}/games/${id}`, { waitUntil: 'networkidle' });
  // Wait for the Blazor circuit so clicks and changes reach the server.
  await page.waitForFunction(() => window.Blazor !== undefined);
  await page.waitForTimeout(1200);
}

async function count(page, sel) { return page.locator(sel).count(); }

async function shots(browser, id, prefix, setup) {
  for (const [vp, [w, h]] of Object.entries(VIEWPORTS)) {
    const ctx = await browser.newContext({ viewport: { width: w, height: h } });
    const p = await ctx.newPage();
    await open(p, id);
    if (setup) await setup(p);
    await p.screenshot({ path: path.join(out, `${prefix}-${vp}.png`) });
    await ctx.close();
  }
}

async function fill(page, side) {
  // Values 1..9 for the side's roster: read option values from the slot-1 select.
  const opts = await page.locator(`#${side}-slot-1-player option`).evaluateAll(os => os.map(o => o.value).filter(v => v && v !== '0'));
  for (let i = 0; i < 9; i++) {
    await page.selectOption(`#${side}-slot-${i + 1}-player`, opts[i]);
    await page.selectOption(`#${side}-slot-${i + 1}-position`, POS[i]);
  }
  return opts.slice(0, 9);
}

(async () => {
  const browser = await chromium.launch({ executablePath: exe });
  const ctx = await browser.newContext({ viewport: { width: 1920, height: 1080 } });
  const page = await ctx.newPage();
  page.on('console', m => { if (m.type() === 'error') consoleErrors.push(m.text()); });
  page.on('pageerror', e => consoleErrors.push(String(e)));

  // AC1: game 1 has no saved lineups.
  await open(page, 1);
  check('AC1', 'both pickers render', (await count(page, '#lineup-away')) === 1 && (await count(page, '#lineup-home')) === 1);
  check('AC1', 'no Edit lineup button', (await count(page, '#away-edit-lineup, #home-edit-lineup')) === 0);
  await shots(browser, 1, 'ac1-no-lineups');

  // AC2 + AC5: save the away lineup on game 1.
  await fill(page, 'away');
  await page.click('#away-save-lineup');
  await page.waitForSelector('#away-edit-lineup', { timeout: 10000 }).catch(() => {});
  check('AC2', 'away picker gone', (await count(page, '#lineup-away')) === 0);
  check('AC2', 'Edit lineup shows for away', ((await page.locator('#away-edit-lineup').textContent()) || '').trim() === 'Edit lineup');
  check('AC2', 'home picker still renders', (await count(page, '#lineup-home')) === 1 && (await count(page, '#home-edit-lineup')) === 0);
  const rows = await count(page, '#away-batting-grid tbody tr.batter');
  check('AC5', 'away batting grid shows 9 saved rows while picker hidden', rows === 9, `rows=${rows}`);
  await page.screenshot({ path: path.join(out, 'ac2-away-saved-desktop.png') });
  await page.locator('#away-batting-grid').screenshot({ path: path.join(out, 'ac5-away-grid-desktop.png') });
  await shots(browser, 1, 'ac2-away-saved');

  // AC3: save both lineups on game 2, then open it fresh.
  await open(page, 2);
  const awayPicks = await fill(page, 'away');
  await page.click('#away-save-lineup');
  await page.waitForSelector('#away-edit-lineup', { timeout: 10000 });
  await fill(page, 'home');
  await page.click('#home-save-lineup');
  await page.waitForSelector('#home-edit-lineup', { timeout: 10000 });
  await open(page, 2);
  check('AC3', 'no pickers on a game with both saved', (await count(page, '#lineup-away, #lineup-home')) === 0);
  check('AC3', 'two Edit lineup buttons', (await count(page, '#away-edit-lineup')) === 1 && (await count(page, '#home-edit-lineup')) === 1);
  await shots(browser, 2, 'ac3-both-saved');

  // AC4: Edit lineup brings the picker back with the saved values; saving hides it again.
  await page.click('#away-edit-lineup');
  await page.waitForSelector('#lineup-away', { timeout: 10000 });
  let same = true;
  for (let i = 0; i < 9; i++) {
    const pv = await page.inputValue(`#away-slot-${i + 1}-player`);
    const qv = await page.inputValue(`#away-slot-${i + 1}-position`);
    if (pv !== awayPicks[i] || qv !== POS[i]) { same = false; console.log(`slot ${i + 1}: ${pv}/${qv}`); }
  }
  check('AC4', 'picker shows saved players and positions', same);
  check('AC4', 'home stays hidden while away edits', (await count(page, '#lineup-home')) === 0);
  await page.screenshot({ path: path.join(out, 'ac4-edit-open-desktop.png') });
  await shots(browser, 2, 'ac4-edit-open', async p => { await p.click('#away-edit-lineup'); await p.waitForSelector('#lineup-away'); });
  await page.click('#away-save-lineup');
  await page.waitForSelector('#away-edit-lineup', { timeout: 10000 }).catch(() => {});
  check('AC4', 'saving hides the picker again', (await count(page, '#lineup-away')) === 0 && (await count(page, '#away-edit-lineup')) === 1);
  check('AC5', 'grid rows still shown after re-save', (await count(page, '#away-batting-grid tbody tr.batter')) === 9);
  await page.screenshot({ path: path.join(out, 'ac4-after-resave-desktop.png') });

  check('ALL', 'no console or page errors', consoleErrors.length === 0, consoleErrors.join(' | '));
  fs.writeFileSync(path.join(out, 'browser-results.json'), JSON.stringify({ results, consoleErrors }, null, 2));
  await browser.close();
  process.exit(results.every(r => r.ok) ? 0 : 1);
})().catch(e => { console.error(e); process.exit(2); });
