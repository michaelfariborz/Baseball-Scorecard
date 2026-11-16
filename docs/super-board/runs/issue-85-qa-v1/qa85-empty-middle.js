// Repro for the AC4 failure: an empty column 2 between column 1 and a saved column 3 stays after a reload
// and cannot be removed. Usage: node qa85-empty-middle.js <baseUrl> <outDir>
const { chromium } = require('playwright-core');
const path = require('path');
const [base, out] = process.argv.slice(2);
const cell = (s, n, c) => c === 1 ? `#away-slot-${s}-inning-${n}-note` : `#away-slot-${s}-inning-${n}-col-${c}-note`;
async function ready(page) {
  await page.goto(`${base}/games/2`, { waitUntil: 'networkidle' });
  for (let i = 0; i < 40; i++) {
    await page.click(cell(9, 9, 1));
    if (await page.waitForSelector('#away-at-bat-dialog', { timeout: 1000 }).catch(() => null)) {
      await page.keyboard.press('Escape'); await page.waitForSelector('#away-at-bat-dialog', { state: 'detached' }); return;
    }
  }
  throw new Error('not interactive');
}
async function setInning(page, n) { await page.fill('#away-extra-inning', String(n)); await page.dispatchEvent('#away-extra-inning', 'change'); }
(async () => {
  const browser = await chromium.launch({ executablePath: '/usr/bin/google-chrome' });
  const page = await (await browser.newContext({ viewport: { width: 1920, height: 1080 } })).newPage();
  await ready(page);
  await setInning(page, 6); await page.click('#away-extra-add'); await page.waitForSelector(cell(1, 6, 2));
  await setInning(page, 6); await page.click('#away-extra-add'); await page.waitForSelector(cell(1, 6, 3));
  await page.click(cell(4, 6, 3)); await page.waitForSelector('#away-at-bat-dialog');
  await page.locator('#away-at-bat-dialog .at-bat-results input[value="K"]').check();
  await page.click('#away-at-bat-save'); await page.waitForSelector('#away-at-bat-dialog', { state: 'detached' });
  await ready(page);
  const h = await page.$$eval('#away-batting-grid thead th', t => t.map(x => x.textContent.trim()).join(','));
  const col2 = await page.$$eval('[id^="away-slot-"][id$="-inning-6-col-2-note"]', b => b.map(x => x.textContent.trim()).join('')).catch(() => '?');
  console.log(`after reload headers=${h} column2-text="${col2}"`);
  await setInning(page, 6); await page.click('#away-extra-remove'); await page.waitForTimeout(500);
  const msg = await page.$eval('#away-batting-grid-error', e => e.textContent.trim()).catch(() => null);
  console.log(`remove inning 6 -> "${msg}"; column 2 still there=${await page.locator(cell(1, 6, 2)).count() > 0}`);
  await page.locator('#away-batting-grid').screenshot({ path: path.join(out, 'ac4-fail-empty-column-2-kept-desktop.png') });
  await browser.close();
})().catch(e => { console.error(e); process.exit(2); });
