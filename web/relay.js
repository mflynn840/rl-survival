export class LanRelay {
  constructor(onEvent){this.onEvent=onEvent;this.token='';this.id='';this.hostId='';this.players=[];this.sequence=0;this.polling=false;this.connected=false;this.room='';this.base='';}
  async request(path,method='GET',body=null){const response=await fetch(this.base+path,{method,headers:body?{'Content-Type':'application/json'}:{},body:body?JSON.stringify(body):undefined});const text=await response.text();let data={};try{data=text?JSON.parse(text):{};}catch{}if(!response.ok)throw new Error(data.error||data.message||text||`Relay request failed (${response.status})`);return data;}
  async authenticate(username,password,create){this.base=document.querySelector('#server').value.trim().replace(/\/$/,'')||location.origin;const data=await this.request(create?'/auth/register':'/auth/login','POST',{username,password});this.token=data.token;return data.profile;}
  async join(room){if(this.connected)return;const result=await this.request('/join','POST',{token:this.token,room});this.id=result.from;this.hostId=result.payload;this.sequence=result.seq;this.players=result.players||[];this.room=room;this.connected=true;this.onEvent({type:'joined',host:this.isHost,room,players:this.players});this.pollLoop();}
  get isHost(){return this.id===this.hostId;}
  async send(type,payload){if(!this.connected)return;await this.request('/send','POST',{id:this.id,type,payload:typeof payload==='string'?payload:JSON.stringify(payload)});}
  async leave(){if(!this.connected)return;const id=this.id;this.connected=false;this.id='';this.hostId='';this.players=[];try{await this.request(`/leave?id=${encodeURIComponent(id)}`);}catch{}this.onEvent({type:'left'});}
  async pollLoop(){if(!this.connected||this.polling)return;this.polling=true;try{const response=await fetch(`${this.base}/poll?id=${encodeURIComponent(this.id)}&since=${this.sequence}`);if(!response.ok)throw new Error('The relay disconnected.');const text=await response.text();for(const line of text.split('\n')){if(!line.trim())continue;const event=JSON.parse(line);if(event.seq<=this.sequence)continue;this.sequence=event.seq;this.handle(event);}}catch(error){if(this.connected)this.onEvent({type:'networkError',message:error.message});}finally{this.polling=false;if(this.connected)setTimeout(()=>this.pollLoop(),180);}}
  handle(event){if(event.type==='join'){if(!this.players.some(p=>p.id===event.from))this.players.push({id:event.from,name:event.name});}
    else if(event.type==='leave'){this.players=this.players.filter(p=>p.id!==event.from);}
    else if(event.type==='host'){this.hostId=event.from;}
    else if(event.type==='welcome'&&event.players)this.players=event.players;
    this.onEvent({...event,players:this.players,host:this.isHost});}
}
