const test = require('node:test');
const assert = require('node:assert/strict');
const http = require('node:http');
const net = require('node:net');
const os = require('node:os');
const path = require('node:path');
const fs = require('node:fs');
const { spawn } = require('node:child_process');

function freePort() {
  return new Promise((resolve,reject)=>{const probe=net.createServer();probe.once('error',reject);probe.listen(0,'127.0.0.1',()=>{const port=probe.address().port;probe.close(()=>resolve(port));});});
}
function request(port, sourceIp, route, method='GET', body=null, token='') {
  return new Promise((resolve,reject)=>{
    const data=body===null?null:Buffer.from(JSON.stringify(body));
    const targetIp=sourceIp.startsWith('127.')?'127.0.0.1':sourceIp;
    const req=http.request({host:targetIp,port,path:route,method,localAddress:sourceIp,headers:{...(data?{'Content-Type':'application/json','Content-Length':data.length}:{}),...(token?{Authorization:`Bearer ${token}`}:{})}},res=>{
      let text='';res.setEncoding('utf8');res.on('data',chunk=>text+=chunk);res.on('end',()=>{let json={};try{json=text?JSON.parse(text):{};}catch{}resolve({status:res.statusCode,json,text,headers:res.headers,sourceIp:req.socket.localAddress});});
    });req.on('error',reject);if(data)req.write(data);req.end();
  });
}
async function waitForRelay(port, child) {
  const end=Date.now()+8000;
  while(Date.now()<end){if(child.exitCode!==null)throw new Error('relay exited before starting');try{const result=await request(port,'127.0.0.2','/health');if(result.status===200)return;}catch{}await new Promise(r=>setTimeout(r,100));}
  throw new Error('relay did not become ready');
}
function launch(port, accountsFile, webDir) {
  return spawn(process.execPath,[path.join(__dirname,'..','relay','server.js')],{env:{...process.env,PORT:String(port),ACCOUNTS_FILE:accountsFile,WEB_BUILD:webDir},stdio:['ignore','ignore','pipe']});
}
function stop(child) { return new Promise(resolve=>{if(child.exitCode!==null)return resolve();child.once('exit',resolve);child.kill('SIGTERM');setTimeout(()=>{if(child.exitCode===null)child.kill('SIGKILL');},1500).unref();}); }

