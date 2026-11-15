// QA v2 for #80: drives Chrome against a running app (base URL in argv[3], default localhost:5181).
// Covers R3: real header NavLink clicks (Blazor enhanced navigation), not page.goto full loads.
// Usage: node qa.js <outDir> [baseUrl]
const { chromium } = require('playwright');
const OUT = process.argv[2];
const B = process.argv[3] || 'http://localhost:5181';
function lum(c){const m=c.match(/[\d.]+/g).map(Number);const f=v=>{v/=255;return v<=0.03928?v/12.92:Math.pow((v+0.055)/1.055,2.4)};return 0.2126*f(m[0])+0.7152*f(m[1])+0.0722*f(m[2]);}
function cr(a,b){const x=lum(a),y=lum(b);return +((Math.max(x,y)+0.05)/(Math.min(x,y)+0.05)).toFixed(2);}
const fails = [];
function check(name, cond, got){ if(!cond) fails.push(name+': '+JSON.stringify(got)); }
const info = p => p.evaluate(()=>({path:location.pathname,theme:document.documentElement.dataset.theme||null,bg:getComputedStyle(document.body).backgroundColor,label:document.querySelector('.theme-toggle')?.getAttribute('aria-label'),ls:localStorage.getItem('theme'),marker:window.__marker===1}));
const LIGHT_BG='rgb(250, 247, 240)', DARK_BG='rgb(15, 26, 18)';
// Click a header nav link and wait for Blazor's enhanced navigation to finish (URL change + DOM patch).
async function nav(p, text, path){
  await p.click(`nav.app-nav a:text-is("${text}")`);
  await p.waitForURL(u => new URL(u).pathname === path);
  await p.waitForTimeout(500);
}
(async()=>{
 const br = await chromium.launch({executablePath:'/usr/bin/google-chrome'});
 const res = {};
 const vps = {desktop:{width:1920,height:1080}, tablet:{width:1024,height:768}, mobile:{width:375,height:667}};

 // A. OS light, no saved choice. Toggle to dark on Home, then walk the header links.
 let ctx = await br.newContext({colorScheme:'light',viewport:vps.desktop});
 let p = await ctx.newPage();
 await p.goto(B+'/'); await p.waitForTimeout(800);
 res.a_home_initial = await info(p);
 check('A initial light', res.a_home_initial.theme===null && res.a_home_initial.bg===LIGHT_BG && res.a_home_initial.label==='Switch to dark mode', res.a_home_initial);
 await p.evaluate(()=>window.__marker=1);
 await p.screenshot({path:OUT+'/ac1-light-home-desktop.png'});
 await p.click('.theme-toggle'); await p.waitForTimeout(300);
 res.a_after_click = await info(p);
 check('A click to dark, no reload', res.a_after_click.theme==='dark' && res.a_after_click.bg===DARK_BG && res.a_after_click.label==='Switch to light mode' && res.a_after_click.marker && res.a_after_click.ls==='dark', res.a_after_click);
 await p.screenshot({path:OUT+'/ac2-dark-home-desktop.png'});
 for (const [text,path,key] of [['Rosters','/rosters','rosters'],['New game','/games/new','newgame'],['Home','/','home']]) {
   await nav(p, text, path);
   const r = res['a_nav_'+key] = await info(p);
   check('A enhanced nav to '+path+' keeps dark', r.path===path && r.theme==='dark' && r.bg===DARK_BG && r.label==='Switch to light mode' && r.marker, r);
 }
 await p.screenshot({path:OUT+'/ac3-dark-after-navlinks-desktop.png'});
 // Toggle after enhanced nav still works and survives the next nav.
 await nav(p, 'Rosters', '/rosters');
 await p.click('.theme-toggle'); await p.waitForTimeout(300);
 res.a_toggle_after_nav = await info(p);
 check('A toggle after nav to light', res.a_toggle_after_nav.theme==='light' && res.a_toggle_after_nav.bg===LIGHT_BG && res.a_toggle_after_nav.label==='Switch to dark mode' && res.a_toggle_after_nav.marker, res.a_toggle_after_nav);
 await nav(p, 'New game', '/games/new');
 res.a_light_nav = await info(p);
 check('A saved light kept on nav', res.a_light_nav.theme==='light' && res.a_light_nav.bg===LIGHT_BG && res.a_light_nav.marker, res.a_light_nav);
 // Reload keeps the choice.
 await p.click('.theme-toggle'); await p.waitForTimeout(200);
 await p.reload(); await p.waitForTimeout(600);
 res.a_reload = await info(p);
 check('A reload keeps dark', res.a_reload.theme==='dark' && res.a_reload.bg===DARK_BG && res.a_reload.label==='Switch to light mode', res.a_reload);
 await ctx.close();

 // B. OS dark, saved light. Enhanced nav must not fall back to the OS theme.
 ctx = await br.newContext({colorScheme:'dark',viewport:vps.desktop});
 await ctx.addInitScript(()=>{ if(!sessionStorage.getItem('seeded')){ localStorage.setItem('theme','light'); sessionStorage.setItem('seeded','1'); } });
 p = await ctx.newPage();
 await p.goto(B+'/'); await p.waitForTimeout(800);
 await p.evaluate(()=>window.__marker=1);
 res.b_home = await info(p);
 check('B saved light beats OS dark', res.b_home.theme==='light' && res.b_home.bg===LIGHT_BG && res.b_home.label==='Switch to dark mode', res.b_home);
 for (const [text,path,key] of [['Rosters','/rosters','rosters'],['New game','/games/new','newgame'],['Home','/','home']]) {
   await nav(p, text, path);
   const r = res['b_nav_'+key] = await info(p);
   check('B enhanced nav to '+path+' keeps light', r.path===path && r.theme==='light' && r.bg===LIGHT_BG && r.label==='Switch to dark mode' && r.marker, r);
 }
 await ctx.close();

 // C. OS dark, no saved choice: follows the OS, and nav keeps following it.
 ctx = await br.newContext({colorScheme:'dark',viewport:vps.desktop});
 p = await ctx.newPage();
 await p.goto(B+'/'); await p.waitForTimeout(800);
 res.c_os_dark = await info(p);
 check('C OS dark followed', res.c_os_dark.theme===null && res.c_os_dark.bg===DARK_BG && res.c_os_dark.label==='Switch to light mode', res.c_os_dark);
 await nav(p, 'Rosters', '/rosters');
 res.c_os_dark_nav = await info(p);
 check('C OS dark after nav', res.c_os_dark_nav.theme===null && res.c_os_dark_nav.bg===DARK_BG && res.c_os_dark_nav.label==='Switch to light mode', res.c_os_dark_nav);
 await ctx.close();

 // D. First paint: block theme.js and blazor.web.js; the head script alone must apply saved dark.
 ctx = await br.newContext({colorScheme:'light',viewport:vps.desktop});
 await ctx.addInitScript(()=>localStorage.setItem('theme','dark'));
 p = await ctx.newPage();
 await p.route(/theme\..*js|blazor\.web.*\.js/, r=>r.abort());
 await p.goto(B+'/rosters');
 res.d_firstpaint = await info(p);
 check('D first paint dark without scripts', res.d_firstpaint.theme==='dark' && res.d_firstpaint.bg===DARK_BG, res.d_firstpaint);
 await p.screenshot({path:OUT+'/ac4-saved-dark-scripts-blocked.png'});
 await ctx.close();

 // E. Contrast, focus ring, 375 width and screenshots per viewport and mode.
 for (const scheme of ['light','dark']) {
   for (const [vp,size] of Object.entries(vps)) {
     ctx = await br.newContext({colorScheme:'light',viewport:size});
     await ctx.addInitScript(s=>localStorage.setItem('theme',s), scheme);
     p = await ctx.newPage();
     await p.goto(B+'/rosters'); await p.waitForTimeout(600);
     await p.keyboard.press('Tab');
     for (let i=0;i<8 && !(await p.evaluate(()=>document.activeElement?.classList.contains('theme-toggle')));i++) await p.keyboard.press('Tab');
     const t = await p.evaluate(()=>{const b=document.querySelector('.theme-toggle'),s=getComputedStyle(b),h=getComputedStyle(document.querySelector('.app-header'));return{focused:document.activeElement===b,color:s.color,hdr:h.backgroundColor,outline:s.outlineWidth+' '+s.outlineStyle,sw:document.documentElement.scrollWidth,cw:document.documentElement.clientWidth}});
     t.contrast = cr(t.color,t.hdr);
     res[`e_${scheme}_${vp}`] = t;
     check(`E ${scheme} ${vp} AA + focus ring`, t.focused && t.contrast>=4.5 && t.outline.startsWith('3px') && t.outline.endsWith('solid'), t);
     check(`E ${scheme} ${vp} no h-scroll`, t.sw<=t.cw, t);
     await p.screenshot({path:OUT+`/ac5-${scheme}-${vp}.png`});
     await ctx.close();
   }
 }
 res.fails = fails;
 console.log(JSON.stringify(res,null,1));
 await br.close();
 process.exit(fails.length ? 1 : 0);
})();
