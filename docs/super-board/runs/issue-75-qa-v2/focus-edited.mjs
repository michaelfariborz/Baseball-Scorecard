// Issue #75 QA v2 (review R1/R3): focus ring on fields Blazor marked edited (.valid.modified) or
// invalid (.invalid), reached by keyboard; and the current-nav underline contrast on the header.
import { chromium } from 'playwright-core';
import fs from 'node:fs';
const BASE = 'http://127.0.0.1:5976';
const OUT = process.argv[2];
const browser = await chromium.launch({ executablePath: '/usr/bin/google-chrome', headless: true });

const parse = c => { const m = c.match(/rgba?\(([^)]+)\)/); const p = m[1].split(/[ ,/]+/).filter(Boolean).map(Number); return { r: p[0], g: p[1], b: p[2] }; };
const lum = ({ r, g, b }) => { const f = v => { v /= 255; return v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4; }; return 0.2126 * f(r) + 0.7152 * f(g) + 0.0722 * f(b); };
const ratio = (a, b) => { const [x, y] = [lum(a), lum(b)].sort((p, q) => q - p); return +((x + 0.05) / (y + 0.05)).toFixed(2); };

async function state(page, sel) {
  return page.$eval(sel, el => {
    const s = getComputedStyle(el);
    let bg = null; for (let e = el.parentElement; e && !bg; e = e.parentElement) { const c = getComputedStyle(e).backgroundColor; if (!/rgba\(0, 0, 0, 0\)|transparent/.test(c)) bg = c; }
    return { cls: el.className, focused: document.activeElement === el, fv: el.matches(':focus-visible'), outline: `${s.outlineStyle} ${s.outlineWidth} ${s.outlineColor}`, color: s.outlineColor, width: s.outlineWidth, bg: bg ?? getComputedStyle(document.body).backgroundColor };
  });
}

// Make the field edited/invalid, Tab away, Shift+Tab back so focus comes from the keyboard.
const CASES = [
  { name: 'team-name edited (valid.modified)', path: '/rosters/1/edit', sel: '#team-name', act: p => p.fill('#team-name', '[TEST] Cubs X'), want: /\bmodified\b.*\bvalid\b|\bvalid\b.*\bmodified\b/ },
  { name: 'team-name cleared (invalid)', path: '/rosters/1/edit', sel: '#team-name', act: p => p.fill('#team-name', ''), want: /\binvalid\b/ },
  { name: 'innings edited (valid.modified)', path: '/games/new', sel: '#innings', act: p => p.fill('#innings', '7'), want: /\bmodified\b/ },
  { name: 'innings bad value (invalid)', path: '/games/new', sel: '#innings', act: p => p.fill('#innings', '1.5'), want: /\binvalid\b/ },
  { name: 'away select edited (valid.modified)', path: '/games/new', sel: '#away', act: async p => { await p.focus('#away'); await p.keyboard.press('ArrowDown'); }, want: /\bmodified\b/ },
];

const report = { fields: [], nav: [] };
for (const scheme of ['light', 'dark']) {
  const ctx = await browser.newContext({ viewport: { width: 1280, height: 900 }, colorScheme: scheme });
  const page = await ctx.newPage();
  for (const c of CASES) {
    await page.goto(BASE + c.path, { waitUntil: 'networkidle' }); await page.waitForTimeout(700);
    await c.act(page);
    await page.keyboard.press('Tab'); await page.waitForTimeout(500);
    await page.keyboard.press('Shift+Tab'); await page.waitForTimeout(300);
    const s = await state(page, c.sel);
    const ok = s.focused && s.fv && c.want.test(s.cls) && s.outline.startsWith('solid') && parseFloat(s.width) >= 2;
    const row = { scheme, case: c.name, ...s, ringVsBg: ratio(parse(s.color), parse(s.bg)), pass: ok && ratio(parse(s.color), parse(s.bg)) >= 3 };
    report.fields.push(row);
    if (c.name.startsWith('team-name edited') || c.name.startsWith('innings bad')) {
      const slug = c.name.startsWith('team') ? 'edited-field-focus' : 'invalid-field-focus';
      await page.locator(c.sel).screenshot({ path: `${OUT}/${slug}-${scheme}.png` }).catch(() => {});
      const box = await page.locator(c.sel).boundingBox();
      await page.screenshot({ path: `${OUT}/${slug}-${scheme}-desktop.png`, clip: { x: 0, y: Math.max(0, box.y - 200), width: 1280, height: 400 } });
    }
  }
  for (const path of ['/', '/rosters', '/games/new']) {
    await page.goto(BASE + path, { waitUntil: 'networkidle' }); await page.waitForTimeout(500);
    const n = await page.$eval('header .app-nav a[aria-current="page"]', a => {
      const s = getComputedStyle(a); let bg = null;
      for (let e = a; e && !bg; e = e.parentElement) { const c = getComputedStyle(e).backgroundColor; if (!/rgba\(0, 0, 0, 0\)|transparent/.test(c)) bg = c; }
      return { text: a.textContent.trim(), underline: `${s.borderBottomStyle} ${s.borderBottomWidth} ${s.borderBottomColor}`, color: s.borderBottomColor, bg };
    });
    const others = await page.$$eval('header .app-nav a:not([aria-current])', as => as.map(a => getComputedStyle(a).borderBottomColor));
    report.nav.push({ scheme, path, ...n, underlineVsHeader: ratio(parse(n.color), parse(n.bg)), othersUnderline: [...new Set(others)] });
  }
  await page.goto(BASE + '/rosters', { waitUntil: 'networkidle' }); await page.waitForTimeout(500);
  await page.locator('header').screenshot({ path: `${OUT}/nav-current-${scheme}.png` });
  await ctx.close();
}
await browser.close();
fs.writeFileSync(`${OUT}/focus-edited-report.json`, JSON.stringify(report, null, 2));
console.log(JSON.stringify(report, null, 1));