test('two LAN clients authenticate, relay a co-op match, and persist equipped upgrades', { timeout:30000 }, async t=>{
  const peerIp=Object.values(os.networkInterfaces()).flat().find(address=>address&&address.family==='IPv4'&&!address.internal)?.address||'127.0.0.3';
  const folder=fs.mkdtempSync(path.join(os.tmpdir(),'rl-survival-beta-')),accountsFile=path.join(folder,'accounts.json'),webDir=path.join(folder,'web'),port=await freePort();fs.mkdirSync(webDir);fs.writeFileSync(path.join(webDir,'index.html'),'<h1>beta fixture</h1>');fs.writeFileSync(path.join(webDir,'arena.wasm.br'),Buffer.from([0x42,0x52]));let relay=launch(port,accountsFile,webDir);t.after(async()=>{await stop(relay);fs.rmSync(folder,{recursive:true,force:true});});await waitForRelay(port,relay);
  const staticPage=await request(port,'127.0.0.2','/');assert.equal(staticPage.status,200);assert.match(staticPage.text,/beta fixture/);
  const compressed=await request(port,'127.0.0.2','/arena.wasm.br');assert.equal(compressed.status,200);assert.equal(compressed.headers['content-type'],'application/wasm');assert.equal(compressed.headers['content-encoding'],'br');

  const hostAuth=await request(port,'127.0.0.2','/auth/register','POST',{username:'HostPilot',password:'LanPass-123'});
  const guestAuth=await request(port,peerIp,'/auth/register','POST',{username:'GuestPilot',password:'LanPass-456'});
  assert.equal(hostAuth.status,201);assert.equal(guestAuth.status,201);
  assert.equal(hostAuth.json.profile.guns.length,6);assert.equal(hostAuth.json.profile.blocks[0],true);
  assert.equal(hostAuth.sourceIp,'127.0.0.2');assert.equal(guestAuth.sourceIp,peerIp);assert.notEqual(hostAuth.sourceIp,guestAuth.sourceIp);
  assert.equal((await request(port,peerIp,'/join','POST',{room:'BETAV1'})).status,401);
  assert.equal((await request(port,'127.0.0.2','/auth/register','POST',{username:'HostPilot',password:'Another-123'})).status,409);
  const host=await request(port,'127.0.0.2','/join','POST',{token:hostAuth.json.token,room:'BETAV1'});
  const guest=await request(port,peerIp,'/join','POST',{token:guestAuth.json.token,room:'BETAV1'});
  assert.equal(host.status,200);assert.equal(guest.status,200);assert.equal(host.json.payload,host.json.from);assert.notEqual(guest.json.payload,guest.json.from);assert.equal(guest.json.players.length,2);

  const hostEvents=await request(port,'127.0.0.2',`/poll?id=${host.json.from}&since=${host.json.seq}`);
  assert.match(hostEvents.text,/"type":"join"/);assert.match(hostEvents.text,/GuestPilot/);
  const badOptions=await request(port,'127.0.0.2','/send','POST',{id:host.json.from,type:'start',payload:JSON.stringify({algorithm:'NotRL',difficulty:'Veteran'})});assert.equal(badOptions.status,400);
  for(const algorithm of ['QLearning','SARSA','DoubleQLearning'])for(const difficulty of ['Rookie','Standard','Veteran']){
    const option=await request(port,'127.0.0.2','/send','POST',{id:host.json.from,type:'start',payload:JSON.stringify({algorithm,difficulty})});assert.equal(option.status,204,`${algorithm}/${difficulty} should be selectable`);
  }
  const deployedModel={id:'studio-model-1',name:'Wall Breaker',algorithm:'SARSA',difficulty:'Veteran',wallFocus:1.8,breachFocus:2.2,coreFocus:1.1,exploration:.12,trainingEpisodes:600,breakthroughRate:.75,qValues:Array(24).fill(.5)};
  const start=await request(port,'127.0.0.2','/send','POST',{id:host.json.from,type:'start',payload:JSON.stringify({algorithm:'SARSA',difficulty:'Veteran',deployedModel})});assert.equal(start.status,204);
  const deniedStart=await request(port,peerIp,'/send','POST',{id:guest.json.from,type:'start',payload:''});assert.equal(deniedStart.status,403);
  const state={phase:'Build',algorithm:'SARSA',difficulty:'Veteran',hasDeployedModel:true,deployedModel,timer:35,wave:0,remaining:0,health:145,maxHealth:145,kills:0,blocks:[],raiders:[],players:[{id:host.json.from,x:0,z:0},{id:guest.json.from,x:2,z:1}]};
  assert.equal((await request(port,'127.0.0.2','/send','POST',{id:host.json.from,type:'state',payload:JSON.stringify(state)})).status,204);
  assert.equal((await request(port,'127.0.0.2','/send','POST',{id:host.json.from,type:'action',payload:JSON.stringify({action:'build',x:2,z:1,value:0})})).status,204);
  assert.equal((await request(port,'127.0.0.2','/send','POST',{id:host.json.from,type:'action',payload:JSON.stringify({action:'spawn',id:'1',x:10,z:2,value:50})})).status,204);
  assert.equal((await request(port,'127.0.0.2','/send','POST',{id:host.json.from,type:'action',payload:JSON.stringify({action:'kill',id:'1'})})).status,204);
  state.phase='Intermission';state.timer=18;state.wave=1;state.kills=1;state.raiders=[];
  await request(port,'127.0.0.2','/send','POST',{id:host.json.from,type:'state',payload:JSON.stringify(state)});
  const guestEvents=await request(port,peerIp,`/poll?id=${guest.json.from}&since=${guest.json.seq}`);
  const events=guestEvents.text.trim().split('\n').map(line=>JSON.parse(line));
  assert.ok(events.some(event=>event.type==='start'));
  const startEvents=events.filter(event=>event.type==='start');
  const selectedStart=JSON.parse(startEvents[startEvents.length-1].payload);
  assert.equal(selectedStart.algorithm,'SARSA');assert.equal(selectedStart.difficulty,'Veteran');assert.equal(selectedStart.deployedModel.name,'Wall Breaker');
  const actions=events.filter(event=>event.type==='action').map(event=>JSON.parse(event.payload));
  assert.ok(actions.some(action=>action.action==='build'));assert.ok(actions.some(action=>action.action==='spawn'));assert.ok(actions.some(action=>action.action==='kill'));
  const guestState=JSON.parse(events.find(event=>event.type==='state').payload);assert.equal(guestState.phase,'Intermission');assert.equal(guestState.algorithm,'SARSA');assert.equal(guestState.difficulty,'Veteran');assert.equal(guestState.deployedModel.name,'Wall Breaker');
  const lastEvent=events.reduce((max,event)=>Math.max(max,event.seq),guest.json.seq);
  assert.equal((await request(port,peerIp,'/send','POST',{id:guest.json.from,type:'input',payload:JSON.stringify({action:'repair',x:1,z:1})})).status,204);
  const hostInput=await request(port,'127.0.0.2',`/poll?id=${host.json.from}&since=${lastEvent}`);
  assert.ok(hostInput.text.split('\n').filter(Boolean).map(JSON.parse).some(event=>event.type==='input'&&event.from===guest.json.from));
  const shared=await request(port,peerIp,'/send','POST',{id:guest.json.from,type:'model',payload:JSON.stringify({...deployedModel,id:'guest-shared',name:'Guest Design'})});assert.equal(shared.status,204);
  const receivedModel=await request(port,'127.0.0.2',`/poll?id=${host.json.from}&since=${lastEvent}`);
  const sharedEvent=receivedModel.text.split('\n').filter(Boolean).map(JSON.parse).find(event=>event.type==='model');
  assert.ok(sharedEvent);assert.equal(JSON.parse(sharedEvent.payload).name,'Guest Design');
  await request(port,'127.0.0.2',`/leave?id=${host.json.from}`);
  const handoff=await request(port,peerIp,`/poll?id=${guest.json.from}&since=${lastEvent}`);
  assert.ok(handoff.text.split('\n').filter(Boolean).map(JSON.parse).some(event=>event.type==='host'&&event.from===guest.json.from));
  const resumedState={...state,phase:'Build',timer:35};
  assert.equal((await request(port,peerIp,'/send','POST',{id:guest.json.from,type:'state',payload:JSON.stringify(resumedState)})).status,204);

  const upgraded={username:'GuestPilot',credits:37,guns:[true,true,false],blocks:[true,true,false],perks:[true,false,false],equippedGun:1,equippedBlock:1,equippedPerk:0,equippedAgentModel:'persisted-model',agentModels:[{...deployedModel,id:'persisted-model',qValues:[5000,...Array(23).fill(.25)]}]};
  assert.equal((await request(port,peerIp,'/profile','POST',upgraded,guestAuth.json.token)).status,200);
  assert.equal(fs.readFileSync(accountsFile,'utf8').includes('LanPass-456'),false);
  await stop(relay);relay=launch(port,accountsFile,webDir);await waitForRelay(port,relay);
  const login=await request(port,peerIp,'/auth/login','POST',{username:'GuestPilot',password:'LanPass-456'});
  assert.equal(login.status,200);assert.equal(login.json.profile.credits,37);assert.deepEqual(login.json.profile.guns,[true,true,false,false,false,false]);assert.equal(login.json.profile.equippedGun,1);
  assert.equal(login.json.profile.schemaVersion,3);assert.equal(login.json.profile.agentModels.length,1);assert.equal(login.json.profile.agentModels[0].name,'Wall Breaker');assert.equal(login.json.profile.agentModels[0].qValues[0],1000);assert.equal(login.json.profile.equippedAgentModel,'persisted-model');
  const wrongPassword=await request(port,'127.0.0.2','/auth/login','POST',{username:'GuestPilot',password:'incorrect-password'});assert.equal(wrongPassword.status,401);
});
