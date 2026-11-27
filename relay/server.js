const http = require('node:http');
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');

const port = Number(process.env.PORT || 8787);
const buildDir = path.resolve(process.env.WEB_BUILD || path.join(__dirname, '..', 'web'));
const accountsFile = path.resolve(process.env.ACCOUNTS_FILE || path.join(__dirname, 'data', 'accounts.json'));
const accounts = new Map();
const sessions = new Map();
const rooms = new Map();
const clients = new Map();
let nextSeq = 1;
const mime = { '.html':'text/html; charset=utf-8', '.js':'text/javascript; charset=utf-8', '.wasm':'application/wasm', '.data':'application/octet-stream', '.json':'application/json', '.css':'text/css; charset=utf-8', '.png':'image/png', '.jpg':'image/jpeg', '.svg':'image/svg+xml', '.ico':'image/x-icon' };
const profileSizes = { guns:6, blocks:8, perks:6 };
const algorithms = ['QLearning','SARSA','DoubleQLearning'];
const difficulties = ['Rookie','Standard','Veteran'];

function loadAccounts() {
  fs.mkdirSync(path.dirname(accountsFile), { recursive:true });
  if (!fs.existsSync(accountsFile)) return;
  try {
    for (const account of JSON.parse(fs.readFileSync(accountsFile, 'utf8'))) {
      account.profile=normalizeProfile(account.profile,account.username);accounts.set(account.key,account);
    }
    saveAccounts();
  }
  catch (error) { console.error(`Could not read account store ${accountsFile}: ${error.message}`); }
}
function saveAccounts() {
  const temporary = `${accountsFile}.${process.pid}.tmp`;
  fs.writeFileSync(temporary, JSON.stringify([...accounts.values()], null, 2), { mode:0o600 });
  fs.renameSync(temporary, accountsFile);
  try { fs.chmodSync(accountsFile, 0o600); } catch {}
}
function defaultProfile(username) { return normalizeProfile({credits:3000},username); }
function publicProfile(account) { return { ...account.profile, username:account.username }; }
function respond(res, code, body, type='application/json; charset=utf-8') {
  res.writeHead(code, { 'Content-Type':type, 'Access-Control-Allow-Origin':'*', 'Cache-Control':'no-store' }); res.end(body);
}
function publish(room, event) { event.seq=nextSeq++; room.events.push(event); if(room.events.length>1600)room.events.splice(0,600); }
function parseBody(req) { return new Promise((resolve,reject)=>{let data='';req.on('data',c=>{data+=c;if(data.length>128*1024){reject(new Error('request body too large'));req.destroy();}});req.on('end',()=>{try{resolve(JSON.parse(data||'{}'));}catch(e){reject(e);}});}); }
function getAccount(req) {
  const token=(req.headers.authorization||'').replace(/^Bearer\s+/i,'');
  const key=sessions.get(token); return key ? accounts.get(key) : null;
}
function normalizeProfile(input, username) {
  input=input||{};
  const flags=(items,count,baseOwned)=>{const result=Array.from({length:count},(_,index)=>Array.isArray(items)&&items[index]===true);if(baseOwned)result[0]=true;return result;};
  const guns=flags(input.guns,profileSizes.guns,true),blocks=flags(input.blocks,profileSizes.blocks,true),perks=flags(input.perks,profileSizes.perks,false);
  const equip=(value,items)=>Number.isInteger(value)&&value>=0&&value<items.length&&items[value]?value:0;
  const models=Array.isArray(input.agentModels)?input.agentModels.slice(-8).map(normalizeAgentModel).filter(Boolean):[];
  const equippedAgentModel=models.some(model=>model.id===input.equippedAgentModel)?input.equippedAgentModel:'';
  return {
    schemaVersion:3,username,
    credits:Number.isFinite(input.credits)?Math.max(0,Math.min(10000000,Math.floor(input.credits))):3000,
    guns,blocks,perks,
    equippedGun:equip(input.equippedGun,guns),
    equippedBlock:equip(input.equippedBlock,blocks),
    equippedPerk:equip(input.equippedPerk,perks),
    agentModels:models,equippedAgentModel
  };
}
function normalizeAgentModel(input) {
  if(!input||typeof input!=='object')return null;
  const id=String(input.id||'').replace(/[^A-Za-z0-9_-]/g,'').slice(0,64);
  if(!id)return null;
  const source=Array.isArray(input.qValues)?input.qValues:[];
  const qValues=Array.from({length:24},(_,index)=>Number.isFinite(source[index])?Math.max(-1000,Math.min(1000,source[index])):0);
  const finite=(value,fallback,min,max)=>Number.isFinite(value)?Math.max(min,Math.min(max,value)):fallback;
  return {
    id,name:String(input.name||'Custom Raider').trim().slice(0,24)||'Custom Raider',
    algorithm:algorithms.includes(input.algorithm)?input.algorithm:'QLearning',
    difficulty:difficulties.includes(input.difficulty)?input.difficulty:'Standard',
    wallFocus:finite(input.wallFocus,1,.2,3),breachFocus:finite(input.breachFocus,1,.2,3),coreFocus:finite(input.coreFocus,1,.2,3),
    exploration:finite(input.exploration,.18,.02,.6),trainingEpisodes:Number.isInteger(input.trainingEpisodes)?Math.max(0,Math.min(1000000,input.trainingEpisodes)):0,
    breakthroughRate:finite(input.breakthroughRate,0,0,1),qValues
  };
}
function sendJson(req, res, code, object) { return respond(res,code,JSON.stringify(object)); }
function removePlayer(room,id) {
  const player=room.players.get(id);if(!player)return;
  room.players.delete(id);clients.delete(id);publish(room,{type:'leave',from:id,name:player.name,payload:''});
  if(room.host===id){room.host=room.players.keys().next().value||'';if(room.host){const next=room.players.get(room.host);publish(room,{type:'host',from:room.host,name:next.name,payload:room.host});}}
  if(room.players.size===0)rooms.delete(room.code);
}

