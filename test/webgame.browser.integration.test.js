const test = require('node:test');
const assert = require('node:assert/strict');
const http = require('node:http');
const net = require('node:net');
const os = require('node:os');
const path = require('node:path');
const fs = require('node:fs');
const { spawn } = require('node:child_process');
const { chromium } = require('playwright');

function freePort() { return new Promise((resolve, reject) => { const s = net.createServer(); s.once('error', reject); s.listen(0, '127.0.0.1', () => { const p=s.address().port; s.close(() => resolve(p)); }); }); }
function waitReady(port, child) { return new Promise((resolve, reject) => { const end=Date.now()+10000; const poll=()=>{if(child.exitCode!==null)return reject(new Error('Relay stopped before listening'));http.get(`http://127.0.0.1:${port}/health`,r=>{r.resume();if(r.statusCode===200)return resolve();setTimeout(poll,100);}).on('error',()=>Date.now()<end?setTimeout(poll,100):reject(new Error('Relay did not start')));};poll(); }); }
function stop(child) { return new Promise(resolve=>{if(child.exitCode!==null)return resolve();child.once('exit',resolve);child.kill('SIGTERM');setTimeout(()=>child.kill('SIGKILL'),1500).unref();}); }

test('two browser clients share the 3D fight, models, cameras, damage, and upgrades', { timeout:90000 }, async t => {
  const folder=fs.mkdtempSync(path.join(os.tmpdir(),'rl-survival-browser-')), port=await freePort();
  const relay=spawn(process.execPath,[path.join(__dirname,'..','relay','server.js')],{env:{...process.env,PORT:String(port),ACCOUNTS_FILE:path.join(folder,'accounts.json')},stdio:['ignore','ignore','pipe']});
  let errors=[];
  t.after(async()=>{await stop(relay);fs.rmSync(folder,{recursive:true,force:true});});
  await waitReady(port,relay);
  const browser=await chromium.launch({headless:true,args:['--no-sandbox','--enable-webgl','--use-gl=angle','--use-angle=swiftshader']});
  t.after(()=>browser.close());
  const host=await browser.newContext({viewport:{width:1440,height:1000}}), guest=await browser.newContext({viewport:{width:1440,height:1000}});
  const a=await host.newPage(), b=await guest.newPage();
  for(const [label,page] of [['host',a],['guest',b]]){page.on('pageerror',e=>errors.push(`${label}: ${e.message} ${e.stack||''}`));page.on('console',m=>{if(m.type()==='error')errors.push(`${label} console: ${m.text()} at ${JSON.stringify(m.location())}`)});}
  await Promise.all([a,b].map((page,i)=>page.addInitScript(()=>{window.addEventListener('error',e=>{window.__scriptError={message:e.message,source:e.filename,line:e.lineno,column:e.colno};});})));
  const base=`http://127.0.0.1:${port}`;
  await Promise.all([a.goto(base),b.goto(base)]);
  assert.equal(await a.locator('#server').inputValue(),base,'host relay address should default to the page LAN address');
  assert.equal(await b.locator('#server').inputValue(),base,'guest relay address should default to the page LAN address');
  await a.locator('#username').fill('HostBrowser');await a.locator('#password').fill('BrowserPass-123');await a.locator('#register').click();
  await b.locator('#username').fill('GuestBrowser');await b.locator('#password').fill('BrowserPass-456');await b.locator('#register').click();
  try { await Promise.all([a.locator('#lobby-screen').waitFor({state:'visible',timeout:8000}),b.locator('#lobby-screen').waitFor({state:'visible',timeout:8000})]); }
  catch { const diag=await Promise.all([a,b].map(p=>p.evaluate(()=>({state:document.readyState,origin:location.origin,server:document.querySelector('#server')?.value,registerDisabled:document.querySelector('#register')?.disabled,world:document.querySelector('#world')?.width,error:window.__scriptError,modules:[...document.querySelectorAll('script')].map(s=>s.src)}))));throw new Error(`Registration did not finish. diag=${JSON.stringify(diag)} host=${await a.locator('#auth-status').textContent()} guest=${await b.locator('#auth-status').textContent()} scriptErrors=${errors.join('; ')}`); }
  await a.locator('#join').click();
  await a.locator('#room-status').waitFor({state:'visible'});
  await b.locator('#join').click();
  await a.locator('#roster').getByText('GuestBrowser',{exact:false}).waitFor({timeout:8000});
  await b.locator('#roster').getByText('HostBrowser',{exact:false}).waitFor({timeout:8000});
  await a.locator('#launch').click();
  await Promise.all([a.locator('#match-screen').waitFor({state:'visible'}),b.locator('#match-screen').waitFor({state:'visible'})]);
  await a.locator('#camera-mode').click();
  assert.match(await a.locator('#camera-mode').textContent(),/First person/);
  await a.locator('#camera-mode').click();
  assert.match(await a.locator('#camera-mode').textContent(),/Third person/);
  await a.locator('[data-item="block:7"]').click();
  await a.evaluate(async()=>{const game=await import('/game.js');await game.requestBuild(12,.5,24);});
  await a.waitForFunction(async()=>{const game=await import('/game.js');return [...game.world.blocks.values()].some(block=>block.userData.type===7&&block.userData.asset);});
  await a.locator('#wave-button').click();
  await a.waitForFunction(()=>document.querySelector('#phase-kicker').textContent==='WAVE IN PROGRESS');
  await a.waitForFunction(()=>Number(document.querySelector('#raider-count').textContent)>0,{timeout:10000});
  await b.waitForFunction(()=>document.querySelector('#phase-kicker').textContent==='WAVE IN PROGRESS',{timeout:10000});
  await a.waitForFunction(async()=>{const game=await import('/game.js');return game.world.raiders.size>0&&[...game.world.players.values()].every(actor=>actor.userData.body&&actor.userData.gunMount);},{timeout:10000});
  const firstRaider=await a.evaluate(async()=>{const game=await import('/game.js');return [...game.world.raiders.keys()][0];});
  await a.evaluate(async id=>{const game=await import('/game.js');game.applyDamage(id,1000);},firstRaider);
  await a.waitForFunction(()=>Number(document.querySelector('#kill-count').textContent)>=1,null,{timeout:5000});
  await b.keyboard.down('w');await b.waitForTimeout(2100);await b.keyboard.up('w');
  await b.waitForFunction(()=>Number(document.querySelector('#player-health').textContent.split(' / ')[0])<100,{timeout:30000});
  assert.ok(Number(await b.locator('#player-health').textContent().then(text=>text.split(' / ')[0]))<100,'guest must take melee damage from a raider');
  await b.locator('#buy-heal').click();
  await b.waitForFunction(()=>Number(document.querySelector('#player-health').textContent.split(' / ')[0])>0,{timeout:5000});
  const canvas=await a.locator('#world').evaluate(el=>({width:el.width,height:el.height,cssWidth:el.clientWidth,cssHeight:el.clientHeight}));
  assert.ok(canvas.width>0&&canvas.height>0,'3D canvas must be initialized');
  assert.equal(await a.locator('#wave-title').textContent(),'Wave 1');
  assert.equal(await b.locator('#wave-title').textContent(),'Wave 1');
  assert.ok(Number(await a.locator('#raider-count').textContent())>0,'host must have live raiders');
  assert.equal(errors.length,0,errors.join('\n'));
  const screenshot=path.join(__dirname,'..','artifacts','wave1-two-browser-clients.png');fs.mkdirSync(path.dirname(screenshot),{recursive:true});
  await a.screenshot({path:screenshot,fullPage:true});
  console.log(`Browser integration screenshot: ${screenshot}`);
  await Promise.all([host.close(),guest.close()]);
});
