import { chromium } from 'playwright-core';

const [, , base, gameId, out] = process.argv;
const url = `${base}/games/${gameId}`;
const results = [];
const problems = [];
const log = (ok, name, info) => { results.push(ok); console.log(`${ok ? 'PASS' : 'FAIL'} ${name}: ${info}`); };

const browser = await chromium.launch({ executablePath: '/usr/bin/google-chrome', headless: true });
const ctx = await browser.newContext({ viewport: { width: 1920, height: 1080 } });
const page = await ctx.newPage();
page.on('console', m => { if (m.type() === 'error') problems.push(`console: ${m.text()}`); });
page.on('pageerror', e => problems.push(`pageerror: ${e.message}`));
page.on('response', r => { if (r.status() >= 400 && !r.url().endsWith('favicon.ico')) problems.push(`${r.status()} ${r.url()}`); });

async function open() {
  await page.goto(url, { waitUntil: 'networkidle' });
  await page.waitForSelector('#home-slot-9-position');
  // Wait for the interactive circuit so changes reach the server.
  await page.waitForFunction(() => window.Blazor !== undefined);
  await page.waitForTimeout(800);
}
const opts = async sel => (await page.$$eval(`${sel} option`, os => os.map(o => o.textContent.trim()).filter(t => t)));
async function fill(side, playerIdx, positions) {
  const players = await page.$$eval(`#${side}-slot-1-player option`, os => os.map(o => o.value).filter(v => v !== '0'));
  for (let i = 0; i < positions.length; i++) {
    await page.selectOption(`#${side}-slot-${i + 1}-player`, players[playerIdx[i]]);
    await page.selectOption(`#${side}-slot-${i + 1}-position`, positions[i]);
  }
  return players;
}
const shot = name => page.screenshot({ path: `${out}/${name}`, fullPage: true });

const NoDh = ['CenterField', 'Shortstop', 'LeftField', 'FirstBase', 'RightField', 'ThirdBase', 'Catcher', 'SecondBase', 'Pitcher'];
const WithDh = ['CenterField', 'Shortstop', 'LeftField', 'FirstBase', 'DesignatedHitter', 'ThirdBase', 'Catcher', 'SecondBase', 'RightField'];

await open();
// AC1
for (const side of ['away', 'home']) {
  const rows = await page.$$eval(`#lineup-${side} tbody tr`, r => r.length);
  const pos = await opts(`#${side}-slot-1-position`);
  const pl = await opts(`#${side}-slot-1-player`);
  log(rows === 9 && pos.length === 10 && pos[0] === '1 · Pitcher' && pos[9] === '10 · Designated Hitter',
    `AC1 ${side} 9 slots + positions`, `rows=${rows} positions=${JSON.stringify(pos)} players=${pl.length}`);
}
const awayPl = await opts('#away-slot-1-player'), homePl = await opts('#home-slot-1-player');
log(awayPl.every(p => !homePl.includes(p)) && awayPl.length === 11 && homePl.length === 10, 'AC1 each side lists only its roster', `away=${awayPl.length} home=${homePl.length}`);
await shot('ac1-empty-lineups-desktop.png');

// AC2 empty slot
await fill('away', [0, 1, 2, 3, 4], NoDh.slice(0, 5));
await page.click('#away-save-lineup');
await page.waitForSelector('#away-lineup-error');
let msg = await page.textContent('#away-lineup-error');
log(msg === 'Batting slot 6 needs a player and a position.', 'AC2 empty slot', msg);
await shot('ac2-empty-slot-desktop.png');
// AC2 same player
await fill('away', [0, 1, 2, 3, 4, 5, 6, 7, 8], NoDh);
await page.selectOption('#away-slot-3-player', (await page.$$eval('#away-slot-2-player', s => s[0].value)));
await page.click('#away-save-lineup');
await page.waitForFunction(() => document.querySelector('#away-lineup-error')?.textContent.includes('same player'));
msg = await page.textContent('#away-lineup-error');
log(msg === 'Batting slot 3 has the same player as slot 2.', 'AC2 same player', msg);
await shot('ac2-same-player-desktop.png');
// AC2 same position
await fill('away', [0, 1, 2, 3, 4, 5, 6, 7, 8], NoDh);
await page.selectOption('#away-slot-4-position', 'CenterField');
await page.click('#away-save-lineup');
await page.waitForFunction(() => document.querySelector('#away-lineup-error')?.textContent.includes('same position'));
msg = await page.textContent('#away-lineup-error');
log(msg === 'Batting slot 4 has the same position as slot 1.', 'AC2 same position', msg);
await shot('ac2-same-position-desktop.png');
// Nothing saved: reload shows empty away lineup
await open();
const v1 = await page.inputValue('#away-slot-1-player');
log(v1 === '0', 'AC2 rejected saves stored nothing (after reload)', `away slot 1 value=${v1}`);

