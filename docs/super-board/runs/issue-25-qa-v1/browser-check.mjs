import { chromium } from 'playwright-core';

const [, , base, out] = process.argv;
const pages = [
  ['rosters', '/rosters', 'h1', 'Rosters'],
  ['roster-new', '/rosters/new', 'h1', null],
  ['game-new', '/games/new', 'h1', null],
  ['game', '/games/1', 'h1', null],
];
const viewports = { desktop: [1920, 1080], tablet: [1024, 768], mobile: [375, 667] };
let fails = 0;
const log = (ok, name, info) => { if (!ok) fails++; console.log(`${ok ? 'PASS' : 'FAIL'} ${name}: ${info}`); };

const browser = await chromium.launch({ executablePath: '/usr/bin/google-chrome', headless: true });
for (const [vp, [w, h]] of Object.entries(viewports)) {
  const ctx = await browser.newContext({ viewport: { width: w, height: h } });
  const page = await ctx.newPage();
  const problems = [];
  page.on('pageerror', e => problems.push(`pageerror: ${e.message}`));
  page.on('response', r => { if (r.status() >= 400) problems.push(`${r.status()} ${r.url()}`); });
  for (const [name, path, sel] of pages) {
    if (vp !== 'desktop' && name !== 'rosters') continue;
    await page.goto(base + path, { waitUntil: 'networkidle' });
    const link = page.locator('nav a[href="/"]');
    const text = (await link.count()) ? (await link.textContent()).trim() : '';
    const linkBox = await link.boundingBox();
    const h1 = (await page.textContent(sel))?.trim();
    const h1Box = await page.locator(sel).first().boundingBox();
    log(text === 'Home' && linkBox && h1Box && linkBox.y < h1Box.y, `AC1/AC2 ${vp} ${path} Home link above body`, `link="${text}" linkY=${linkBox?.y} h1="${h1}" h1Y=${h1Box?.y}`);
    if (name === 'rosters') {
      const newRoster = await page.locator('a', { hasText: 'New roster' }).count();
      const items = await page.locator('main li, li').count();
      log(h1 === 'Rosters' && newRoster === 1, `AC3 ${vp} /rosters content`, `h1="${h1}" newRosterLinks=${newRoster} listItems=${items}`);
    } else {
      const forms = await page.locator('form, select, input').count();
      log(!!h1 && (name === 'game' || forms > 0), `AC3 ${vp} ${path} content`, `h1="${h1}" formControls=${forms}`);
    }
    await page.screenshot({ path: `${out}/ac2-${name}-${vp}.png`, fullPage: true });
  }
  if (vp === 'desktop') {
    await page.goto(base + '/rosters', { waitUntil: 'networkidle' });
    await page.click('nav a[href="/"]');
    await page.waitForURL(base + '/');
    const h1 = (await page.textContent('h1')).trim();
    log(page.url() === base + '/' && h1 === 'Baseball Scorecard', 'AC1 desktop click Home from /rosters', `url=${page.url()} h1="${h1}"`);
    await page.screenshot({ path: `${out}/ac1-home-after-click-desktop.png`, fullPage: true });
  }
  log(problems.length === 0, `${vp} no page errors / 4xx`, problems.join('; ') || 'none');
  await ctx.close();
}
await browser.close();
console.log(fails ? `FAILED ${fails}` : 'ALL PASS');
process.exit(fails ? 1 : 0);
