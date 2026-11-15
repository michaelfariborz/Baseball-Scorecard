const { chromium } = require('playwright');
(async()=>{const br=await chromium.launch({executablePath:'/usr/bin/google-chrome'});
for (const th of ['light','dark']){
 const ctx=await br.newContext({colorScheme:th});const p=await ctx.newPage();
 await p.goto('http://localhost:5180/not-found');await p.waitForTimeout(500);
 for(let i=0;i<8;i++){await p.keyboard.press('Tab');const on=await p.evaluate(()=>document.activeElement.classList.contains('theme-toggle'));if(on)break;}
 console.log(th,await p.evaluate(()=>{const s=getComputedStyle(document.activeElement);return document.activeElement.className+' '+s.outlineWidth+' '+s.outlineStyle+' '+s.outlineColor+' hdr '+getComputedStyle(document.querySelector('.app-header')).backgroundColor}));
 await p.screenshot({path:process.argv[2]+`/focus-${th}.png`,clip:{x:0,y:0,width:1280,height:90}});
 await ctx.close();}
await br.close();})();
