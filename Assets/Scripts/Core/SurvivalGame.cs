using System;
using System.Collections.Generic;
using UnityEngine;

namespace RLSurvival
{
    /// <summary>Composition root and match-level API. Feature behavior lives in the focused services beside this file.</summary>
    public sealed class SurvivalGame : MonoBehaviour
    {
        readonly Dictionary<string,Vector3> playerPositions=new Dictionary<string,Vector3>();
        readonly Dictionary<string,int> playerHealth=new Dictionary<string,int>();
        readonly Dictionary<string,float> respawnAt=new Dictionary<string,float>();
        readonly List<RaiderModel> sharedAgentModels=new List<RaiderModel>();
        float snapshotAt,moveAt;
        public ProceduralArena Arena { get; private set; }
        public PlayerAccountService Account { get; private set; }
        public LanRelayClient Relay { get; private set; }
        public PlayerMotor Motor { get; private set; }
        public BuildingSystem Building { get; private set; }
        public WeaponSystem Weapons { get; private set; }
        public CombatVfx Vfx { get; private set; }
        public RaiderDirector Raiders { get; private set; }
        public MatchDirector MatchDirector { get; private set; }
        public AgentTrainingStudio TrainingStudio { get; private set; }
        public IReadOnlyList<RaiderModel> SharedAgentModels { get { return sharedAgentModels; } }
        public MatchPhase Phase { get; private set; }=MatchPhase.Lobby;
        public MatchOptions Options { get; set; }=new MatchOptions();
        public float PhaseRemaining { get; set; }
        public int Wave { get; set; }
        public int Kills { get; set; }
        public int CoreHealth { get; set; }=100;
        public int LocalHealth { get { return PlayerHealth(LocalPlayerId); } }
        public int PlayerHealth(string id){return !string.IsNullOrEmpty(id)&&playerHealth.TryGetValue(id,out var hp)?hp:100;}
        public int MaxCoreHealth { get; private set; }=100;
        public int SelectedBlock { get; set; }
        public string Status { get; set; }="Sign in to play.";
        public string ServerUrl { get; set; }="http://localhost:8787";
        public string UsernameInput { get; set; }="";
        public string PasswordInput { get; set; }="";
        public string RoomInput { get; set; }="FORT";
        public string LocalPlayerId { get; private set; }="local";
        public bool Connected { get { return Relay!=null&&Relay.Connected; } }
        public bool IsHost { get { return !Connected||Relay.IsHost; } }
        public bool CanChooseMatchOptions { get { return !Connected||Relay.IsHost; } }
        public int Credits { get { return Account.Profile==null?0:Account.Profile.credits; } }
        public int SelectedWeapon { get { return Account.Equipped(UpgradeKind.Weapon); } }
        public int CrewCount { get { return Connected?Mathf.Max(1,Relay.Players.Length):1; } }

