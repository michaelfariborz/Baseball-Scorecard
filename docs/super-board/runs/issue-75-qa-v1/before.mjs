// Before screenshots from the base branch app (same data) for the PR's Before | After block.
import { chromium } from 'playwright-core';
const OUT = process.argv[2];
const b = await chromium.launch({ executablePath: '/usr/bin/google-chrome', headless: true });
for (const [vp, w, h] of [['desktop', 1920, 1080], ['mobile', 375, 667]]) {
  const p = await (await b.newContext({ viewport: { width: w, height: h }, colorScheme: 'light' })).newPage();
  for (const [slug, path] of [['home', '/'], ['roster-edit', '/rosters/1/edit']]) {
    await p.goto('http://127.0.0.1:5976' + path, { waitUntil: 'networkidle' }); await p.waitForTimeout(600);
    await p.screenshot({ path: `${OUT}/before-${slug}-light-${vp}.png`, fullPage: true });
  }
}
await b.close();
