const { chromium } = require('playwright');
const OUT = process.argv[2];
const B = 'http://localhost:5180';
function lum(c){const m=c.match(/[\d.]+/g).map(Number);const f=v=>{v/=255;return v<=0.03928?v/12.92:Math.pow((v+0.055)/1.055,2.4)};return 0.2126*f(m[0])+0.7152*f(m[1])+0.0722*f(m[2]);}
function cr(a,b){const x=lum(a),y=lum(b);return ((Math.max(x,y)+0.05)/(Math.min(x,y)+0.05)).toFixed(2);}
(async()=>{
 const br = await chromium.launch({executablePath:'/usr/bin/google-chrome'});
 const res = {};
 const info = p => p.evaluate(()=>({theme:document.documentElement.dataset.theme||null,bg:getComputedStyle(document.body).backgroundColor,label:document.querySelector('.theme-toggle')?.getAttribute('aria-label'),text:document.querySelector('.theme-toggle')?.textContent,ls:localStorage.getItem('theme')}));
 // OS light, no saved
 let ctx = await br.newContext({colorScheme:'light',viewport:{width:1280,height:800}});
 let p = await ctx.newPage();
 await p.goto(B+'/not-found'); await p.waitForTimeout(800);
 res.light_initial = await info(p);
 await p.evaluate(()=>window.__marker=1);
 await p.screenshot({path:OUT+'/light-desktop.png'});
 await p.click('.theme-toggle'); await p.waitForTimeout(300);
 res.after_click = await info(p); res.no_reload = await p.evaluate(()=>window.__marker===1);
 await p.screenshot({path:OUT+'/dark-desktop.png'});
 // contrast + focus in dark
 await p.focus('.theme-toggle');
 res.dark_toggle = await p.evaluate(()=>{const b=document.querySelector('.theme-toggle'),s=getComputedStyle(b),h=getComputedStyle(document.querySelector('.app-header'));return{color:s.color,hdr:h.backgroundColor,outline:s.outlineWidth+' '+s.outlineStyle+' '+s.outlineColor}});
 res.dark_toggle.contrast = cr(res.dark_toggle.color,res.dark_toggle.hdr);
 // reload + second page
 await p.reload(); await p.waitForTimeout(600); res.reload = await info(p);
 await p.goto(B+'/Error'); await p.waitForTimeout(600); res.second_page = await info(p);
 // enhanced nav: click nav link to home? (DB 500) - skip; use client-side check of enhancedload sync
 // first paint: abort scripts except inline
 const p2 = await ctx.newPage();
 await p2.route(/theme\..*\.js|blazor\.web.*\.js/, r=>r.abort());
 await p2.goto(B+'/not-found'); res.firstpaint_saved_dark_scripts_blocked = await info(p2);
 await p2.screenshot({path:OUT+'/saved-dark-no-js.png'});
 // toggle back to light, check contrast
 await p.click('.theme-toggle'); await p.waitForTimeout(200); res.back_to_light = await info(p);
 await p.focus('.theme-toggle');
 res.light_toggle = await p.evaluate(()=>{const b=document.querySelector('.theme-toggle'),s=getComputedStyle(b),h=getComputedStyle(document.querySelector('.app-header'));return{color:s.color,hdr:h.backgroundColor,outline:s.outlineWidth+' '+s.outlineStyle+' '+s.outlineColor}});
 res.light_toggle.contrast = cr(res.light_toggle.color,res.light_toggle.hdr);
 await ctx.close();
 // OS dark, no saved
 ctx = await br.newContext({colorScheme:'dark'}); p = await ctx.newPage();
 await p.goto(B+'/not-found'); await p.waitForTimeout(600); res.os_dark_nosaved = await info(p);
 await p.click('.theme-toggle'); await p.waitForTimeout(200); res.os_dark_click = await info(p);
 await p.reload(); await p.waitForTimeout(500); res.os_dark_saved_light_reload = await info(p);
 await ctx.close();
 // 375 wide
 for (const scheme of ['light','dark']) {
  ctx = await br.newContext({colorScheme:scheme,viewport:{width:375,height:667}}); p = await ctx.newPage();
  for (const pg of ['/not-found','/Error']) { await p.goto(B+pg); await p.waitForTimeout(500);
   res['w375_'+scheme+pg] = await p.evaluate(()=>({sw:document.documentElement.scrollWidth,cw:document.documentElement.clientWidth})); }
  await p.goto(B+'/not-found'); await p.waitForTimeout(500);
  await p.screenshot({path:OUT+`/mobile-${scheme}.png`});
  await ctx.close();
 }
 console.log(JSON.stringify(res,null,1)); await br.close();
})();