        void Awake()
        {
            Application.targetFrameRate=60;
            Arena=gameObject.AddComponent<ProceduralArena>();Arena.Build();
            Account=gameObject.AddComponent<PlayerAccountService>();Account.Initialize(this);
            Relay=gameObject.AddComponent<LanRelayClient>();Relay.Initialize(this);
            Motor=gameObject.AddComponent<PlayerMotor>();Motor.Initialize(this);
            Building=gameObject.AddComponent<BuildingSystem>();Building.Initialize(this);
            Weapons=gameObject.AddComponent<WeaponSystem>();Weapons.Initialize(this);
            Vfx=gameObject.AddComponent<CombatVfx>();Vfx.Initialize();
            Raiders=gameObject.AddComponent<RaiderDirector>();Raiders.Initialize(this);
            MatchDirector=gameObject.AddComponent<MatchDirector>();MatchDirector.Initialize(this);
            TrainingStudio=gameObject.AddComponent<AgentTrainingStudio>();TrainingStudio.Initialize(this);
            var hud=gameObject.AddComponent<SurvivalHud>();hud.Initialize(this);
            Cursor.lockState=CursorLockMode.None;
        }
        void Start(){if(Application.platform==RuntimePlatform.WebGLPlayer)try{ServerUrl=new Uri(Application.absoluteURL).GetLeftPart(UriPartial.Authority);}catch{}}
        void Update()
        {
            Account.Tick(Time.unscaledTime);
            if(!Account.SignedIn)return;
            if(IsHost&&Phase==MatchPhase.Wave)
            {
                foreach(var entry in new List<KeyValuePair<string,float>>(respawnAt))if(Time.time>=entry.Value)
                {
                    playerHealth[entry.Key]=100;respawnAt.Remove(entry.Key);
                    if(entry.Key==LocalPlayerId)Arena.LocalPlayer.transform.position=new Vector3(0,0,22);
                    PublishAction("heal",Vector3.zero,100,entry.Key);
                }
            }
            MatchDirector.Tick(Time.deltaTime);
            if(!Phase.IsPlaying()){if(Connected&&IsHost&&Phase==MatchPhase.GameOver&&Time.unscaledTime>=snapshotAt){snapshotAt=Time.unscaledTime+.35f;Relay.Send("state",JsonUtility.ToJson(CaptureSnapshot()));}return;}
            Motor.Tick(Time.deltaTime);
            if(Connected&&!IsHost&&Time.unscaledTime>=moveAt){moveAt=Time.unscaledTime+.15f;Relay.SendMove(Arena.LocalPlayer.transform.position);}
            if(Phase==MatchPhase.Build||Phase==MatchPhase.Intermission)Building.TickInput();
            if(Phase==MatchPhase.Wave){Weapons.Tick();Raiders.Tick(Time.deltaTime);}
            Arena.TickCamera(Time.deltaTime);
            if(Connected&&IsHost&&Time.unscaledTime>=snapshotAt){snapshotAt=Time.unscaledTime+.35f;Relay.Send("state",JsonUtility.ToJson(CaptureSnapshot()));}
        }

