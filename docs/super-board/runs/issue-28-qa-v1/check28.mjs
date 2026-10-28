// Issue #28 browser check. Usage: node check28.mjs <base-url> <out-dir>
// Expects game 1 = Cubs (away) at Sox (home), 9 innings; game 2 = Sox at Cubs, 7 innings; no runs yet.
import { chromium } from 'playwright-core';

const [, , base, out] = process.argv;
const results = [];
const problems = [];
const log = (ok, name, info) => { results.push(ok); console.log(`${ok ? 'PASS' : 'FAIL'} ${name}: ${info}`); };

const browser = await chromium.launch({ executablePath: '/usr/bin/google-chrome', headless: true });
const ctx = await browser.newContext({ viewport: { width: 1920, height: 1080 } });
const page = await ctx.newPage();
page.on('console', m => { if (m.type() === 'error') problems.push(`console: ${m.text()}`); });
page.on('pageerror', e => problems.push(`pageerror: ${e.message}`));
page.on('response', r => { if (r.status() >= 400) problems.push(`${r.status()} ${r.url()}`); });

const shot = name => page.screenshot({ path: `${out}/${name}`, fullPage: true });
const section = name => page.locator('#line-score').screenshot({ path: `${out}/${name}` });
async function open(id) {
  await page.goto(`${base}/games/${id}`, { waitUntil: 'networkidle' });
  await page.waitForSelector('#home-final');
  await page.waitForTimeout(800);
}
const headers = () => page.$$eval('#line-score thead th', ths => ths.map(t => t.textContent.trim()));
const rowNames = () => page.$$eval('#line-score tbody tr th', ths => ths.map(t => t.textContent.trim()));
const final = side => page.textContent(`#${side}-final`).then(t => t.trim());
const cell = (side, n) => page.inputValue(`#${side}-inning-${n}-runs`);
async function type(side, n, text) {
  const sel = `#${side}-inning-${n}-runs`;
  await page.fill(sel, text);
  await page.press(sel, 'Tab');
  await page.waitForTimeout(700);
}

// AC1
await open(1);
let h = await headers();
log(JSON.stringify(h) === '["","1","2","3","4","5","6","7","8","9","Final"]', 'AC1 header innings 1-9 then Final', JSON.stringify(h));
let r = await rowNames();
log(JSON.stringify(r) === '["Cubs","Sox"]', 'AC1 one row per team, Away (Cubs) first', JSON.stringify(r));
log(await final('away') === '0' && await final('home') === '0', 'AC3 Final 0 before any runs', `${await final('away')} / ${await final('home')}`);
await shot('desktop.png');
await page.setViewportSize({ width: 1024, height: 768 }); await shot('tablet.png');
await page.setViewportSize({ width: 375, height: 667 }); await shot('mobile.png');
await page.setViewportSize({ width: 1920, height: 1080 });

// AC2 + AC3
await type('away', 1, '2');
await type('away', 4, '3');
await type('home', 2, '1');
await type('home', 5, '0');
log(await final('away') === '5' && await final('home') === '1', 'AC3 Final is the sum (2+3, 1+0)', `${await final('away')} / ${await final('home')}`);
log(await cell('away', 2) === '' && await cell('away', 3) === '', 'AC3 blank innings stay blank', `away2='${await cell('away', 2)}' away3='${await cell('away', 3)}'`);
await section('ac2-ac3-typed-desktop.png');
await page.reload({ waitUntil: 'networkidle' });
await page.waitForSelector('#home-final'); await page.waitForTimeout(800);
const after = [await cell('away', 1), await cell('away', 4), await cell('home', 2), await cell('home', 5), await cell('away', 2)];
log(JSON.stringify(after) === '["2","3","1","0",""]', 'AC2 values still there after reload', JSON.stringify(after));
log(await final('away') === '5' && await final('home') === '1', 'AC3 Final after reload', `${await final('away')} / ${await final('home')}`);
await shot('ac2-after-reload-desktop.png');

// AC4
for (const bad of ['-1', 'two', '1.5']) {
  await type('away', 1, bad);
  const msg = (await page.textContent('#line-score-error').catch(() => null))?.trim();
  log(msg === 'Runs must be a whole number of 0 or more.', `AC4 '${bad}' shows the message`, String(msg));
  log(await final('away') === '5', `AC4 '${bad}' not saved (Final stays 5)`, await final('away'));
}
await section('ac4-rejected-desktop.png');
await page.reload({ waitUntil: 'networkidle' });
await page.waitForSelector('#home-final'); await page.waitForTimeout(800);
log(await cell('away', 1) === '2', 'AC4 reload shows the saved 2, not the rejected entry', await cell('away', 1));
log(await page.locator('#line-score-error').count() === 0, 'AC4 no message after reload', '');

// AC5
await open(2);
h = await headers();
const inputs = await page.locator('#line-score tbody tr:first-child input').count();
log(JSON.stringify(h) === '["","1","2","3","4","5","6","7","Final"]' && inputs === 7, 'AC5 7-inning game shows 7 inning columns', `${JSON.stringify(h)} inputs=${inputs}`);
r = await rowNames();
log(JSON.stringify(r) === '["Sox","Cubs"]', 'AC1 game 2 Away (Sox) first', JSON.stringify(r));
await section('ac5-seven-innings-desktop.png');

log(problems.length === 0, 'no console errors, page errors or 4xx/5xx', JSON.stringify(problems));
await browser.close();
const failed = results.filter(x => !x).length;
console.log(`${results.length - failed}/${results.length} passed`);
process.exit(failed ? 1 : 0);
