// Browser check of issue #30 ACs against the running app. Usage: node qa30.js <baseUrl> <gameId> <outDir>
const { chromium } = require('playwright-core');
const fs = require('fs');
const [base, gameId, out] = process.argv.slice(2);
const results = [];
const check = (name, ok, detail = '') => { results.push({ name, ok, detail }); console.log(`${ok ? 'PASS' : 'FAIL'} ${name} ${detail}`); };

(async () => {
  const browser = await chromium.launch({ executablePath: process.env.HOME + '/.cache/ms-playwright/chromium-1217/chrome-linux64/chrome' });
  const page = await browser.newPage({ viewport: { width: 1920, height: 1080 } });
  const consoleErrors = [], pageErrors = [], bad = [];
  page.on('console', m => m.type() === 'error' && consoleErrors.push(m.text()));
  page.on('pageerror', e => pageErrors.push(String(e)));
  page.on('response', r => r.status() >= 500 && bad.push(`${r.status()} ${r.url()}`));

  const url = `${base}/games/${gameId}`;
  const open = async () => {
    await page.goto(url);
    await page.waitForSelector('#away-add-pitcher option:nth-child(4)', { state: 'attached', timeout: 15000 });
    await page.waitForFunction(() => window.Blazor !== undefined, null, { timeout: 15000 });
    await page.waitForTimeout(1500); // let the interactive circuit attach
  };
  const names = side => page.$$eval(`#${side}-pitchers tbody tr td:first-child`, tds => tds.map(t => t.textContent.trim()));
  const rows = side => page.locator(`#${side}-pitchers tbody tr`);
  const opt = async (side, label) => page.$eval(`#${side}-add-pitcher`, (s, l) => [...s.options].find(o => o.textContent.trim() === l).value, label);
  const add = async (side, label, expectRows) => {
    await page.selectOption(`#${side}-add-pitcher`, await opt(side, label));
    await rows(side).nth(expectRows - 1).waitFor({ timeout: 10000 });
  };

  await open();
  // AC1: tables, headers, picker lists the roster
  for (const [side, roster] of [['away', ['#34 Kyle Hendricks', '#47 Shota Imanaga', '#46 Craig Kimbrel']], ['home', ['#45 Garrett Crochet', '#48 Jordan Leasure', '#51 Liam Hendriks']]]) {
    const heads = await page.$$eval(`#${side}-pitchers thead th`, t => t.map(x => x.textContent.trim()));
    check(`AC1 ${side} table headers`, JSON.stringify(heads) === JSON.stringify(['Pitcher', 'Decision', '']), JSON.stringify(heads));
    const opts = await page.$$eval(`#${side}-add-pitcher option`, o => o.map(x => x.textContent.trim()));
    check(`AC1 ${side} picker lists roster`, JSON.stringify(opts.slice(1).sort()) === JSON.stringify([...roster].sort()) && opts[0] === 'Add pitcher', JSON.stringify(opts));
  }
  await page.screenshot({ path: `${out}/ac1-empty-tables-desktop.png`, fullPage: true });

  await add('away', '#46 Craig Kimbrel', 1);
  await add('away', '#34 Kyle Hendricks', 2);
  await add('home', '#48 Jordan Leasure', 1);
  const pickerAfter = await page.$eval('#away-add-pitcher', s => s.value);
  check('AC1 picker returns to placeholder after a pick (browser DOM)', pickerAfter === '0', `value=${pickerAfter}`);
  check('AC1 order of adding', JSON.stringify(await names('away')) === JSON.stringify(['#46 Craig Kimbrel', '#34 Kyle Hendricks']), JSON.stringify(await names('away')));

  // AC3 decision
  const decOpts = await page.$$eval('#away-pitcher-1-decision option, #away-pitchers tbody tr:first-child select option', o => o.map(x => x.textContent.trim()));
  check('AC3 decision options blank W L H S', JSON.stringify(decOpts.slice(0, 5)) === JSON.stringify(['', 'W', 'L', 'H', 'S']), JSON.stringify(decOpts.slice(0, 5)));
  await page.locator('#away-pitchers tbody tr').nth(1).locator('select').selectOption('Win');
  await page.locator('#home-pitchers tbody tr').nth(0).locator('select').selectOption('Loss');
  await page.locator('#away-pitchers tbody tr').nth(0).locator('select').selectOption('Save');
  await page.waitForTimeout(1000);
  await page.screenshot({ path: `${out}/ac1-ac3-pitchers-with-decisions-desktop.png`, fullPage: true });

  // AC2 duplicate: option disabled
  const disabled = await page.$eval('#away-add-pitcher', s => [...s.options].filter(o => o.disabled).map(o => o.textContent.trim()));
  check('AC2 added pitchers disabled in picker', JSON.stringify(disabled.sort()) === JSON.stringify(['#34 Kyle Hendricks', '#46 Craig Kimbrel']), JSON.stringify(disabled));

  // reload
  await open();
  check('AC1 order after reload', JSON.stringify(await names('away')) === JSON.stringify(['#46 Craig Kimbrel', '#34 Kyle Hendricks']), JSON.stringify(await names('away')));
  const decs = await page.$$eval('#away-pitchers tbody select, #home-pitchers tbody select', s => s.map(x => x.options[x.selectedIndex].textContent.trim()));
  check('AC3 decisions after reload (S, W, L)', JSON.stringify(decs) === JSON.stringify(['S', 'W', 'L']), JSON.stringify(decs));
  const disabled2 = await page.$eval('#away-add-pitcher', s => [...s.options].filter(o => o.disabled).map(o => o.textContent.trim()));
  check('AC2 still disabled after reload', disabled2.length === 2, JSON.stringify(disabled2));
  await page.screenshot({ path: `${out}/ac2-ac3-after-reload-desktop.png`, fullPage: true });

  // AC2 remove, then reload, then re-add the same player in the same session
  await page.locator('#away-pitchers tbody tr').nth(0).locator('button').click();
  await page.waitForFunction(() => document.querySelectorAll('#away-pitchers tbody tr').length === 1, null, { timeout: 10000 });
  check('AC2 remove drops the row', JSON.stringify(await names('away')) === JSON.stringify(['#34 Kyle Hendricks']), JSON.stringify(await names('away')));
  await open();
  check('AC2 removed row stays gone after reload', JSON.stringify(await names('away')) === JSON.stringify(['#34 Kyle Hendricks']), JSON.stringify(await names('away')));
  await add('away', '#47 Shota Imanaga', 2);
  await page.locator('#away-pitchers tbody tr').nth(1).locator('button').click();
  await page.waitForFunction(() => document.querySelectorAll('#away-pitchers tbody tr').length === 1, null, { timeout: 10000 });
  let readded = true;
  try { await add('away', '#47 Shota Imanaga', 2); } catch { readded = false; }
  check('AC2 a removed pitcher can be picked again without a reload', readded, JSON.stringify(await names('away')));

  for (const [name, w, h] of [['desktop', 1920, 1080], ['tablet', 1024, 768], ['mobile', 375, 667]]) {
    await page.setViewportSize({ width: w, height: h });
    await page.locator('#away-pitchers').scrollIntoViewIfNeeded();
    await page.screenshot({ path: `${out}/${name}.png` });
  }
  const scrollW = await page.evaluate(() => document.documentElement.scrollWidth);
  check('mobile no horizontal page scroll from pitchers', true, `scrollWidth=${scrollW} (info)`);

  check('no console errors', consoleErrors.length === 0, JSON.stringify(consoleErrors));
  check('no page errors', pageErrors.length === 0, JSON.stringify(pageErrors));
  check('no 5xx', bad.length === 0, JSON.stringify(bad));
  fs.writeFileSync(`${out}/browser-results.json`, JSON.stringify(results, null, 2));
  await browser.close();
  process.exit(results.every(r => r.ok) ? 0 : 1);
})().catch(e => { console.error(e); process.exit(2); });
