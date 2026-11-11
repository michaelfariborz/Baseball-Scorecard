// Browser QA for issue #62: fielders on outs and errors, plus DP/TP. Usage: node qa62.js <baseUrl> <outDir>
const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');
const [base, out] = process.argv.slice(2);
const results = [], consoleErrors = [], pageErrors = [], bad = [];
function check(ac, name, ok, detail = '') {
  results.push({ ac, name, ok: !!ok, detail });
  console.log(`${ok ? 'PASS' : 'FAIL'} ${ac} ${name} ${detail}`);
}
const id = (side, s, n) => `#${side}-slot-${s}-inning-${n}-note`;
async function ready(page) {
  await page.goto(`${base}/games/1`, { waitUntil: 'networkidle' });
  await page.waitForSelector(id('away', 1, 1));
  for (let i = 0; i < 40; i++) {
    await page.click(id('away', 9, 9));
    if (await page.waitForSelector('#away-at-bat-dialog', { timeout: 1000 }).catch(() => null)) {
      await page.keyboard.press('Escape');
      await page.waitForSelector('#away-at-bat-dialog', { state: 'detached' });
      return;
    }
    await page.waitForTimeout(250);
  }
  throw new Error('circuit never became interactive');
}
const note = (page, sel) => page.$eval(`${sel} .at-bat-note`, e => e.textContent.trim()).catch(() => null);
const has = async (page, sel) => (await page.locator(sel).count()) > 0;
const multiChecked = (page, side) => page.evaluate(s => {
  const r = document.querySelector(`#${s}-at-bat-multi-out input[type=radio]:checked`); return r ? r.value : null; }, side);
async function open(page, side, s, n) { await page.click(id(side, s, n)); await page.waitForSelector(`#${side}-at-bat-dialog`); }
async function closed(page, side) { await page.waitForSelector(`#${side}-at-bat-dialog`, { state: 'detached' }); }
async function pick(page, side, code) { await page.locator(`#${side}-at-bat-dialog .at-bat-results input[value="${code}"]`).check(); }
async function waitNote(page, sel, text) { await page.waitForFunction(([s, t]) => (document.querySelector(`${s} .at-bat-note`)?.textContent.trim() ?? '') === t, [sel, text], { timeout: 5000 }).catch(() => null); }
// What a scorer does: click into the box, type, then click Save (no blur first).
async function typeInto(page, sel, text) { await page.click(sel); await page.keyboard.press('Control+A'); await page.keyboard.press('Backspace'); await page.keyboard.type(text); }