// AC3 DH
await fill('away', [0, 1, 2, 3, 4, 5, 6, 7, 8], WithDh);
await page.waitForSelector('#away-starting-pitcher');
const pitcherDisabled = await page.$$eval('#lineup-away option[value=Pitcher]', os => os.every(o => o.disabled));
const homePitcherEnabled = await page.$$eval('#lineup-home option[value=Pitcher]', os => os.every(o => !o.disabled));
const spOpts = await opts('#away-starting-pitcher');
log(pitcherDisabled && homePitcherEnabled, 'AC3 Pitcher disabled in away slots only', `away disabled=${pitcherDisabled} home enabled=${homePitcherEnabled}`);
log(JSON.stringify(spOpts) === JSON.stringify(awayPl.slice(9)), 'AC3 starting pitcher lists only non-batting roster players', JSON.stringify(spOpts));
await page.click('#away-save-lineup');
await page.waitForFunction(() => document.querySelector('#away-lineup-error')?.textContent.includes('Starting pitcher'));
msg = await page.textContent('#away-lineup-error');
log(msg.startsWith('Pick a Starting pitcher'), 'AC3 starting pitcher required', msg);
await shot('ac3-dh-pitcher-required-desktop.png');
await page.selectOption('#away-starting-pitcher', { label: awayPl[10] });
await page.click('#away-save-lineup');
await page.waitForSelector('#away-lineup-saved');
log(true, 'AC3 DH lineup saved', await page.textContent('#away-lineup-saved'));
await shot('ac3-dh-saved-desktop.png');

// Home lineup without DH, custom order
await fill('home', [4, 0, 8, 1, 2, 3, 5, 6, 7], NoDh);
await page.click('#home-save-lineup');
await page.waitForSelector('#home-lineup-saved');
const homeExpected = [4, 0, 8, 1, 2, 3, 5, 6, 7].map(i => homePl[i]);

// AC4 reload
await open();
const awayAfter = [], homeAfter = [], awayPos = [], homePos = [];
for (let i = 1; i <= 9; i++) {
  awayAfter.push(await page.$eval(`#away-slot-${i}-player`, s => s.selectedOptions[0]?.textContent.trim()));
  awayPos.push(await page.inputValue(`#away-slot-${i}-position`));
  homeAfter.push(await page.$eval(`#home-slot-${i}-player`, s => s.selectedOptions[0]?.textContent.trim()));
  homePos.push(await page.inputValue(`#home-slot-${i}-position`));
}
const sp = await page.$eval('#away-starting-pitcher', s => s.selectedOptions[0]?.textContent.trim());
log(JSON.stringify(awayAfter) === JSON.stringify(awayPl.slice(0, 9)) && JSON.stringify(awayPos) === JSON.stringify(WithDh) && sp === awayPl[10],
  'AC4 away lineup + starting pitcher reload in batting order', `${JSON.stringify(awayAfter)} sp=${sp}`);
log(JSON.stringify(homeAfter) === JSON.stringify(homeExpected) && JSON.stringify(homePos) === JSON.stringify(NoDh),
  'AC4 home lineup reloads in batting order', JSON.stringify(homeAfter));
await shot('ac4-reloaded-desktop.png');
await page.setViewportSize({ width: 1024, height: 768 }); await shot('tablet.png');
await page.setViewportSize({ width: 375, height: 667 }); await shot('mobile.png');
await page.setViewportSize({ width: 1920, height: 1080 }); await shot('desktop.png');

log(problems.length === 0, 'No console/page errors or 4xx/5xx', problems.join(' | ') || 'none');
await browser.close();
process.exit(results.every(Boolean) ? 0 : 1);