        public void OnSignedIn(string username)
        {
            Status="Welcome, "+username+".";if(Weapons!=null)Weapons.Equip(SelectedWeapon);
            var deployed=Account.FindAgentModel(Account.Profile.equippedAgentModel);
            if(deployed!=null)ApplyDeployedModel(deployed);
        }
        public bool TrySpend(int amount){return Account.Spend(amount);}
        public void EarnCredits(int amount){Account.Earn(amount);}
        public bool IsUnlocked(UpgradeKind kind,int index){return Account.IsUnlocked(kind,index);}
        public int Equipped(UpgradeKind kind){return Account.Equipped(kind);}
        public bool HasPerk(int index){return IsUnlocked(UpgradeKind.Perk,index)&&Equipped(UpgradeKind.Perk)==index;}
        public void SelectUpgrade(UpgradeKind kind,int index)
        {
            if(!IsUnlocked(kind,index)&&!Account.Unlock(kind,index)){Status="Not enough credits to unlock "+UpgradeCatalog.Name(kind,index)+".";return;}
            int previous=Account.Equipped(kind);Account.Equip(kind,index);if(kind==UpgradeKind.Block)SelectedBlock=index;if(kind==UpgradeKind.Weapon&&previous!=index)Weapons.Equip(index);Status="Equipped "+UpgradeCatalog.Name(kind,index)+".";
        }
        public void ChooseAlgorithm(RaiderAlgorithm algorithm){if(!CanChooseMatchOptions)return;Options.deployedModel=null;Options.Algorithm=algorithm;SendSnapshot();}
        public void ChooseDifficulty(AgentDifficulty difficulty){if(!CanChooseMatchOptions)return;Options.deployedModel=null;Options.Difficulty=difficulty;SendSnapshot();}
        public void DeployAgentModel(RaiderModel model)
        {
            if(model==null)return;
            if(Connected&&!IsHost){Relay.Send("model",JsonUtility.ToJson(model));return;}
            if(!CanChooseMatchOptions)return;
            Account.SaveAgentModel(model);ApplyDeployedModel(model);SendSnapshot();
        }
        public void OnAgentModelReceived(string json)
        {
            if(!IsHost)return;
            RaiderModel model=null;try{model=JsonUtility.FromJson<RaiderModel>(json);}catch{}
            if(model==null||string.IsNullOrEmpty(model.id)||model.qValues==null||model.qValues.Length!=24)return;
            model.name=string.IsNullOrWhiteSpace(model.name)?"Crew Raider":model.name.Substring(0,Mathf.Min(24,model.name.Length));
            model.Algorithm=model.Algorithm;model.Difficulty=model.Difficulty;
            model.wallFocus=Mathf.Clamp(model.wallFocus,.2f,3f);model.breachFocus=Mathf.Clamp(model.breachFocus,.2f,3f);model.coreFocus=Mathf.Clamp(model.coreFocus,.2f,3f);model.exploration=Mathf.Clamp(model.exploration,.02f,.6f);
            for(int i=0;i<model.qValues.Length;i++)if(float.IsNaN(model.qValues[i])||float.IsInfinity(model.qValues[i]))model.qValues[i]=0;else model.qValues[i]=Mathf.Clamp(model.qValues[i],-1000f,1000f);
            int index=sharedAgentModels.FindIndex(candidate=>candidate.id==model.id);
            if(index<0)sharedAgentModels.Add(model.Copy());else sharedAgentModels[index]=model.Copy();
            Status="Received “"+model.name+"” from a crew member. It is ready to deploy in the Training Studio.";
        }
        void ApplyDeployedModel(RaiderModel model)
        {
            Options.deployedModel=model.Copy();Options.Algorithm=model.Algorithm;Options.Difficulty=model.Difficulty;
        }
        public void DamageCore(int damage)
        {
            if(!IsHost||Phase==MatchPhase.GameOver)return;CoreHealth=Mathf.Max(0,CoreHealth-Mathf.Max(0,damage));
            if(CoreHealth<=0)SetPhase(MatchPhase.GameOver,0,"The crew core was destroyed. Your progress is saved.");
        }
        public void SetPhase(MatchPhase phase,float timer,string status){Phase=phase;PhaseRemaining=timer;Status=status;}
        public void ResetMatchState()
        {
            Building.Clear();Raiders.Clear();Wave=0;Kills=0;MaxCoreHealth=HasPerk(3)?145:100;CoreHealth=MaxCoreHealth;SelectedBlock=Equipped(UpgradeKind.Block);Weapons.Refill();playerPositions.Clear();playerHealth.Clear();respawnAt.Clear();
            if(Connected)foreach(var member in Relay.Players){playerPositions[member.id]=new Vector3(0,0,19);playerHealth[member.id]=100;}
            playerHealth[LocalPlayerId]=100;
            Arena.LocalPlayer.transform.position=new Vector3(0,0,19f);
        }
        public void StartFromLobby()
        {
            if(Connected&&!Relay.IsHost)return;
            MatchDirector.enabled=true;MatchDirector.StartMatch(Options);
            if(Connected)Relay.Send("start",JsonUtility.ToJson(Options));
        }
        public void StartMatchFromHost(MatchOptions options){MatchDirector.enabled=true;MatchDirector.StartMatch(options);}
        public void ReturnToLobby(){if(Connected&&Relay.IsHost)Relay.Send("lobby","");SetPhase(MatchPhase.Lobby,0,"Back in the lobby. Your account progress is saved.");}
        public void ReturnToLobbyFromHost(){SetPhase(MatchPhase.Lobby,0,"Host returned the crew to the lobby.");}
        public void ToggleRoom(){if(Connected)LeaveRoom();else Relay.Join(RoomInput);}
        public void LeaveRoom(){Relay.Leave();SetPhase(MatchPhase.Lobby,0,"Left the LAN room.");playerPositions.Clear();}
        public void SignOut(){if(Connected)LeaveRoom();Account.SignOut();PasswordInput="";SetPhase(MatchPhase.Lobby,0,"Signed out.");}

