// Issue #75 QA: header/nav, shared styles, contrast, focus outline, 375px overflow, game page behaviour.
import { chromium } from 'playwright-core';
import fs from 'node:fs';
const BASE = 'http://127.0.0.1:5976';
const OUT = process.argv[2];
fs.mkdirSync(OUT, { recursive: true });
const PAGES = [
  ['home', '/', 'Home'],
  ['rosters', '/rosters', 'Rosters'],
  ['roster-edit', '/rosters/1/edit', null],
  ['new-game', '/games/new', 'New game'],
  ['game', '/games/1', null],
];
const VIEWPORTS = [['desktop', 1920, 1080], ['tablet', 1024, 768], ['mobile', 375, 667]];
const browser = await chromium.launch({ executablePath: '/usr/bin/google-chrome', headless: true });
const report = {};

// Runs in the page: audits contrast, control styling, header and overflow.
function audit() {
  const parse = c => { const m = c.match(/rgba?\(([^)]+)\)/); if (!m) return null; const p = m[1].split(/[ ,/]+/).filter(Boolean).map(Number); return { r: p[0], g: p[1], b: p[2], a: p[3] ?? 1 }; };
  const lum = ({ r, g, b }) => { const f = v => { v /= 255; return v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4; }; return 0.2126 * f(r) + 0.7152 * f(g) + 0.0722 * f(b); };
  const ratio = (a, b) => { const [x, y] = [lum(a), lum(b)].sort((p, q) => q - p); return (x + 0.05) / (y + 0.05); };
  const over = (top, bot) => ({ r: top.r * top.a + bot.r * (1 - top.a), g: top.g * top.a + bot.g * (1 - top.a), b: top.b * top.a + bot.b * (1 - top.a), a: 1 });
  const bgOf = el => { const stack = []; for (let e = el; e; e = e.parentElement) { const c = parse(getComputedStyle(e).backgroundColor); if (c && c.a > 0) { stack.push(c); if (c.a >= 1) break; } } let bg = { r: 255, g: 255, b: 255, a: 1 }; const root = parse(getComputedStyle(document.documentElement).backgroundColor); if (root && root.a > 0) bg = root; for (let i = stack.length - 1; i >= 0; i--) bg = over(stack[i], bg); return bg; };
  const visible = el => { const r = el.getBoundingClientRect(); const s = getComputedStyle(el); return r.width > 0 && r.height > 0 && s.visibility !== 'hidden' && s.display !== 'none' && !el.closest('#blazor-error-ui'); };
  const label = el => `${el.tagName.toLowerCase()}${el.id ? '#' + el.id : ''}${el.className && typeof el.className === 'string' ? '.' + el.className.trim().split(/\s+/).join('.') : ''} "${(el.textContent || el.value || '').trim().slice(0, 30)}"`;
  const text = []; let minText = 99;
  for (const el of document.querySelectorAll('body *')) {
    if (!visible(el)) continue;
    const own = [...el.childNodes].some(n => n.nodeType === 3 && n.textContent.trim()) || ['INPUT', 'SELECT', 'TEXTAREA'].includes(el.tagName);
    if (!own) continue;
    const s = getComputedStyle(el); const fg = parse(s.color); const bg = bgOf(el);
    const r = ratio(over(fg, bg), bg); const size = parseFloat(s.fontSize); const bold = +s.fontWeight >= 700;
    const need = (size >= 24 || (size >= 18.66 && bold)) ? 3 : 4.5;
    minText = Math.min(minText, r);
    if (r < need) text.push({ el: label(el), ratio: +r.toFixed(2), need, disabled: el.disabled || !!el.closest(':disabled') });
  }
  const controls = [];
  for (const el of document.querySelectorAll('button, .button, input, select, textarea, table')) {
    if (!visible(el)) continue;
    const s = getComputedStyle(el); const bg = bgOf(el);
    controls.push({ el: label(el), tag: el.tagName, bg: s.backgroundColor, color: s.color, border: `${s.borderTopWidth} ${s.borderTopStyle} ${s.borderTopColor}`, font: s.fontFamily.slice(0, 20), borderVsBg: s.borderTopStyle !== 'none' && parseFloat(s.borderTopWidth) > 0 ? +ratio(parse(s.borderTopColor), bg).toFixed(2) : null });
  }
  const nav = [...document.querySelectorAll('header .app-nav a')].map(a => ({ text: a.textContent.trim(), href: a.getAttribute('href'), current: a.getAttribute('aria-current'), weight: getComputedStyle(a).fontWeight, underline: getComputedStyle(a).borderBottomColor }));
  const root = getComputedStyle(document.documentElement);
  return {
    brand: document.querySelector('header .brand')?.textContent.trim() ?? null, nav,
    tokens: ['--color-bg', '--color-text', '--color-primary', '--color-accent', '--font-body', '--space-3'].map(t => [t, root.getPropertyValue(t).trim()]),
    bodyBg: getComputedStyle(document.body).backgroundColor, bodyFont: getComputedStyle(document.body).fontFamily.slice(0, 30),
    scrollWidth: document.documentElement.scrollWidth, clientWidth: document.documentElement.clientWidth,
    minText: +minText.toFixed(2), textFails: text, controls,
  };
}

