// Keyboard modality on, then focus each focusable element on the four AC pages and record its :focus-visible outline.
import { chromium } from 'playwright-core';
import fs from 'node:fs';
const BASE = 'http://127.0.0.1:5975';
const browser = await chromium.launch({ executablePath: '/usr/bin/google-chrome', headless: true });
const out = {};
for (const scheme of ['light', 'dark']) {
  const page = await (await browser.newContext({ viewport: { width: 1280, height: 900 }, colorScheme: scheme })).newPage();
  for (const path of ['/', '/rosters', '/rosters/1/edit', '/games/new']) {
    await page.goto(BASE + path, { waitUntil: 'networkidle' }); await page.waitForTimeout(600);
    await page.keyboard.press('Tab');
    const n = await page.$$eval('a[href], button, input, select, textarea', els => els.filter(e => !e.disabled && e.getBoundingClientRect().width > 0 && !e.closest('#blazor-error-ui')).length);
    const rows = [];
    for (let i = 0; i < n; i++) {
      await page.keyboard.press('Shift'); // keep keyboard modality
      rows.push(await page.evaluate(i => {
        const els = [...document.querySelectorAll('a[href], button, input, select, textarea')].filter(e => !e.disabled && e.getBoundingClientRect().width > 0 && !e.closest('#blazor-error-ui'));
        const el = els[i]; el.focus();
        const s = getComputedStyle(el);
        return { el: `${el.tagName.toLowerCase()}${el.id ? '#' + el.id : ''} "${(el.textContent || '').trim().slice(0, 20)}"`, fv: el.matches(':focus-visible'), outline: `${s.outlineStyle} ${s.outlineWidth} ${s.outlineColor}` };
      }, i));
    }
    out[`${scheme} ${path}`] = { checked: rows.length, notVisible: rows.filter(r => !r.fv || r.outline.startsWith('none') || r.outline.includes(' 0px')), outlines: [...new Set(rows.map(r => r.outline))] };
  }
}
await browser.close();
console.log(JSON.stringify(out, null, 1));
fs.writeFileSync(process.argv[2], JSON.stringify(out, null, 2));
