// QA v1 browser check of issue #39 ACs (pitching lines). Usage: NODE_PATH=<dir with playwright-core> node browser-check.js <baseUrl> <gameId> <outDir>
const { chromium } = require('playwright-core');
const fs = require('fs');
const [base, gameId, out] = process.argv.slice(2);
const results = [];
const check = (name, ok, detail = '') => { results.push({ name, ok, detail }); console.log(`${ok ? 'PASS' : 'FAIL'} ${name} ${detail}`); };
const STATS = ['ip', 'bf', 'bb', 'so', 'h', 'r', 'er'];
const IP_MSG = 'IP must be whole innings, or .1 or .2 for outs.';
const COUNT_MSG = 'Pitching stats must be whole numbers of 0 or more.';

(async () => {
  const browser = await chromium.launch({ executablePath: process.env.HOME + '/.cache/ms-playwright/chromium-1217/chrome-linux64/chrome' });
  const page = await browser.newPage({ viewport: { width: 1920, height: 1080 } });
  const consoleErrors = [], pageErrors = [], bad = [];
  page.on('console', m => m.type() === 'error' && consoleErrors.push(m.text()));
  page.on('pageerror', e => pageErrors.push(String(e)));
  page.on('response', r => r.status() >= 500 && bad.push(`${r.status()} ${r.url()}`));

  const open = async () => {
    await page.goto(`${base}/games/${gameId}`);
    await page.waitForSelector('#away-add-pitcher option:nth-child(3)', { state: 'attached', timeout: 15000 });
    await page.waitForFunction(() => window.Blazor !== undefined, null, { timeout: 15000 });
    await page.waitForTimeout(1500); // let the interactive circuit attach
  };
  const rows = side => page.locator(`#${side}-pitchers tbody tr`);
  const add = async (side, label, n) => {
    await page.locator(`#${side}-add-pitcher`).selectOption({ label });
    await rows(side).nth(n - 1).waitFor({ timeout: 10000 });
  };
  const rowId = async (side, i) => (await rows(side).nth(i).locator('select').getAttribute('id')).replace(/-decision$/, '');
  const box = (prefix, stat) => page.locator(`#${prefix}-${stat}`);
  const type = async (prefix, stat, value) => {
    const b = box(prefix, stat);
    await b.fill(value);
    await b.press('Tab'); // fires change, like a scorer moving to the next box
    await page.waitForTimeout(600);
  };
  const values = async prefix => Promise.all(STATS.map(s => box(prefix, s).inputValue()));
  const errText = async side => (await page.locator(`#${side}-pitchers-error`).count()) ? (await page.locator(`#${side}-pitchers-error`).textContent()).trim() : '';

  await open();
  for (const side of ['away', 'home']) {
    const heads = await page.$$eval(`#${side}-pitchers thead th`, t => t.map(x => x.textContent.trim()));
    check(`AC1 ${side} headers IP..ER after Decision`, JSON.stringify(heads) === JSON.stringify(['Pitcher', 'Decision', 'IP', 'BF', 'BB', 'SO', 'H', 'R', 'ER', '']), JSON.stringify(heads));
  }

  await add('away', '#28 Kyle Hendricks', 1);
  await add('away', '#18 Shota Imanaga', 2);
  await add('home', '#45 Garrett Crochet', 1);
  const a1 = await rowId('away', 0), a2 = await rowId('away', 1), h1 = await rowId('home', 0);
  const cellIds = await rows('away').nth(0).locator('td').evaluateAll(tds => tds.slice(1, 9).map(td => td.querySelector('select, input')?.id));
  check('AC1 row boxes sit after Decision in order', JSON.stringify(cellIds) === JSON.stringify(['decision', ...STATS].map(x => `${a1}-${x}`)), JSON.stringify(cellIds));
  for (const p of [a1, a2, h1]) check(`AC4 new pitcher ${p} starts with every box empty`, (await values(p)).every(v => v === ''), JSON.stringify(await values(p)));
  await page.screenshot({ path: `${out}/ac1-ac4-new-pitchers-empty-boxes-desktop.png`, fullPage: true });

  // AC2 + AC3 good values
  const line = ['6.2', '27', '3', '8', '5', '2', '1'];
  for (const [s, v] of STATS.map((s, i) => [s, line[i]])) await type(a1, s, v);
  await type(a2, 'ip', '2.1'); await type(a2, 'so', '0');
  await type(h1, 'ip', '6'); await type(h1, 'bf', '24');
  check('AC2/AC3 no error after good values', (await errText('away')) === '' && (await errText('home')) === '', `${await errText('away')}|${await errText('home')}`);
  await page.screenshot({ path: `${out}/ac2-ac3-lines-typed-desktop.png`, fullPage: true });

  // AC2 bad IP
  for (const badIp of ['6.3', '-1']) {
    await type(a2, 'ip', badIp);
    check(`AC2 IP "${badIp}" shows message`, (await errText('away')) === IP_MSG, await errText('away'));
  }
  await page.screenshot({ path: `${out}/ac2-bad-ip-message-desktop.png`, fullPage: true });
  // AC3 bad count
  for (const badN of ['-2', '1.5', 'x']) {
    await type(h1, 'bb', badN);
    check(`AC3 BB "${badN}" shows message`, (await errText('home')) === COUNT_MSG, await errText('home'));
  }
  await page.screenshot({ path: `${out}/ac3-bad-count-message-desktop.png`, fullPage: true });

  // AC4 clear a box
  await type(a1, 'er', '');
  check('AC4 cleared ER box shows empty', (await box(a1, 'er').inputValue()) === '', await box(a1, 'er').inputValue());

  // reload
  await open();
  check('AC2/AC3 away row 1 line after reload', JSON.stringify(await values(a1)) === JSON.stringify(['6.2', '27', '3', '8', '5', '2', '']), JSON.stringify(await values(a1)));
  check('AC2 bad IP not saved (2.1 kept) after reload', (await box(a2, 'ip').inputValue()) === '2.1', await box(a2, 'ip').inputValue());
  check('AC3 SO 0 saved after reload', (await box(a2, 'so').inputValue()) === '0', await box(a2, 'so').inputValue());
  check('AC3 bad BB not saved (empty) after reload', (await box(h1, 'bb').inputValue()) === '', await box(h1, 'bb').inputValue());
  check('AC2/AC3 home row after reload', JSON.stringify(await values(h1)) === JSON.stringify(['6', '24', '', '', '', '', '']), JSON.stringify(await values(h1)));
  check('AC4 untouched and cleared boxes empty after reload', (await values(a2)).filter((_, i) => ![0, 3].includes(i)).every(v => v === ''), JSON.stringify(await values(a2)));
  await page.screenshot({ path: `${out}/ac2-ac4-after-reload-desktop.png`, fullPage: true });

  // Gap witness (Medium, not an AC): a rejected box keeps its typed text after another box saves and clears the message.
  await type(h1, 'so', '-2');
  await type(h1, 'h', '4');
  const stale = await box(h1, 'so').inputValue();
  check('GAP-M1 (info) rejected SO still shows "-2" with no message after H saves', true, `so="${stale}" message="${await errText('home')}"`);
  await page.screenshot({ path: `${out}/gap-m1-stale-rejected-box-desktop.png`, fullPage: true });

  await open();
  for (const [name, w, h] of [['desktop', 1920, 1080], ['tablet', 1024, 768], ['mobile', 375, 667]]) {
    await page.setViewportSize({ width: w, height: h });
    await page.locator('#away-pitchers').scrollIntoViewIfNeeded();
    await page.screenshot({ path: `${out}/${name}.png` });
  }
  const pitW = await page.$eval('#away-pitchers table', t => t.getBoundingClientRect().right);
  check('(info) mobile pitchers table width at 375 px', true, `table right=${pitW}`);

  check('no console errors', consoleErrors.length === 0, JSON.stringify(consoleErrors));
  check('no page errors', pageErrors.length === 0, JSON.stringify(pageErrors));
  check('no 5xx', bad.length === 0, JSON.stringify(bad));
  fs.writeFileSync(`${out}/browser-results.json`, JSON.stringify(results, null, 2));
  await browser.close();
  process.exit(results.every(r => r.ok) ? 0 : 1);
})().catch(e => { console.error(e); process.exit(2); });
