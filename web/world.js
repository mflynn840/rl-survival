import * as THREE from './vendor/three.module.js';
import { model, gunNames, blockNames } from './models.js';

const C={asphalt:0x3b3f3d,earth:0x74715e,grass:0x73785d,gold:0xd0ad5f,teal:0x56bca5};
const makeMat=(color,roughness=.88)=>new THREE.MeshStandardMaterial({color,roughness});

export class ThreeWorld {
  constructor(canvas,handlers){
    this.canvas=canvas;this.handlers=handlers;this.blocks=new Map();this.raiders=new Map();this.players=new Map();this.materials=new Map();this.effects=[];this.spawnDoors=[];
    this.localId='';this.cameraMode='Third';this.viewYaw=0;this.viewPitch=.05;this.dragging=false;this.heldTarget='';this.lastShotAt=0;
    this.scene=new THREE.Scene();this.scene.background=new THREE.Color(0x9aabb1);this.scene.fog=new THREE.Fog(0x9aabb1,48,118);
    this.camera=new THREE.PerspectiveCamera(47,1,.1,180);this.camera.position.set(4,40,38);this.camera.lookAt(4,0,0);
    this.renderer=new THREE.WebGLRenderer({canvas,antialias:true,powerPreference:'high-performance'});this.renderer.setPixelRatio(Math.min(devicePixelRatio||1,1.6));this.renderResolution=localStorage.getItem('rl-survival-resolution')||'native';this.renderer.shadowMap.enabled=true;this.renderer.shadowMap.type=THREE.PCFSoftShadowMap;this.renderer.outputColorSpace=THREE.SRGBColorSpace;this.renderer.toneMapping=THREE.ACESFilmicToneMapping;this.renderer.toneMappingExposure=1.05;
    this.raycaster=new THREE.Raycaster();this.pointer=new THREE.Vector2();this.ground=new THREE.Plane(new THREE.Vector3(0,1,0),0);this.clock=new THREE.Clock();
    this.lights();this.buildDistrict();this.loadTextures();this.resize();window.addEventListener('resize',()=>this.resize());canvas.addEventListener('pointerdown',e=>this.pointerDown(e));canvas.addEventListener('pointerup',()=>{this.dragging=false;this.heldTarget='';});canvas.addEventListener('pointerleave',()=>{this.dragging=false;this.heldTarget='';});canvas.addEventListener('pointermove',e=>{if(this.dragging){this.viewYaw+=e.movementX*.006;this.viewPitch=THREE.MathUtils.clamp(this.viewPitch+e.movementY*.004,-.38,.45);}});canvas.addEventListener('contextmenu',e=>e.preventDefault());this.animate();
  }
  material(color){if(!this.materials.has(color))this.materials.set(color,makeMat(color));return this.materials.get(color);}
  loadTextures(){
    const image=new Image();image.onload=()=>{
      const tile=(row,column,repeatX=1,repeatY=1)=>{const canvas=document.createElement('canvas');canvas.width=128;canvas.height=128;canvas.getContext('2d').drawImage(image,113+column*143,8+row*125,134,119,0,0,128,128);const texture=new THREE.CanvasTexture(canvas);texture.colorSpace=THREE.SRGBColorSpace;texture.wrapS=texture.wrapT=THREE.RepeatWrapping;texture.repeat.set(repeatX,repeatY);texture.anisotropy=4;return texture;};
      this.blockTextures=[tile(3,1),tile(2,0),tile(4,3),tile(4,7),tile(6,4),tile(5,2),tile(3,9),tile(4,4)];
      for(const [mesh,texture] of [[this.dirt,tile(0,0,22,17)],[this.plot,tile(0,3,12,10)],[this.asphalt,tile(5,7,35,3)]]){mesh.material=mesh.material.clone();mesh.material.map=texture;mesh.material.color.set(0xffffff);mesh.material.needsUpdate=true;}
      for(const object of this.blocks.values())this.textureBlock(object);
    };image.src='/art/blocks.png';
  }
  textureBlock(object){const texture=this.blockTextures?.[object.userData.type];if(!texture||!object.userData.asset)return;object.userData.asset.traverse(node=>{if(!node.isMesh||!['solid wall','brick course','triangular roof slope','spike plate'].some(name=>node.name.includes(name)))return;node.material=node.material.clone();node.material.map=texture;node.material.color.set(0xffffff);node.material.needsUpdate=true;});}
  mesh(geometry,color,parent=this.scene,p=[0,0,0],rotation=[0,0,0],cast=true){const m=new THREE.Mesh(geometry,typeof color==='number'?this.material(color):color);m.position.set(...p);m.rotation.set(...rotation);m.castShadow=cast;m.receiveShadow=true;parent.add(m);return m;}
  box(parent,name,p,s,color,r=[0,0,0]){const m=this.mesh(new THREE.BoxGeometry(...s),color,parent,p,r);m.name=name;return m;}
  lights(){this.scene.add(new THREE.HemisphereLight(0xd5e0dc,0x41453d,2));const sun=new THREE.DirectionalLight(0xffdfad,3.15);sun.position.set(-18,30,13);sun.castShadow=true;sun.shadow.mapSize.set(2048,2048);sun.shadow.camera.left=-45;sun.shadow.camera.right=45;sun.shadow.camera.top=50;sun.shadow.camera.bottom=-45;sun.shadow.bias=-.00035;sun.shadow.normalBias=.025;this.scene.add(sun);const fill=new THREE.DirectionalLight(0x9bb9c3,.9);fill.position.set(20,13,-22);this.scene.add(fill);const warm=new THREE.PointLight(0xffbd72,18,14,2);warm.position.set(0,5.25,-12);this.scene.add(warm);const sign=new THREE.PointLight(0xffce7c,28,27,2);sign.position.set(11.3,9,-14.5);this.scene.add(sign);}
  buildDistrict(){
    this.dirt=this.box(this.scene,'dirt lot',[0,-.18,0],[90,.36,70],0x74715e);this.plot=this.box(this.scene,'crew build plot',[0,.012,14.1],[29,.035,22],C.grass);
    for(let x=-12;x<=12;x+=3)this.box(this.scene,'plot grid',[x,.036,14.1],[.025,.012,21.6],0x929176);
    for(let z=5;z<=24;z+=3)this.box(this.scene,'plot grid',[0,.036,z],[28.8,.012,.025],0x929176);
    this.box(this.scene,'plot boundary',[0,.06,25.22],[29,.09,.17],0xc4ae77);this.box(this.scene,'plot boundary',[-14.42,.06,14.1],[.17,.09,22],0xc4ae77);this.box(this.scene,'plot boundary',[14.42,.06,14.1],[.17,.09,22],0xc4ae77);
    this.road();this.motel();this.cars();this.trees();this.core();
  }
  road(){
    this.asphalt=this.box(this.scene,'two lane asphalt',[0,.025,-3.5],[90,.05,9.2],0x3b3f3d);this.box(this.scene,'double yellow line',[0,.057,-3.47],[90,.018,.11],0xe1b941);this.box(this.scene,'double yellow line',[0,.057,-3.76],[90,.018,.045],0xe1b941);
    this.box(this.scene,'road edge',[0,.057,.98],[90,.018,.07],0xd6d0bb);this.box(this.scene,'road edge',[0,.057,-7.98],[90,.018,.07],0xd6d0bb);
    for(let x=-40;x<=40;x+=8)this.box(this.scene,'lane marker',[x,.059,-1.65],[3.1,.018,.06],0xd3cdb9);
    this.box(this.scene,'motel driveway',[0,.009,-9.95],[27,.02,3.7],0x555750);this.box(this.scene,'plot driveway',[0,.009,2.3],[23,.02,2.3],0x555750);
    for(let x=-10;x<=10;x+=2)this.box(this.scene,'crosswalk stripe',[x,.06,.82],[.82,.02,.24],0xe1dece);
    this.groundText('STOP',[0,.075,-6.9],0xe5ddcb);
  }
  roof(parent){const x=11.5,z=4.8,h=2.35,v=new Float32Array([-x,0,-z,-x,0,z,-x,h,0,x,0,-z,x,0,z,x,h,0]),g=new THREE.BufferGeometry();g.setAttribute('position',new THREE.BufferAttribute(v,3));g.setIndex([0,1,2,3,5,4,0,5,3,0,2,5,2,4,5,2,1,4]);g.computeVertexNormals();this.mesh(g,new THREE.MeshStandardMaterial({color:0x343331,roughness:.95,side:THREE.DoubleSide}),parent,[0,6.49,-18.2]);}
  motel(){
    const m=new THREE.Group();m.name='Paradise Motel';this.scene.add(m);const z=-18.2;
    this.box(m,'foundation',[0,.22,z],[23,.42,9.4],0x6a6051);this.box(m,'lower floor',[0,1.95,z],[22.5,3.1,8.9],0x86654c);this.box(m,'upper floor',[0,5.02,z],[22.5,2.95,8.9],0x71533f);this.roof(m);
    for(let x=-10.8;x<=10.8;x+=5.4){this.box(m,'wood band',[x,2.98,-13.68],[5.25,.13,.16],0x503a2e);this.window(m,x-.8,2,-13.68);this.door(m,x+1.3,1.28,-13.68,true);this.window(m,x-.8,5.04,-13.68);this.door(m,x+1.3,4.38,-13.68);this.box(m,'room plaque',[x+1.3,5.83,-13.58],[.48,.23,.055],0xb29a70);}
    this.box(m,'front canopy',[0,3.3,-12.72],[23.5,.27,2.7],0x382f27);for(let x=-10;x<=10;x+=5)this.box(m,'porch post',[x,1.7,-11.55],[.23,3.2,.23],0x4d3a29);
    this.box(m,'upper gallery',[0,3.63,-13.02],[22.5,.18,1.65],0x4d4337);this.box(m,'gallery rail',[0,4.04,-12.22],[22.3,.1,.1],0xb4aa8e);for(let x=-10;x<=10;x+=2)this.box(m,'baluster',[x,3.86,-12.22],[.065,.45,.065],0xb4aa8e);
    this.box(m,'sign board',[0,6.08,-13.69],[6.7,.66,.2],0x35271f);this.sign(m,'PARADISE MOTEL',[0,6.08,-13.56],4.9,0xffdc91);
    this.box(m,'roadside sign pole',[11.3,5.15,-17.3],[.24,10.3,.24],0x302b25);this.box(m,'roadside sign',[11.3,10.35,-17.3],[3.5,2.55,.28],0x282c2a);this.sign(m,'PARADISE\nMOTEL',[11.3,10.35,-17.13],2.5,0xffd574);
    for(let i=0;i<6;i++)this.box(m,'stair tread',[11.9,.36+i*.3,-21+i*.64],[1.9,.19,.72],0x6c6253);
  }
  window(parent,x,y,z){this.box(parent,'window trim',[x,y,z],[1.6,1.12,.13],0x302e2a);this.box(parent,'room glass',[x,y,z+.074],[1.37,.91,.04],0x59747a);this.box(parent,'mullion',[x,y,z+.1],[.045,.93,.035],0xc4b392);this.box(parent,'lit room',[x-.37,y+.08,z+.1],[.08,.13,.035],0xd5bd84);}
  door(parent,x,y,z,spawn=false){this.box(parent,'door frame',[x,y,z],[1.12,2.36,.16],0x352d27);const hinge=new THREE.Group();hinge.position.set(x-.44,y,z+.1);parent.add(hinge);this.box(hinge,'hinged room door',[.44,0,0],[.88,2.11,.07],0x61422e);this.mesh(new THREE.SphereGeometry(.055,10,8),0xd6b76d,hinge,[.72,0,.07]);if(spawn)this.spawnDoors.push({x,z,hinge,openUntil:0});}
  sign(parent,text,p,width,color){const c=document.createElement('canvas');c.width=1024;c.height=text.includes('\n')?380:220;const x=c.getContext('2d');x.fillStyle='#f6d98f';x.shadowColor='#ffcf71';x.shadowBlur=28;x.textAlign='center';x.textBaseline='middle';x.font='800 122px Georgia';text.split('\n').forEach((line,i)=>x.fillText(line,512,c.height*(i+.5)/text.split('\n').length));const t=new THREE.CanvasTexture(c);t.colorSpace=THREE.SRGBColorSpace;this.mesh(new THREE.PlaneGeometry(width,width*c.height/c.width),new THREE.MeshBasicMaterial({map:t,transparent:true,side:THREE.DoubleSide}),parent,p,[0,0,0],false);}
  groundText(text,p,color){const c=document.createElement('canvas');c.width=512;c.height=160;const x=c.getContext('2d');x.fillStyle='#ddd7c7';x.font='800 116px Arial';x.textAlign='center';x.textBaseline='middle';x.fillText(text,256,82);const t=new THREE.CanvasTexture(c);t.colorSpace=THREE.SRGBColorSpace;this.mesh(new THREE.PlaneGeometry(3.8,1.2),new THREE.MeshBasicMaterial({map:t,transparent:true,side:THREE.DoubleSide}),this.scene,p,[-Math.PI/2,0,0],false);}
  cars(){[[-11,0x3e6c78],[-5,0x943f31],[1,0xc2b99d],[7,0x52564f],[12,0x6b7780]].forEach(([x,color],i)=>{const c=new THREE.Group();c.position.set(x,.1,-10);c.rotation.y=i%2?Math.PI:0;this.scene.add(c);this.box(c,'sedan body',[0,.47,0],[3.1,.62,1.45],color);this.box(c,'sedan cabin',[.1,.96,0],[1.72,.52,1.24],color);this.box(c,'windshield',[1,.98,0],[.045,.39,1.06],0x587077);[-1,1].forEach(a=>[-1,1].forEach(b=>this.mesh(new THREE.CylinderGeometry(.27,.27,.16,12),0x17191a,c,[a*1.02,.27,b*.74],[Math.PI/2,0,0])));});}
  trees(){const greens=[0x40543a,0x4b5a3d,0x596147,0x354b38];[-32,-25,24,32].forEach((x,i)=>{const z=i%2?-8:2;this.mesh(new THREE.CylinderGeometry(.16,.27,6.7,8),0x765b3b,this.scene,[x,3.35,z]);for(let j=0;j<8;j++){const a=j*Math.PI/4;this.mesh(new THREE.BoxGeometry(.2,.09,3.7),greens[j%4],this.scene,[x+Math.sin(a)*1.1,6.35,z+Math.cos(a)*1.1],[-.18,a,0]);}});for(let side of [-1,1])for(let i=-4;i<=4;i++){const x=side*31+i*2.7,z=side<0?-25:27;this.mesh(new THREE.CylinderGeometry(.28,.48,5.8,7),0x4d3827,this.scene,[x,2.9,z]);for(let layer=0;layer<3;layer++)this.mesh(new THREE.ConeGeometry(3.1-layer*.6,3.1,7),greens[(i+layer+8)%4],this.scene,[x,5.3+layer*1.35,z]);}for(let i=0;i<17;i++){const x=-25+i*3.1;this.mesh(new THREE.SphereGeometry(1,8,6),greens[i%4],this.scene,[x,.45,-6.35],[0,0,0],false).scale.set(1.35,.7,.85);}}
  core(){const g=new THREE.Group();g.position.set(0,0,10.25);this.scene.add(g);this.coreMesh=this.box(g,'crew reactor',[0,.95,0],[1.55,1.9,1.55],0x388a78);this.box(g,'reactor cap',[0,1.97,0],[1.92,.16,1.92],0x78cbb4);this.mesh(new THREE.OctahedronGeometry(.48,0),0xb1ead0,g,[0,2.75,0]);for(let i=0;i<4;i++){const a=i*Math.PI/2;this.box(g,'core brace',[Math.sin(a)*1.1,1.05,Math.cos(a)*1.1],[.17,2.35,.17],0x4d685e,[0,a,0]);}this.coreGroup=g;}
  humanoid(kind,id,tint){
    const root=new THREE.Group();root.userData[kind==='enemy'?'raiderId':'playerId']=id;root.userData.kind=kind;root.userData.phase=Math.random()*4;root.userData.moving=0;
    const fallback=this.mesh(new THREE.CapsuleGeometry(.34,1.25,5,10),kind==='enemy'?0x27272a:tint,root,[0,1.05,0]);fallback.name='Loading character';
    this.makeHealthBar(root,kind==='enemy'?0xbd4d3e:0x78c7a4);
    model(kind==='enemy'?'Raider':'Survivor').then(asset=>{
      root.remove(fallback);root.add(asset);const find=name=>asset.getObjectByName(name);
      root.userData.body=find('Body')||asset;root.userData.limbs=[find('LeftArm'),find('RightArm'),find('LeftLeg'),find('RightLeg')];
      root.userData.gunMount=find('GunMount');root.userData.meleeMount=find('MeleeMount');
      if(kind==='enemy')asset.traverse(node=>{if(node.name.includes('knife'))node.visible=root.userData.weapon!=='fists';});
      if(kind==='player')this.equipGun(root,root.userData.gunIndex||0);
    }).catch(error=>console.error('Character model failed to load',error));
    return root;
  }
  makeHealthBar(root,color){const holder=new THREE.Group();holder.name='HealthBar';holder.position.y=2.43;root.add(holder);const back=this.mesh(new THREE.PlaneGeometry(.9,.11),new THREE.MeshBasicMaterial({color:0x201c1a,depthTest:false,side:THREE.DoubleSide}),holder,[0,0,0],[0,0,0],false);const fill=this.mesh(new THREE.PlaneGeometry(.86,.075),new THREE.MeshBasicMaterial({color,depthTest:false,side:THREE.DoubleSide}),holder,[0,0,.002],[0,0,0],false);back.renderOrder=2;fill.renderOrder=3;root.userData.healthBar={holder,fill};}
  setHealth(root,hp,maxHp){const bar=root.userData.healthBar;if(!bar)return;const fraction=THREE.MathUtils.clamp(hp/Math.max(1,maxHp),0,1);bar.fill.scale.x=fraction;bar.fill.position.x=-(1-fraction)*.43;bar.fill.material.color.setHex(fraction<.3?0xe46548:root.userData.kind==='enemy'?0xbd4d3e:0x78c7a4);bar.holder.visible=fraction<1||root.userData.kind==='enemy';}
  equipGun(actor,index){actor.userData.gunIndex=index;const mount=actor.userData.gunMount;if(!mount)return;mount.clear();model(gunNames[index]||gunNames[0]).then(asset=>{if(actor.userData.gunIndex===index){mount.add(asset);actor.userData.muzzle=asset.getObjectByName('Muzzle');}}).catch(error=>console.error('Gun model failed to load',error));}
  blockColors=[0x887153,0x877f6d,0x8c969c,0x52717c,0x55a79b,0x8c765d,0x715a43,0x66696b];
  makeBlock(b){const root=new THREE.Group();root.position.set(b.x,b.y??.5,b.z);root.userData.block=true;root.userData.type=b.type;this.scene.add(root);const fallback=this.mesh(new THREE.BoxGeometry(.95,1,.95),this.blockColors[b.type]||this.blockColors[0],root);root.userData.fallback=fallback;model(blockNames[b.type]||blockNames[0]).then(asset=>{root.remove(fallback);root.add(asset);root.userData.asset=asset;this.textureBlock(root);}).catch(error=>console.error('Block model failed to load',error));this.blocks.set(`${b.x}:${b.y??.5}:${b.z}`,root);return root;}
  updateWorld(state,localId){
    this.localId=localId;
    const blocks=new Set();
    for(const b of state.blocks||[]){
      const id=`${b.x}:${b.y??.5}:${b.z}`;blocks.add(id);let object=this.blocks.get(id);if(!object)object=this.makeBlock(b);
      object.position.y=b.y??.5;
      if(object.userData.hp!==b.hp){object.userData.hp=b.hp;const wear=THREE.MathUtils.clamp(1-b.hp/Math.max(1,b.maxHp),0,1);object.traverse(node=>{if(node.isMesh&&node.material?.color)node.material.color.setHex(this.blockColors[b.type]||this.blockColors[0]).lerp(new THREE.Color(0x7c2d24),wear*.62);});}
    }
    for(const [id,object] of this.blocks)if(!blocks.has(id)){this.scene.remove(object);this.blocks.delete(id);}
    const enemies=new Set();
    for(const raider of state.raiders||[]){
      enemies.add(raider.id);let actor=this.raiders.get(raider.id);
      if(!actor){actor=this.humanoid('enemy',raider.id,0x3a3432);actor.scale.setScalar(.96);actor.position.set(raider.x,0,raider.z);this.scene.add(actor);this.raiders.set(raider.id,actor);}
      const target=new THREE.Vector3(raider.x,0,raider.z),delta=target.clone().sub(actor.position);actor.userData.moving=Math.min(1,delta.length()*2);
      if(delta.lengthSq()>.0001)actor.rotation.y=Math.atan2(delta.x,delta.z);
      actor.position.lerp(target,.42);this.setHealth(actor,raider.hp,raider.maxHp||raider.hp);actor.userData.weapon=raider.weapon||'fists';
      if(actor.userData.meleeMount)actor.userData.meleeMount.traverse(node=>{if(node.name.includes('knife'))node.visible=actor.userData.weapon!=='fists';});
    }
    for(const [id,actor] of this.raiders)if(!enemies.has(id)){this.scene.remove(actor);this.raiders.delete(id);}
    const players=new Set();
    for(const player of state.players||[]){
      players.add(player.id);let actor=this.players.get(player.id);
      if(!actor){actor=this.humanoid('player',player.id,0x6d5140);actor.position.set(player.x,0,player.z);this.scene.add(actor);this.players.set(player.id,actor);}
      const target=new THREE.Vector3(player.x,0,player.z),delta=target.clone().sub(actor.position);actor.userData.moving=Math.min(1,delta.length()*2);
      if(player.id===localId)actor.rotation.y=Math.PI+this.viewYaw;
      else if(delta.lengthSq()>.001)actor.rotation.y=Math.atan2(delta.x,delta.z);
      actor.position.lerp(target,player.id===localId?.85:.45);this.setHealth(actor,player.hp??100,player.maxHp??100);
      if(actor.userData.gunIndex!==(player.gun??0))this.equipGun(actor,player.gun??0);
    }
    for(const [id,actor] of this.players)if(!players.has(id)){this.scene.remove(actor);this.players.delete(id);}
    this.coreMesh.material.emissive.setHex(state.health<40?0x481c13:0x12362e);
  }
  setResolution(mode){this.renderResolution=mode;localStorage.setItem('rl-survival-resolution',mode);this.resize();}
  resize(){const r=this.canvas.getBoundingClientRect();const presets={720:[1280,720],1080:[1920,1080],1440:[2560,1440],2160:[3840,2160]};let ratio=Math.min(devicePixelRatio||1,1.6);if(this.renderResolution!=='native'){const [width,height]=presets[this.renderResolution]||presets[1080];ratio=Math.min(width/Math.max(1,r.width),height/Math.max(1,r.height),4);}this.renderer.setPixelRatio(ratio);this.renderer.setSize(r.width,r.height,false);this.camera.aspect=r.width/Math.max(1,r.height);this.camera.updateProjectionMatrix();}
  pointerDown(event){
    if(event.button===2){this.dragging=true;return;}
    if(event.button!==0||!this.handlers.canInteract())return;
    try {
      const rect=this.canvas.getBoundingClientRect();if(!rect.width||!rect.height)return;this.pointer.x=(event.clientX-rect.left)/rect.width*2-1;this.pointer.y=-(event.clientY-rect.top)/rect.height*2+1;this.raycaster.setFromCamera(this.pointer,this.camera);
      if(['Build','Intermission'].includes(this.handlers.phase())){
        const hits=this.raycaster.intersectObjects([...this.blocks.values()],true);
        if(hits.length){const hit=hits[0];if(!hit.face)return;const normal=hit.face.normal.clone().transformDirection(hit.object.matrixWorld),point=hit.point.clone().addScaledVector(normal,.51),x=Math.round(point.x),y=Math.round(point.y-.5)+.5,z=Math.round(point.z);if(x>=-13&&x<=13&&z>=4&&z<=24&&y<=8.5)this.handlers.build(x,y,z);return;}
        const point=new THREE.Vector3();if(this.raycaster.ray.intersectPlane(this.ground,point)&&point.x>=-13&&point.x<=13&&point.z>=4&&point.z<=24)this.handlers.build(Math.round(point.x),.5,Math.round(point.z));
      }
      if(this.handlers.phase()==='Wave'){
        for(const hit of this.raycaster.intersectObjects([...this.raiders.values()],true)){
          let object=hit.object;while(object&&!object.userData.raiderId)object=object.parent;
          if(object?.userData.raiderId){this.heldTarget=object.userData.raiderId;this.handlers.shoot(this.heldTarget);return;}
        }
      }
    } catch(error) { console.error('World click was ignored safely',error); }
  }
  setCameraMode(mode){this.cameraMode=mode;this.camera.fov=mode==='First'?68:mode==='Third'?57:47;this.camera.updateProjectionMatrix();return mode;}
  openDoor(index){const door=this.spawnDoors[index];if(door)door.openUntil=this.clock.elapsedTime+1.3;}
  shot(data){
    const shooter=this.players.get(data.from),target=this.raiders.get(String(data.id));if(!shooter||!target)return;
    shooter.rotation.y=Math.atan2(target.position.x-shooter.position.x,target.position.z-shooter.position.z);
    const origin=shooter.userData.muzzle?.getWorldPosition(new THREE.Vector3())||shooter.position.clone().add(new THREE.Vector3(0,1.3,-.6));
    const end=target.position.clone().add(new THREE.Vector3(0,1.35,0));
    const projectile=this.mesh(new THREE.SphereGeometry(data.gun===4?.18:.055,8,6),new THREE.MeshBasicMaterial({color:data.gun===4?0xeb9c3b:0xffd393}),this.scene,origin.toArray(),[0,0,0],false);
    this.effects.push({object:projectile,kind:'bullet',from:origin,to:end,age:0,duration:data.gun===4?.35:.12});
    const flash=this.mesh(new THREE.SphereGeometry(.24,10,8),new THREE.MeshBasicMaterial({color:0xffd27a,transparent:true,opacity:.95}),this.scene,origin.toArray(),[0,0,0],false);
    this.effects.push({object:flash,kind:'flash',age:0,duration:.1});
    shooter.userData.recoilUntil=this.clock.elapsedTime+.16;
  }
  blood(position,count=9){for(let index=0;index<count;index++){const object=this.mesh(new THREE.SphereGeometry(.025+Math.random()*.035,5,4),new THREE.MeshBasicMaterial({color:index%3?0x871c14:0xb63221,transparent:true,opacity:.9}),this.scene,position.toArray(),[0,0,0],false);this.effects.push({object,kind:'blood',age:0,duration:.38+Math.random()*.3,velocity:new THREE.Vector3((Math.random()-.5)*3,Math.random()*2.5,(Math.random()-.5)*3)});}}
  melee(data){const player=this.players.get(data.id),raider=this.raiders.get(String(data.from));if(player){this.blood(player.position.clone().add(new THREE.Vector3(0,1.2,0)),6);player.userData.hurtUntil=this.clock.elapsedTime+.25;}if(raider)raider.userData.attackUntil=this.clock.elapsedTime+.4;}
  animate(){
    requestAnimationFrame(()=>this.animate());const dt=Math.min(this.clock.getDelta(),.05),time=this.clock.elapsedTime;
    if(this.heldTarget&&this.handlers.phase()==='Wave'&&this.handlers.canInteract())this.handlers.shoot(this.heldTarget);
    const local=this.players.get(this.localId);
    if(local&&this.cameraMode!=='Tactical'){
      const forward=new THREE.Vector3(Math.sin(this.viewYaw),0,-Math.cos(this.viewYaw));
      const head=local.position.clone().add(new THREE.Vector3(0,1.72,0));
      const desired=this.cameraMode==='First'?head.clone().addScaledVector(forward,.15):local.position.clone().add(new THREE.Vector3(0,3.0,0)).addScaledVector(forward,-4.8);
      const look=head.clone().addScaledVector(forward,this.cameraMode==='First'?10:6).add(new THREE.Vector3(0,-this.viewPitch*8,0));
      this.camera.position.lerp(desired,.12);this.camera.lookAt(look);
    }else if(this.cameraMode==='Tactical'){this.camera.position.lerp(new THREE.Vector3(4,40,38),.12);this.camera.lookAt(4,0,0);}
    for(const door of this.spawnDoors)door.hinge.rotation.y=THREE.MathUtils.lerp(door.hinge.rotation.y,time<door.openUntil?-1.25:0,.16);
    for(const actor of [...this.players.values(),...this.raiders.values()]){
      const moving=actor.userData.moving||0;actor.userData.phase+=dt*(5+moving*8);const gait=Math.sin(actor.userData.phase)*.5*moving;
      const [leftArm,rightArm,leftLeg,rightLeg]=actor.userData.limbs||[];const enemy=actor.userData.kind==='enemy';
      if(leftArm)leftArm.rotation.x=(enemy?0:-.9)-gait*.5;
      if(rightArm)rightArm.rotation.x=time<(actor.userData.attackUntil||0)?-1.7:(enemy?0:-.9)+gait*.5-(time<(actor.userData.recoilUntil||0)?.25:0);
      if(leftLeg)leftLeg.rotation.x=gait;if(rightLeg)rightLeg.rotation.x=-gait;
      if(actor.userData.body)actor.userData.body.position.y=Math.abs(Math.sin(actor.userData.phase*2))*.035*moving+Math.sin(time*2+actor.userData.phase)*.006;
      const bar=actor.userData.healthBar;if(bar)bar.holder.quaternion.copy(this.camera.quaternion);
    }
    for(let index=this.effects.length-1;index>=0;index--){const effect=this.effects[index];effect.age+=dt;const life=effect.age/effect.duration;
      if(effect.kind==='bullet')effect.object.position.lerpVectors(effect.from,effect.to,Math.min(1,life));
      else if(effect.kind==='blood'){effect.object.position.addScaledVector(effect.velocity,dt);effect.velocity.y-=dt*6;effect.object.material.opacity=Math.max(0,1-life);}
      else{effect.object.scale.setScalar(Math.max(.01,1-life));effect.object.material.opacity=Math.max(0,1-life);}
      if(life>=1){if(effect.kind==='bullet')this.blood(effect.to,effect.object.geometry.parameters.radius>.1?16:9);this.scene.remove(effect.object);this.effects.splice(index,1);}
    }
    this.coreGroup.rotation.y=Math.sin(time*.45)*.045;this.renderer.render(this.scene,this.camera);
  }
}