        public void OnRoomJoined(string room,bool host){LocalPlayerId=Relay.ClientId;Status=host?"Hosting room "+room:"Joined room "+room;playerPositions[LocalPlayerId]=Arena.LocalPlayer.transform.position;playerHealth[LocalPlayerId]=100;if(host)SendSnapshot();}
        public void OnHostChanged(bool host){Status=host?"Host role transferred to this player.":"Match host changed.";if(host)SendSnapshot();}
        public void OnPlayerJoined(RelayEvent data){Relay.AddPlayer(data.from,data.name);playerPositions[data.from]=new Vector3(0,0,19);playerHealth[data.from]=100;if(IsHost)SendSnapshot();}
        public void OnPlayerLeft(string id){Relay.RemovePlayer(id);playerPositions.Remove(id);playerHealth.Remove(id);respawnAt.Remove(id);Arena.RemoveRemotePlayer(id);}
        public bool FindLivingPlayer(Vector3 from,float range,out string id,out Vector3 position)
        {
            id=null;position=Vector3.zero;float best=range;
            foreach(var entry in playerPositions){if(PlayerHealth(entry.Key)<=0)continue;float distance=Vector3.Distance(from,entry.Value);if(distance<best){best=distance;id=entry.Key;position=entry.Value;}}
            return id!=null;
        }
        public void DamagePlayer(string id,int damage)
        {
            if(!IsHost||string.IsNullOrEmpty(id)||PlayerHealth(id)<=0)return;
            int health=Mathf.Max(0,PlayerHealth(id)-Mathf.Max(0,damage));SetPlayerHealth(id,health);
            if(Vfx&&playerPositions.ContainsKey(id))Vfx.PlayBlood(playerPositions[id]+Vector3.up*1.25f);
            if(health==0)respawnAt[id]=Time.time+8f;
            PublishAction("melee",playerPositions.ContainsKey(id)?playerPositions[id]:Vector3.zero,health,id);
        }
        void SetPlayerHealth(string id,int health)
        {
            playerHealth[id]=Mathf.Clamp(health,0,100);Arena.SetPlayerHealth(id,playerHealth[id]);
        }
        public void BuyMedkit()
        {
            if(!Phase.IsPlaying()||LocalHealth<=0||LocalHealth>=100||!TrySpend(40))return;
            if(IsHost)HealPlayer(LocalPlayerId);else SendCommand("input",new GameCommand{action="heal"});
        }
        void HealPlayer(string id){if(PlayerHealth(id)<=0)return;int health=Mathf.Min(100,PlayerHealth(id)+45);SetPlayerHealth(id,health);PublishAction("heal",Vector3.zero,health,id);}
        public void HealCrewBetweenWaves(){if(!IsHost)return;foreach(var id in new List<string>(playerHealth.Keys))if(PlayerHealth(id)>0)SetPlayerHealth(id,Mathf.Min(100,PlayerHealth(id)+35));}
        public void OnPlayerMoved(string id,string payload)
        {
            var move=JsonUtility.FromJson<GameCommand>(payload);if(move==null)return;var position=new Vector3(move.x,0,move.z);playerPositions[id]=position;Arena.SetRemotePlayer(id,position);
        }
        public void SetPlayerPosition(string id,Vector3 position)
        {
            if(string.IsNullOrEmpty(id))return;
            playerPositions[id]=position;
        }
        public void SendCommand(string type,GameCommand command){Relay.Send(type,JsonUtility.ToJson(command));}
        public void PublishAction(string action,Vector3 position,int value,string id="")
        {
            if(!Connected||!IsHost)return;Relay.Send("action",JsonUtility.ToJson(new GameCommand{action=action,id=id,x=position.x,y=position.y,z=position.z,value=value}));
        }
        public void ApplyClientInput(string playerId,string payload)
        {
            var command=JsonUtility.FromJson<GameCommand>(payload);if(command==null)return;var position=new Vector3(command.x,command.y>0?command.y:.5f,command.z);
            if(command.action=="build"){Building.TryPlace(position,command.value,true,false);PublishAction("build",position,command.value);}
            else if(command.action=="repair"){Building.RepairAt(position,false);PublishAction("repair",position,90);}
            else if(command.action=="shoot"){var weapon=UpgradeCatalog.Weapons[Mathf.Clamp(command.value,0,UpgradeCatalog.Weapons.Length-1)];Weapons.ApplyShot(command.id,command.value2>0?command.value2:weapon.Damage,weapon.SplashRadius);}
            else if(command.action=="heal")HealPlayer(playerId);
        }
        public void ApplyHostAction(string payload)
        {
            var command=JsonUtility.FromJson<GameCommand>(payload);if(command==null)return;var position=new Vector3(command.x,command.y>0?command.y:.5f,command.z);
            switch(command.action)
            {
                case "build":Building.TryPlace(position,command.value,true,false);break;
                case "repair":Building.RepairAt(position,false);break;
                case "wave":Wave=Mathf.Max(Wave,command.value);break;
                case "spawn":Raiders.ApplySpawn(new Vector3(command.x,0,command.z),command.value,command.id);break;
                case "kill":Raiders.ApplyHostKill(command.id);break;
                case "damage":Building.SetHealth(position,command.value);break;
                case "block_destroy":Building.RemoveAt(position);break;
                case "raider_damage":Raiders.SetHealth(command.id,command.value);break;
                case "melee":SetPlayerHealth(command.id,command.value);if(Vfx)Vfx.PlayBlood(position+Vector3.up*1.25f);break;
                case "heal":SetPlayerHealth(command.id,command.value);break;
            }
        }
        public void ApplySnapshot(MatchSnapshot snapshot)
        {
            MatchPhase previous=Phase;MatchPhase next;if(!Enum.TryParse(snapshot.phase,out next))return;
            int previousKills=Kills;
            if(snapshot.algorithm!=null)Options.algorithm=snapshot.algorithm;if(snapshot.difficulty!=null)Options.difficulty=snapshot.difficulty;Options.deployedModel=snapshot.hasDeployedModel&&snapshot.deployedModel!=null?snapshot.deployedModel.Copy():null;
            Wave=snapshot.wave;PhaseRemaining=snapshot.timer;CoreHealth=snapshot.health;MaxCoreHealth=snapshot.maxHealth>0?snapshot.maxHealth:100;Kills=snapshot.kills;SetPhase(next,snapshot.timer,StatusFor(next));
            if(!IsHost&&Kills>previousKills)EarnCredits((15+(HasPerk(1)?5:0))*(Kills-previousKills));
            if(previous==MatchPhase.Wave&&next==MatchPhase.Intermission)EarnCredits(50+Wave*12);
            Building.Apply(snapshot.blocks);Raiders.Apply(snapshot.raiders);Raiders.SetRemainingToSpawn(snapshot.remaining);
            if(snapshot.players!=null)foreach(var player in snapshot.players){var position=new Vector3(player.x,0,player.z);playerPositions[player.id]=position;if(player.id!=LocalPlayerId)Arena.SetRemotePlayer(player.id,position);SetPlayerHealth(player.id,player.hp);}
        }
        MatchSnapshot CaptureSnapshot()
        {
            playerPositions[LocalPlayerId]=Arena.LocalPlayer.transform.position;var playerStates=new List<PlayerSnapshot>();foreach(var entry in playerPositions)playerStates.Add(new PlayerSnapshot{id=entry.Key,x=entry.Value.x,z=entry.Value.z,hp=PlayerHealth(entry.Key),maxHp=100,gun=entry.Key==LocalPlayerId?SelectedWeapon:0});
            return new MatchSnapshot{phase=Phase.ToString(),algorithm=Options.algorithm,difficulty=Options.difficulty,hasDeployedModel=Options.deployedModel!=null,deployedModel=Options.deployedModel,timer=PhaseRemaining,wave=Wave,remaining=Raiders.RemainingToSpawn,health=CoreHealth,maxHealth=MaxCoreHealth,kills=Kills,blocks=Building.Capture(),raiders=Raiders.Capture(),players=playerStates.ToArray()};
        }
        void SendSnapshot(){if(Connected&&IsHost)Relay.Send("state",JsonUtility.ToJson(CaptureSnapshot()));}
        static string StatusFor(MatchPhase phase){switch(phase){case MatchPhase.Build:return "Build phase — fortify the base.";case MatchPhase.Wave:return "Raiders are attacking. Defend the core.";case MatchPhase.Intermission:return "Wave cleared — repair, resupply, and upgrade.";case MatchPhase.GameOver:return "The crew core was destroyed. Your account progress is saved.";default:return "In the lobby.";}}
    }
}
