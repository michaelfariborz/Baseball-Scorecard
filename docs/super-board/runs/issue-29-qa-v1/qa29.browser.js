// QA #29: drive /games/1 in a real browser against the throwaway DB.
const { chromium } = require('playwright-core');
const out = process.argv[2];
const base = 'http://localhost:5299/games/1';
const log = [];
const ok = (name, pass, detail = '') => { log.push(`${pass ? 'PASS' : 'FAIL'} ${name}${detail ? ' :: ' + detail : ''}`); };

async function ready(page) {
  await page.goto(base);
  await page.waitForSelector('#away-batting-grid table');
  await page.waitForSelector('#home-batting-grid-empty');
  // wait for the interactive circuit before typing
  await page.waitForFunction(() => window.Blazor !== undefined);
  await page.waitForTimeout(1500);
}
async function type(page, id, text) {
  await page.fill(`#${id}`, text);
  await page.press(`#${id}`, 'Tab');
  await page.waitForTimeout(600);
}
const val = (page, id) => page.inputValue(`#${id}`);

(async () => {
  const browser = await chromium.launch({ executablePath: '/usr/bin/google-chrome', headless: true });
  const page = await browser.newPage({ viewport: { width: 1920, height: 1080 } });
  const errors = [];
  page.on('console', m => { if (m.type() === 'error') errors.push(m.text()); });
  await ready(page);

  // AC1
  const heads = await page.$$eval('#away-batting-grid thead th', ths => ths.map(t => t.textContent.trim()));
  ok('AC1 header Number,Name,Position,1..9', heads.join(',') === 'Number,Name,Position,1,2,3,4,5,6,7,8,9', heads.join(','));
  const rows = await page.$$eval('#away-batting-grid tbody tr.batter', trs => trs.map(tr => [...tr.querySelectorAll('td')].slice(0, 3).map(td => td.textContent.trim()).join('|')));
  ok('AC1 9 rows in batting order', rows.length === 9 && rows[0] === '8|Ian Happ|LF' && rows[8].startsWith('34|Carson Kelly'), rows.join(' ; '));
  // AC4
  const empty = (await page.textContent('#home-batting-grid-empty')).trim();
  ok('AC4 home message', empty === 'Save a lineup to start scoring.', empty);
  ok('AC4 home has no table', (await page.$$('#home-batting-grid table')).length === 0);

  // AC2
  await type(page, 'away-slot-1-inning-1-note', '6-3');
  await type(page, 'away-slot-2-inning-1-note', 'K');
  await type(page, 'away-slot-3-inning-1-note', '1B');
  await type(page, 'away-slot-4-inning-1-note', 'BB');
  await type(page, 'away-slot-3-inning-2-note', 'ABCDEFGHIJKLMNOPQRST'); // exactly 20
  ok('AC2 maxlength=20', (await page.getAttribute('#away-slot-1-inning-1-note', 'maxlength')) === '20');
  // AC3
  await type(page, 'away-inning-1-hits', '1');
  await type(page, 'away-inning-1-lob', '2');
  await type(page, 'away-inning-2-hits', '0');
  await type(page, 'away-inning-2-lob', '0');
  const hitsCols = await page.$$eval('#away-batting-grid tr.totals input[id$="-hits"]', x => x.length);
  const lobCols = await page.$$eval('#away-batting-grid tr.totals input[id$="-lob"]', x => x.length);
  ok('AC3 HITS + LOB box per inning', hitsCols === 9 && lobCols === 9, `${hitsCols}/${lobCols}`);

  await ready(page); // reload
  ok('AC2 6-3 survives reload', (await val(page, 'away-slot-1-inning-1-note')) === '6-3');
  ok('AC2 K survives reload', (await val(page, 'away-slot-2-inning-1-note')) === 'K');
  ok('AC2 20-char note survives reload', (await val(page, 'away-slot-3-inning-2-note')) === 'ABCDEFGHIJKLMNOPQRST');
  ok('AC3 hits/lob survive reload', (await val(page, 'away-inning-1-hits')) === '1' && (await val(page, 'away-inning-1-lob')) === '2' && (await val(page, 'away-inning-2-hits')) === '0', 'inning1 1/2 inning2 0/0');
  await page.screenshot({ path: `${out}/desktop.png`, fullPage: true });

  // AC2 clear
  await type(page, 'away-slot-3-inning-2-note', '');
  await ready(page);
  ok('AC2 cleared note gone after reload', (await val(page, 'away-slot-3-inning-2-note')) === '');

  // AC3 bad entries
  for (const bad of ['-1', 'two', '1.5']) {
    await type(page, 'away-inning-1-hits', bad);
    const msg = (await page.textContent('#away-batting-grid-error').catch(() => '')).trim();
    ok(`AC3 bad HITS "${bad}" shows message`, msg === 'HITS and LOB must be whole numbers of 0 or more.', msg);
  }
  await page.locator('#away-batting-grid').screenshot({ path: `${out}/ac3-bad-hits-message.png` });
  await ready(page);
  ok('AC3 bad entry not saved (HITS still 1)', (await val(page, 'away-inning-1-hits')) === '1');

  // Edge: stale error after a later good save elsewhere
  await type(page, 'away-inning-3-lob', 'x');
  await type(page, 'away-slot-5-inning-3-note', 'F8');
  const stillTyped = await val(page, 'away-inning-3-lob');
  const errShown = (await page.$$('#away-batting-grid-error')).length > 0;
  log.push(`EDGE bad LOB "x" then a good note save: LOB box shows "${stillTyped}", message shown=${errShown}`);

  // Viewports
  await ready(page);
  await page.setViewportSize({ width: 1024, height: 768 });
  await page.screenshot({ path: `${out}/tablet.png`, fullPage: true });
  await page.setViewportSize({ width: 375, height: 667 });
  await page.screenshot({ path: `${out}/mobile.png`, fullPage: true });
  const overflow = await page.evaluate(() => document.documentElement.scrollWidth);
  log.push(`INFO mobile scrollWidth=${overflow} (viewport 375)`);

  log.push(`INFO console errors: ${errors.length ? errors.join(' | ') : 'none'}`);
  await browser.close();
  console.log(log.join('\n'));
})().catch(e => { console.log(log.join('\n')); console.error(e); process.exit(1); });
