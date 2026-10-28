// Issue #26 browser check. Usage: node browser-check.mjs <base-url> <out-dir>
// Expects rosters Cubs 2025, Sox 2025, Cubs 2026, Mets 2026 and no games.
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
const opts = sel => page.$$eval(`${sel} option`, os => os.map(o => o.textContent.trim()));
async function open() {
  await page.goto(`${base}/games/new`, { waitUntil: 'networkidle' });
  await page.waitForFunction(() => window.Blazor !== undefined);
  await page.waitForTimeout(800);
}
async function setDate(v) {
  await page.fill('#date', v);
  await page.dispatchEvent('#date', 'change');
  await page.waitForTimeout(600);
}
const pickByText = (sel, text) => page.selectOption(sel, { label: text });

// Home -> New game
await page.goto(base, { waitUntil: 'networkidle' });
const link = page.getByRole('link', { name: /new game/i });
log(await link.count() > 0, 'Home has a New game link', `count=${await link.count()}`);

// AC1
await open();
const labels = await page.$$eval('form label', ls => ls.map(l => l.textContent.trim()));
const yearInputs = await page.$$('#year');
log(yearInputs.length === 0 && !labels.includes('Year'), 'AC1 no Year field', `labels=${JSON.stringify(labels)}`);
await shot('desktop.png');
await page.setViewportSize({ width: 1024, height: 768 }); await shot('tablet.png');
await page.setViewportSize({ width: 375, height: 667 }); await shot('mobile.png');
await page.setViewportSize({ width: 1920, height: 1080 });

// AC2
await setDate('2025-06-01');
let away = await opts('#away'), home = await opts('#home');
log(JSON.stringify(away) === '["Pick a team","Cubs 2025","Sox 2025"]' && JSON.stringify(home) === JSON.stringify(away),
  'AC2 Date 2025-06-01 lists only 2025', `away=${JSON.stringify(away)} home=${JSON.stringify(home)}`);
await pickByText('#away', 'Cubs 2025');
await pickByText('#home', 'Sox 2025');
await page.waitForTimeout(300);
await shot('ac2-2025-picked-desktop.png');
await setDate('2026-04-10');
away = await opts('#away'); home = await opts('#home');
const av = await page.inputValue('#away'), hv = await page.inputValue('#home');
log(JSON.stringify(away) === '["Pick a team","Cubs 2026","Mets 2026"]' && JSON.stringify(home) === JSON.stringify(away) && av === '0' && hv === '0',
  'AC2 Date 2026-04-10 lists only 2026 and clears 2025 picks', `away=${JSON.stringify(away)} awayValue=${av} homeValue=${hv}`);
await shot('ac2-2026-cleared-desktop.png');

// AC4 existing checks in the browser: no team
await page.click('button[type=submit]');
await page.waitForSelector('[role=alert]');
let msg = await page.textContent('[role=alert]');
log(msg.trim() === "Pick an Away Team and a Home Team from the rosters for the Date's year.", 'AC4 no team message', msg.trim());
await shot('ac4-no-team-desktop.png');
// same team
await pickByText('#away', 'Cubs 2026'); await pickByText('#home', 'Cubs 2026');
await page.click('button[type=submit]');
await page.waitForFunction(() => document.querySelector('[role=alert]')?.textContent.includes('different'));
msg = await page.textContent('[role=alert]');
log(msg.trim() === 'Away Team and Home Team must be different.', 'AC4 same team message', msg.trim());
await shot('ac4-same-team-desktop.png');
// innings 0
await pickByText('#home', 'Mets 2026');
await page.fill('#innings', '0'); await page.dispatchEvent('#innings', 'change');
await page.click('button[type=submit]');
await page.waitForFunction(() => document.querySelector('[role=alert]')?.textContent.includes('Innings'));
msg = await page.textContent('[role=alert]');
log(msg.trim() === 'Innings must be a whole number of at least 1.', 'AC4 innings < 1 message', msg.trim());
await shot('ac4-innings-desktop.png');
log(page.url().endsWith('/games/new'), 'AC4 rejected saves stay on /games/new', page.url());

// AC3 + double-click: Date 2025-06-01, pick 2025 teams, double-click Save
await open();
await setDate('2025-06-01');
await pickByText('#away', 'Cubs 2025'); await pickByText('#home', 'Sox 2025');
await page.waitForTimeout(300);
await page.dblclick('button[type=submit]');
await page.waitForURL(/\/games\/\d+$/, { timeout: 10000 });
log(true, 'AC3 save opened the game page', page.url());
await page.waitForTimeout(800);
await shot('ac3-saved-game-desktop.png');
console.log(`GAME_ID=${page.url().split('/').pop()}`);

log(problems.length === 0, 'No page errors, console errors or 4xx/5xx', problems.join(' | ') || 'none');
await browser.close();
console.log(`SUMMARY ${results.filter(Boolean).length}/${results.length} passed`);
process.exit(results.every(Boolean) ? 0 : 1);
