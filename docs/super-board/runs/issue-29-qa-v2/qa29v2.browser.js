// QA #29 v2: drive /games/1 in a real browser against a throwaway DB (Cubs lineup saved, Sox not).
const { chromium } = require('playwright-core');
const out = process.argv[2];
const base = 'http://localhost:5299/games/1';
const log = [];
const ok = (name, pass, detail = '') => log.push(`${pass ? 'PASS' : 'FAIL'} ${name}${detail ? ' :: ' + detail : ''}`);
async function ready(page) {
  await page.goto(base);
  await page.waitForSelector('#away-batting-grid table');
  await page.waitForSelector('#home-batting-grid-empty');
  await page.waitForFunction(() => window.Blazor !== undefined);
  await page.waitForTimeout(1500);
}
async function type(page, id, text) {
  await page.fill(`#${id}`, text);
  await page.press(`#${id}`, 'Tab');
  await page.waitForTimeout(700);
}
const val = (page, id) => page.inputValue(`#${id}`);
const errText = async page => ((await page.$('#away-batting-grid-error')) ? (await page.textContent('#away-batting-grid-error')).trim() : '');
const MSG = 'HITS and LOB must be whole numbers of 0 or more.';
(async () => {
  const browser = await chromium.launch({ executablePath: '/usr/bin/google-chrome', headless: true });
  const page = await browser.newPage({ viewport: { width: 1920, height: 1080 } });
  const errors = [];
  page.on('console', m => { if (m.type() === 'error') errors.push(m.text()); });
  await ready(page);

  const heads = await page.$$eval('#away-batting-grid thead th', ths => ths.map(t => t.textContent.trim()));
  ok('AC1 header Number,Name,Position,1..9', heads.join(',') === 'Number,Name,Position,1,2,3,4,5,6,7,8,9', heads.join(','));
  const rows = await page.$$eval('#away-batting-grid tbody tr.batter', trs => trs.map(tr => [...tr.querySelectorAll('td')].slice(0, 3).map(td => td.textContent.trim()).join('|')));
  ok('AC1 9 rows in batting order', rows.length === 9 && rows[0].startsWith('8|Ian Happ|') && rows[8].startsWith('34|Carson Kelly|'), rows.join(' ; '));
  const empty = (await page.textContent('#home-batting-grid-empty')).trim();
  ok('AC4 home message', empty === 'Save a lineup to start scoring.', empty);
  ok('AC4 home has no table', (await page.$$('#home-batting-grid table')).length === 0);

  await type(page, 'away-slot-1-inning-1-note', '6-3');
  await type(page, 'away-slot-2-inning-1-note', 'K');
  await type(page, 'away-slot-3-inning-2-note', 'ABCDEFGHIJKLMNOPQRST');
  ok('AC2 maxlength=20', (await page.getAttribute('#away-slot-1-inning-1-note', 'maxlength')) === '20');
  await type(page, 'away-inning-1-hits', '1');
  await type(page, 'away-inning-1-lob', '2');
  await type(page, 'away-inning-2-hits', '0');
  await type(page, 'away-inning-2-lob', '0');
  const hc = await page.$$eval('#away-batting-grid tr.totals input[id$="-hits"]', x => x.length);
  const lc = await page.$$eval('#away-batting-grid tr.totals input[id$="-lob"]', x => x.length);
  ok('AC3 HITS + LOB box per inning', hc === 9 && lc === 9, `${hc}/${lc}`);

  await ready(page);
  ok('AC2 6-3 survives reload', (await val(page, 'away-slot-1-inning-1-note')) === '6-3');
  ok('AC2 K survives reload', (await val(page, 'away-slot-2-inning-1-note')) === 'K');
  ok('AC2 20-char note survives reload', (await val(page, 'away-slot-3-inning-2-note')) === 'ABCDEFGHIJKLMNOPQRST');
  ok('AC3 hits/lob survive reload', (await val(page, 'away-inning-1-hits')) === '1' && (await val(page, 'away-inning-1-lob')) === '2' && (await val(page, 'away-inning-2-hits')) === '0' && (await val(page, 'away-inning-2-lob')) === '0');
  await page.screenshot({ path: `${out}/desktop.png`, fullPage: true });

  await type(page, 'away-slot-3-inning-2-note', '');
  await ready(page);
  ok('AC2 cleared note gone after reload', (await val(page, 'away-slot-3-inning-2-note')) === '');

  for (const bad of ['-1', 'two', '1.5']) {
    await type(page, 'away-inning-1-hits', bad);
    const msg = await errText(page);
    ok(`AC3 bad HITS "${bad}" shows message`, msg === MSG, msg);
  }
  await page.locator('#away-batting-grid').screenshot({ path: `${out}/ac3-bad-hits-message.png` });
  await ready(page);
  ok('AC3 bad entry not saved (HITS still 1)', (await val(page, 'away-inning-1-hits')) === '1');

  // R2: a refused entry, then a good save elsewhere, leaves no refused text looking saved
  await type(page, 'away-inning-3-lob', 'x');
  ok('R2 refused LOB shows message and keeps typed text', (await errText(page)) === MSG && (await val(page, 'away-inning-3-lob')) === 'x');
  await type(page, 'away-slot-5-inning-3-note', 'F8');
  const lob3 = await val(page, 'away-inning-3-lob');
  ok('R2 good save elsewhere puts refused box back to saved (blank) and clears message', lob3 === '' && (await errText(page)) === '', `lob3="${lob3}"`);
  await page.locator('#away-batting-grid').screenshot({ path: `${out}/r2-after-good-save.png` });

  // R3 live: swap batting slots 1 and 2 in the saved lineup; the slot-1 note stays on slot 1
  const p1 = await page.inputValue('#away-slot-1-player');
  const p2 = await page.inputValue('#away-slot-2-player');
  await page.selectOption('#away-slot-1-player', p2);
  await page.selectOption('#away-slot-2-player', p1);
  await page.click('#away-save-lineup');
  await page.waitForSelector('#away-lineup-saved');
  await ready(page);
  const r1 = await page.$$eval('#away-batting-grid tbody tr.batter', trs => [...trs[0].querySelectorAll('td')].slice(1, 2).map(td => td.textContent.trim()).join(''));
  ok('R3 slot 1 now Dansby Swanson and note 6-3 stayed on slot 1', r1 === 'Dansby Swanson' && (await val(page, 'away-slot-1-inning-1-note')) === '6-3' && (await val(page, 'away-slot-2-inning-1-note')) === 'K', `slot1=${r1}`);

  await page.setViewportSize({ width: 1024, height: 768 });
  await page.screenshot({ path: `${out}/tablet.png`, fullPage: true });
  await page.setViewportSize({ width: 375, height: 667 });
  await page.screenshot({ path: `${out}/mobile.png`, fullPage: true });
  log.push(`INFO mobile scrollWidth=${await page.evaluate(() => document.documentElement.scrollWidth)} (viewport 375)`);
  log.push(`INFO console errors: ${errors.length ? errors.join(' | ') : 'none'}`);
  await browser.close();
  console.log(log.join('\n'));
})().catch(e => { console.log(log.join('\n')); console.error(e); process.exit(1); });
