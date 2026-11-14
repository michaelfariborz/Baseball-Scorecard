// Seeds two rosters (9 players each), a game and both lineups through the UI.
import { chromium } from 'playwright-core';
const BASE = 'http://127.0.0.1:5976';
const browser = await chromium.launch({ executablePath: '/usr/bin/google-chrome', headless: true });
const page = await browser.newPage();
const settle = () => page.waitForTimeout(700);
async function go(path) { await page.goto(BASE + path, { waitUntil: 'networkidle' }); await settle(); }

for (const team of ['[TEST] Cubs', '[TEST] Cardinals']) {
  await go('/rosters/new');
  await page.fill('#team-name', team);
  await page.fill('#year', String(new Date().getFullYear()));
  await page.click('button[type=submit]');
  await page.waitForURL(/\/rosters$/); await settle();
  await page.click(`text=${team}`);
  await page.waitForURL(/edit/); await settle();
  for (let i = 1; i <= 9; i++) {
    await page.fill('#player-number', String(i));
    await page.fill('#player-first', 'Test');
    await page.fill('#player-last', `${team.slice(7)}${i}`);
    await page.click('#player-form button[type=submit]');
    await page.waitForFunction(n => document.querySelectorAll('#players li').length >= n, i);
  }
}

await go('/games/new');
await page.selectOption('#away', { label: '[TEST] Cubs' });
await page.selectOption('#home', { label: '[TEST] Cardinals' });
await settle();
await page.click('button[type=submit]');
await page.waitForURL(/\/games\/\d+/); await settle();
console.log('game url', page.url());
for (const side of ['away', 'home']) {
  for (let n = 1; n <= 9; n++) {
    await page.selectOption(`#${side}-slot-${n}-player`, { index: n });
    await page.selectOption(`#${side}-slot-${n}-position`, { index: n });
  }
  await page.click(`#${side}-save-lineup`);
  await page.waitForSelector(`#${side}-lineup-saved, #${side}-lineup-error`);
  const err = await page.$(`#${side}-lineup-error`);
  if (err) console.log(side, 'lineup error', await err.textContent());
}
await browser.close();