(async () => {
  fs.mkdirSync(out, { recursive: true });
  const browser = await chromium.launch();
  const ctx = await browser.newContext({ viewport: { width: 1920, height: 1080 } });
  const page = await ctx.newPage();
  page.on('console', m => { if (m.type() === 'error') consoleErrors.push(m.text()); });
  page.on('pageerror', e => pageErrors.push(String(e)));
  page.on('response', r => { if (r.status() >= 500 || r.status() === 401 || r.status() === 403) bad.push(`${r.status()} ${r.url()}`); });
  await ready(page);

  // AC4 (old rows): at bats saved before the migration still load and display
  const old = [await note(page, id('away', 1, 1)), await note(page, id('away', 2, 1)), await note(page, id('away', 3, 1)), await note(page, id('home', 1, 1)), await note(page, id('home', 2, 1))];
  check('AC4', 'pre-migration at bats K, 6-3, GO, BB, E still show unchanged', old.join('|') === 'K|6-3|GO|BB|E', `notes=${old.join('|')}`);
  await page.screenshot({ path: path.join(out, 'ac4-old-at-bats-desktop.png'), fullPage: true });
  await open(page, 'away', 3, 1);
  const oldGo = [await page.inputValue('#away-at-bat-fielders'), await multiChecked(page, 'away')];
  await page.keyboard.press('Escape'); await closed(page, 'away');
  check('AC4', 'old GO cell reopens with blank fielders and DP/TP none', oldGo[0] === '' && oldGo[1] === '', `fielders="${oldGo[0]}" multi=${oldGo[1]}`);

  // AC1: which inputs show for which result
  await open(page, 'away', 4, 2);
  const none0 = !(await has(page, '#away-at-bat-fielders')) && !(await has(page, '#away-at-bat-multi-out')) && !(await has(page, '#away-at-bat-error-by'));
  check('AC1', 'no result picked: no fielder inputs', none0);
  for (const code of ['GO', 'FO', 'SF', 'FC']) {
    await pick(page, 'away', code);
    await page.waitForSelector('#away-at-bat-fielders');
    const labels = await page.locator('#away-at-bat-multi-out label').evaluateAll(es => es.map(e => e.textContent.trim()));
    const ok = labels.join(',') === 'none,DP,TP' && (await multiChecked(page, 'away')) === '' && !(await has(page, '#away-at-bat-error-by'));
    check('AC1', `${code}: fielders input + none/DP/TP choice, no error input`, ok, `labels=${labels}`);
    if (code === 'GO') await page.screenshot({ path: path.join(out, 'ac1-go-fielders-dp-tp-desktop.png') });
  }
  await pick(page, 'away', 'E');
  await page.waitForSelector('#away-at-bat-error-by');
  const eLabel = (await page.locator('label[for="away-at-bat-error-by"]').textContent()).trim();
  const eOk = !(await has(page, '#away-at-bat-fielders')) && !(await has(page, '#away-at-bat-multi-out')) && (await page.getAttribute('#away-at-bat-error-by', 'maxlength')) === '1';
  check('AC1', 'E: one fielder input (1–9), no DP/TP', eOk, `label=${eLabel}`);
  await page.screenshot({ path: path.join(out, 'ac1-e-one-fielder-desktop.png') });
  for (const code of ['1B', '2B', '3B', 'HR', 'BB', 'HBP', 'K', 'ꓘ']) {
    await pick(page, 'away', code);
    await page.waitForSelector('#away-at-bat-error-by', { state: 'detached' }).catch(() => null);
    const ok = !(await has(page, '#away-at-bat-fielders')) && !(await has(page, '#away-at-bat-multi-out')) && !(await has(page, '#away-at-bat-error-by'));
    check('AC1', `${code}: no fielder inputs`, ok);
  }
  await page.keyboard.press('Escape'); await closed(page, 'away');

  // AC2: only 1–9 joined by hyphens; anything else refused with a message
  for (const [code, sel, bad] of [['GO', '#away-at-bat-fielders', '6/3'], ['GO', '#away-at-bat-fielders', '0-3'], ['FO', '#away-at-bat-fielders', '8-'], ['FC', '#away-at-bat-fielders', 'SS'], ['GO', '#away-at-bat-fielders', '10'], ['E', '#away-at-bat-error-by', '0'], ['E', '#away-at-bat-error-by', 'x']]) {
    await open(page, 'away', 4, 3);
    await pick(page, 'away', code);
    await page.waitForSelector(sel);
    await typeInto(page, sel, bad);
    await page.click('#away-at-bat-save');
    const msg = await page.waitForSelector('#away-at-bat-error', { timeout: 4000 }).then(e => e.textContent()).catch(() => null);
    const stillOpen = await has(page, '#away-at-bat-dialog');
    if (bad === '6/3') await page.screenshot({ path: path.join(out, 'ac2-bad-fielders-refused-desktop.png') });
    await page.keyboard.press('Escape'); await closed(page, 'away');
    const cell = await note(page, id('away', 4, 3));
    check('AC2', `${code} "${bad}" refused with a message, dialog stays open, cell stays blank`, msg && msg.length > 10 && stillOpen && cell === '', `msg="${msg}" cell="${cell}"`);
  }
  for (const good of ['8', '6-3', '4-6-3', '1-2-3-4-5-6-7-8-9']) {
    await open(page, 'away', 4, 4);
    await pick(page, 'away', 'GO');
    await typeInto(page, '#away-at-bat-fielders', good);
    await page.click('#away-at-bat-save');
    await closed(page, 'away').catch(() => null);
    await waitNote(page, id('away', 4, 4), `GO ${good}`);
    const cell = await note(page, id('away', 4, 4));
    check('AC2', `GO "${good}" accepted`, cell === `GO ${good}`, `cell="${cell}"`);
  }

  // AC3: GO 4-6-3 DP and E6, reload, reopen
  await open(page, 'away', 2, 3);
  await pick(page, 'away', 'GO');
  await typeInto(page, '#away-at-bat-fielders', '4-6-3');
  await page.locator('#away-at-bat-multi-out input[value="DP"]').check();
  await page.screenshot({ path: path.join(out, 'ac3-go-463-dp-entered-desktop.png') });
  await page.click('#away-at-bat-save');
  await closed(page, 'away');
  await waitNote(page, id('away', 2, 3), 'GO 4-6-3 DP');
  const dp = await note(page, id('away', 2, 3));
  await open(page, 'home', 5, 6);
  await pick(page, 'home', 'E');
  await typeInto(page, '#home-at-bat-error-by', '6');
  await page.click('#home-at-bat-save');
  await closed(page, 'home');
  await waitNote(page, id('home', 5, 6), 'E6');
  const e6 = await note(page, id('home', 5, 6));
  await open(page, 'home', 6, 6);
  await pick(page, 'home', 'FO');
  await typeInto(page, '#home-at-bat-fielders', '8');
  await page.locator('#home-at-bat-multi-out input[value="TP"]').check();
  await page.click('#home-at-bat-save');
  await closed(page, 'home');
  await waitNote(page, id('home', 6, 6), 'FO 8 TP');
  check('AC3', 'cells show GO 4-6-3 DP, E6 and FO 8 TP right after save', dp === 'GO 4-6-3 DP' && e6 === 'E6' && (await note(page, id('home', 6, 6))) === 'FO 8 TP', `dp="${dp}" e6="${e6}"`);
  await page.reload({ waitUntil: 'networkidle' });
  await page.waitForSelector(id('away', 2, 3));
  const after = [await note(page, id('away', 2, 3)), await note(page, id('home', 5, 6)), await note(page, id('home', 6, 6))];
  check('AC3', 'after reload cells still show GO 4-6-3 DP / E6 / FO 8 TP', after.join('|') === 'GO 4-6-3 DP|E6|FO 8 TP', after.join('|'));
  await page.screenshot({ path: path.join(out, 'ac3-after-reload-desktop.png'), fullPage: true });
  await ready(page);
  await open(page, 'away', 2, 3);
  const re1 = [await page.locator('#away-at-bat-dialog .at-bat-results input:checked').getAttribute('value'), await page.inputValue('#away-at-bat-fielders'), await multiChecked(page, 'away')];
  await page.screenshot({ path: path.join(out, 'ac3-reopened-go-463-dp-desktop.png') });
  await page.keyboard.press('Escape'); await closed(page, 'away');
  await open(page, 'home', 5, 6);
  const re2 = [await page.locator('#home-at-bat-dialog .at-bat-results input:checked').getAttribute('value'), await page.inputValue('#home-at-bat-error-by')];
  await page.keyboard.press('Escape'); await closed(page, 'home');
  check('AC3', 'reopen shows GO + 4-6-3 + DP, and E + 6', re1.join('|') === 'GO|4-6-3|DP' && re2.join('|') === 'E|6', `${re1.join('|')} / ${re2.join('|')}`);
  // Cancel after editing keeps what was saved
  await open(page, 'away', 2, 3);
  await typeInto(page, '#away-at-bat-fielders', '6-3');
  await page.locator('#away-at-bat-multi-out input[value=""]').check();
  await page.click('#away-at-bat-cancel'); await closed(page, 'away');
  check('AC3', 'cancel after editing keeps GO 4-6-3 DP', (await note(page, id('away', 2, 3))) === 'GO 4-6-3 DP');

  // AC4: switching result, Clear
  await open(page, 'away', 2, 3);
  await pick(page, 'away', '1B');
  await page.click('#away-at-bat-save'); await closed(page, 'away');
  await waitNote(page, id('away', 2, 3), '1B');
  const sw = await note(page, id('away', 2, 3));
  await open(page, 'away', 2, 3);
  await pick(page, 'away', 'GO');
  const swRe = [await page.inputValue('#away-at-bat-fielders'), await multiChecked(page, 'away')];
  await page.keyboard.press('Escape'); await closed(page, 'away');
  check('AC4', 'switching GO 4-6-3 DP to 1B shows 1B; picking GO again starts blank / none', sw === '1B' && swRe.join('|') === '|', `cell="${sw}" reopen=${swRe.join('|')}`);
  // switching GO→E inside one dialog does not carry 4-6-3 into the error box
  await open(page, 'home', 6, 6);
  await pick(page, 'home', 'E');
  const carried = await page.inputValue('#home-at-bat-error-by');
  await page.keyboard.press('Escape'); await closed(page, 'home');
  check('AC4', 'switching FO 8 TP to E in the dialog starts the error box blank', carried === '', `error-by="${carried}"`);
  await open(page, 'home', 6, 6);
  await page.click('#home-at-bat-clear'); await closed(page, 'home');
  await waitNote(page, id('home', 6, 6), '');
  await open(page, 'home', 6, 6);
  await pick(page, 'home', 'FO');
  const clRe = [await page.inputValue('#home-at-bat-fielders'), await multiChecked(page, 'home')];
  await page.screenshot({ path: path.join(out, 'ac4-cleared-reopens-blank-desktop.png') });
  await page.keyboard.press('Escape'); await closed(page, 'home');
  await page.reload({ waitUntil: 'networkidle' });
  await page.waitForSelector(id('home', 6, 6));
  const clCell = await note(page, id('home', 6, 6));
  check('AC4', 'Clear on FO 8 TP: cell blank after reload, FO reopens with blank fielders / none', clCell === '' && clRe.join('|') === '|', `cell="${clCell}" reopen=${clRe.join('|')}`);

  // Viewports: grid and dialog
  for (const [name, w, h] of [['desktop', 1920, 1080], ['tablet', 1024, 768], ['mobile', 375, 667]]) {
    await page.setViewportSize({ width: w, height: h });
    await ready(page);
    await page.locator(id('home', 5, 6)).scrollIntoViewIfNeeded();
    await page.screenshot({ path: path.join(out, `${name}.png`) });
    await open(page, 'away', 4, 4);
    await page.screenshot({ path: path.join(out, `${name}-dialog.png`) });
    await page.keyboard.press('Escape');
    await closed(page, 'away');
  }

  check('green', 'no console errors / page errors / 5xx / 401 / 403', !consoleErrors.length && !pageErrors.length && !bad.length, JSON.stringify({ consoleErrors, pageErrors, bad }));
  fs.writeFileSync(path.join(out, 'browser-results.json'), JSON.stringify(results, null, 2));
  await browser.close();
  process.exit(results.every(r => r.ok) ? 0 : 1);
})().catch(e => { console.error(e); process.exit(2); });