// Runs in the page: outline on the focused element and its contrast with the background behind it.
function focusInfo() {
  const el = document.activeElement; if (!el || el === document.body) return null;
  const parse = c => { const m = c.match(/rgba?\(([^)]+)\)/); if (!m) return null; const p = m[1].split(/[ ,/]+/).filter(Boolean).map(Number); return { r: p[0], g: p[1], b: p[2], a: p[3] ?? 1 }; };
  const lum = ({ r, g, b }) => { const f = v => { v /= 255; return v <= 0.03928 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4; }; return 0.2126 * f(r) + 0.7152 * f(g) + 0.0722 * f(b); };
  const ratio = (a, b) => { const [x, y] = [lum(a), lum(b)].sort((p, q) => q - p); return (x + 0.05) / (y + 0.05); };
  let bg = null; for (let e = el.parentElement; e; e = e.parentElement) { const c = parse(getComputedStyle(e).backgroundColor); if (c && c.a >= 1) { bg = c; break; } }
  bg ??= parse(getComputedStyle(document.documentElement).backgroundColor) ?? { r: 255, g: 255, b: 255, a: 1 };
  const s = getComputedStyle(el);
  return { el: `${el.tagName.toLowerCase()}${el.id ? '#' + el.id : ''} "${(el.textContent || '').trim().slice(0, 20)}"`, style: s.outlineStyle, width: s.outlineWidth, color: s.outlineColor, vsBg: s.outlineStyle !== 'none' ? +ratio(parse(s.outlineColor), bg).toFixed(2) : 0 };
}

for (const scheme of ['light', 'dark']) {
  for (const [vp, w, h] of VIEWPORTS) {
    const ctx = await browser.newContext({ viewport: { width: w, height: h }, colorScheme: scheme });
    const page = await ctx.newPage();
    const errors = []; page.on('console', m => m.type() === 'error' && errors.push(m.text())); page.on('pageerror', e => errors.push(String(e)));
    for (const [slug, path, current] of PAGES) {
      await page.goto(BASE + path, { waitUntil: 'networkidle' }); await page.waitForTimeout(600);
      const key = `${slug}|${scheme}|${vp}`;
      const a = await page.evaluate(audit);
      a.expectedCurrent = current; a.errors = [...errors]; errors.length = 0;
      if (vp === 'desktop') {
        const focus = [];
        for (let i = 0; i < 60; i++) { await page.keyboard.press('Tab'); const f = await page.evaluate(focusInfo); if (!f) break; if (focus.some(x => x.el === f.el)) break; focus.push(f); }
        a.focus = focus;
        await page.mouse.click(1, 1);
      }
      await page.screenshot({ path: `${OUT}/${slug}-${scheme}-${vp}.png`, fullPage: slug !== 'game' });
      report[key] = a;
    }
    await ctx.close();
  }
}

// Game page: a cell opens the at-bat dialog, Cancel closes it, totals unchanged.
{
  const ctx = await browser.newContext({ viewport: { width: 1920, height: 1080 } });
  const page = await ctx.newPage();
  await page.goto(BASE + '/games/1', { waitUntil: 'networkidle' }); await page.waitForTimeout(600);
  const totals = () => page.$$eval('tr.totals, #line-score table, table', ts => ts.map(t => t.innerText).join('\n---\n'));
  const before = await totals();
  const cell = page.locator('.at-bat-cell').first();
  const cellBg = await cell.evaluate(e => getComputedStyle(e).backgroundColor);
  await cell.click();
  const dialog = page.locator('[role=dialog]').first();
  await dialog.waitFor({ timeout: 5000 });
  await page.screenshot({ path: `${OUT}/game-at-bat-dialog-light-desktop.png` });
  const dialogBg = await dialog.evaluate(e => getComputedStyle(e).backgroundColor);
  await page.locator('[id$=at-bat-cancel]').first().click();
  await page.waitForTimeout(400);
  const open = await page.locator('[role=dialog]').count();
  const after = await totals();
  report.game = { cells: await page.locator('.at-bat-cell').count(), cellBg, dialogBg, dialogClosed: open === 0, totalsUnchanged: before === after };
  await ctx.close();
  const dctx = await browser.newContext({ viewport: { width: 1920, height: 1080 }, colorScheme: 'dark' });
  const dp = await dctx.newPage();
  await dp.goto(BASE + '/games/1', { waitUntil: 'networkidle' }); await dp.waitForTimeout(600);
  await dp.locator('.at-bat-cell').first().click();
  await dp.locator('[role=dialog]').first().waitFor();
  await dp.screenshot({ path: `${OUT}/game-at-bat-dialog-dark-desktop.png` });
  report.game.dialogBgDark = await dp.locator('[role=dialog]').first().evaluate(e => getComputedStyle(e).backgroundColor);
  await dctx.close();
}
await browser.close();
fs.writeFileSync(`${OUT}/qa-report.json`, JSON.stringify(report, null, 2));
console.log('wrote', OUT);