loadAccounts();
const server = http.createServer(async (req,res)=>{
  const url = new URL(req.url, `http://${req.headers.host || 'localhost'}`);
  if(req.method==='OPTIONS'){res.writeHead(204,{'Access-Control-Allow-Origin':'*','Access-Control-Allow-Methods':'GET,POST,OPTIONS','Access-Control-Allow-Headers':'Content-Type,Authorization'});return res.end();}
  if(url.pathname==='/health')return sendJson(req,res,200,{ok:true,rooms:rooms.size,accounts:accounts.size});

  if(url.pathname==='/auth/register'&&req.method==='POST'){
    try {
      const body=await parseBody(req), username=String(body.username||'').trim(), key=username.toLowerCase(), password=String(body.password||'');
      if(!/^[A-Za-z0-9_-]{3,20}$/.test(username))return sendJson(req,res,400,{error:'Username must be 3–20 letters, numbers, _ or -.'});
      if(password.length<8||password.length>128)return sendJson(req,res,400,{error:'Password must be 8–128 characters.'});
      if(accounts.has(key))return sendJson(req,res,409,{error:'That username is already registered.'});
      const salt=crypto.randomBytes(16).toString('hex'), passwordHash=crypto.scryptSync(password,salt,64).toString('hex');
      const account={key,username,salt,passwordHash,profile:defaultProfile(username)};accounts.set(key,account);saveAccounts();
      const token=crypto.randomUUID();sessions.set(token,key);return sendJson(req,res,201,{token,profile:publicProfile(account)});
    } catch(error) { return sendJson(req,res,400,{error:error.message}); }
  }
  if(url.pathname==='/auth/login'&&req.method==='POST'){
    try {
      const body=await parseBody(req), key=String(body.username||'').trim().toLowerCase(), password=String(body.password||''), account=accounts.get(key);
      if(!account)return sendJson(req,res,401,{error:'Invalid username or password.'});
      const supplied=crypto.scryptSync(password,account.salt,64), expected=Buffer.from(account.passwordHash,'hex');
      if(supplied.length!==expected.length||!crypto.timingSafeEqual(supplied,expected))return sendJson(req,res,401,{error:'Invalid username or password.'});
      const token=crypto.randomUUID();sessions.set(token,key);return sendJson(req,res,200,{token,profile:publicProfile(account)});
    } catch(error) { return sendJson(req,res,400,{error:error.message}); }
  }
  if(url.pathname==='/auth/logout'&&req.method==='POST'){
    const token=(req.headers.authorization||'').replace(/^Bearer\s+/i,'');sessions.delete(token);return sendJson(req,res,200,{ok:true});
  }
  if(url.pathname==='/profile'&&req.method==='GET'){
    const account=getAccount(req);if(!account)return sendJson(req,res,401,{error:'Please log in again.'});return sendJson(req,res,200,publicProfile(account));
  }
  if(url.pathname==='/profile'&&req.method==='POST'){
    try {const account=getAccount(req);if(!account)return sendJson(req,res,401,{error:'Please log in again.'});const body=await parseBody(req);account.profile=normalizeProfile(body,account.username);saveAccounts();return sendJson(req,res,200,publicProfile(account));}
    catch(error){return sendJson(req,res,400,{error:error.message});}
  }

  if(url.pathname==='/join'&&req.method==='POST'){
    try {
      const body=await parseBody(req), token=String(body.token||''), key=sessions.get(token), account=key&&accounts.get(key);
      if(!account)return sendJson(req,res,401,{error:'Log in before joining a lobby.'});
      const code=String(body.room||'FORT').trim().toUpperCase().slice(0,12), name=account.username;
      if(!/^[A-Z0-9_-]{1,12}$/.test(code))return sendJson(req,res,400,{error:'Use letters, numbers, _ or - for the room code.'});
      let room=rooms.get(code);if(!room){room={code,host:'',players:new Map(),events:[],state:null,options:{algorithm:'QLearning',difficulty:'Standard'}};rooms.set(code,room);}
      if(room.players.size>=4)return sendJson(req,res,409,{error:'This lobby is full (4 players max).'});
      if([...room.players.values()].some(p=>p.key===key))return sendJson(req,res,409,{error:'This account is already in that lobby.'});
      const id=crypto.randomUUID();if(!room.host)room.host=id;
      room.players.set(id,{id,name,key,lastSeen:Date.now()});clients.set(id,code);publish(room,{type:'join',from:id,name,payload:''});
      return sendJson(req,res,200,{seq:room.events.at(-1).seq,type:'welcome',from:id,name,payload:room.host,players:[...room.players.values()].map(p=>({id:p.id,name:p.name}))});
    } catch(error) { return sendJson(req,res,400,{error:error.message}); }
  }
  if(url.pathname==='/poll'&&req.method==='GET'){
    const id=url.searchParams.get('id'),code=clients.get(id),room=rooms.get(code);if(!room)return sendJson(req,res,404,{error:'Lobby not found'});room.players.get(id).lastSeen=Date.now();
    const since=Number(url.searchParams.get('since')||0),events=room.events.filter(e=>e.seq>since);if(room.state&&room.state.seq>since)events.push(room.state);events.sort((a,b)=>a.seq-b.seq);
    return respond(res,200,events.map(e=>JSON.stringify(e)).join('\n')+(events.length?'\n':''),'application/x-ndjson; charset=utf-8');
  }
  if(url.pathname==='/send'&&req.method==='POST'){
    try {
      const message=await parseBody(req),code=clients.get(message.id),room=rooms.get(code);if(!room)return sendJson(req,res,404,{error:'Lobby not found'});
      const player=room.players.get(message.id),type=String(message.type||'');player.lastSeen=Date.now();
      if(!['start','state','action','input','move','lobby','model'].includes(type))return sendJson(req,res,400,{error:'Unknown event type'});
      if(['start','state','action','lobby'].includes(type)&&message.id!==room.host)return sendJson(req,res,403,{error:'Only the host can publish this event.'});
      const event={type,from:message.id,name:player.name,payload:String(message.payload||'')};if(event.payload.length>256*1024)return sendJson(req,res,413,{error:'Event payload too large.'});
      if(type==='start'){
        let options;try{options=JSON.parse(event.payload);}catch{return sendJson(req,res,400,{error:'Start options must be valid JSON.'});}
        if(!algorithms.includes(options.algorithm)||!difficulties.includes(options.difficulty))return sendJson(req,res,400,{error:'Choose a supported learning algorithm and difficulty.'});room.options=options;
      }
      if(type==='state'){event.seq=nextSeq++;room.state=event;}else publish(room,event);return respond(res,204,'');
    } catch(error) { return sendJson(req,res,400,{error:error.message}); }
  }
  if(url.pathname==='/leave'&&req.method==='GET'){
    const id=url.searchParams.get('id'),code=clients.get(id),room=rooms.get(code);if(room)removePlayer(room,id);
    return sendJson(req,res,200,{ok:true});
  }

  let file=path.resolve(buildDir,'.'+decodeURIComponent(url.pathname));
  if(url.pathname==='/')file=path.join(buildDir,'index.html');
  if(!file.startsWith(buildDir+path.sep)&&file!==buildDir)return respond(res,403,'Forbidden','text/plain');
  if(!fs.existsSync(file)||!fs.statSync(file).isFile())file=path.join(buildDir,'index.html');
  if(!fs.existsSync(file))return respond(res,404,'Build WebGL into web/ and reload. See README.md.','text/plain; charset=utf-8');
  const ext=path.extname(file),compressed=ext==='.br'||ext==='.gz',sourceExt=compressed?path.extname(file.slice(0,-ext.length)):ext;
  const headers={'Content-Type':mime[sourceExt]||'application/octet-stream','Cache-Control':sourceExt==='.wasm'?'public, max-age=3600':'no-cache'};
  if(compressed){headers['Content-Encoding']=ext==='.br'?'br':'gzip';headers.Vary='Accept-Encoding';}
  res.writeHead(200,headers);fs.createReadStream(file).pipe(res);
});

const expiry=setInterval(()=>{const cutoff=Date.now()-60000;for(const room of [...rooms.values()])for(const player of [...room.players.values()])if(player.lastSeen<cutoff)removePlayer(room,player.id);},10000);expiry.unref();

server.listen(port,'0.0.0.0',()=>console.log(`RL Survival beta relay listening on 0.0.0.0:${port}; WebGL root: ${buildDir}`));
