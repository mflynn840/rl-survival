using UnityEngine;

namespace RLSurvival
{
    public sealed class SurvivalHud : MonoBehaviour
    {
        SurvivalGame game;
        Vector2 upgradesScroll;
        Vector2 modelsScroll;
        int upgradeTab;
        bool trainingStudioOpen;
        bool settingsOpen;
        public bool SettingsOpen { get { return settingsOpen; } }
        int selectedResolution;
        int selectedGraphics;
        bool selectedVsync;
        static readonly Vector2Int[] SupportedResolutions={new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(2560,1440),new Vector2Int(3840,2160)};
        public void Initialize(SurvivalGame owner){game=owner;selectedResolution=Mathf.Clamp(PlayerPrefs.GetInt("rl-survival-resolution",1),0,SupportedResolutions.Length-1);selectedGraphics=Mathf.Clamp(PlayerPrefs.GetInt("rl-survival-graphics",2),0,3);selectedVsync=PlayerPrefs.GetInt("rl-survival-vsync",1)==1;}
        void OnGUI()
        {
            if(game==null)return;GUI.skin.label.fontSize=15;GUI.skin.button.fontSize=14;GUI.skin.box.fontSize=14;
            if(GUI.Button(new Rect(Screen.width-144,12,130,34),"⚙ SETTINGS"))settingsOpen=!settingsOpen;
            if(!game.Account.SignedIn)DrawSignIn();
            else if(game.Phase==MatchPhase.Lobby){if(trainingStudioOpen)DrawTrainingStudio();else DrawLobby();}
            else DrawMatch();
            if(settingsOpen)DrawSettings();
        }
        void DrawSettings()
        {
            float width=Mathf.Min(470,Screen.width-32),height=Mathf.Min(510,Screen.height-24),x=(Screen.width-width)*.5f,y=(Screen.height-height)*.5f;
            GUILayout.BeginArea(new Rect(x,y,width,height),GUI.skin.window);GUILayout.Space(12);CenterLabel("DISPLAY & GRAPHICS",25);GUILayout.Space(7);
            GUILayout.Label(Application.platform==RuntimePlatform.WebGLPlayer?"Choose up to 4K for local builds. Browser rendering follows the size of this tab.":"Choose the game resolution (up to 4K). The setting is saved on this device.");
            for(int i=0;i<SupportedResolutions.Length;i++)
            {
                var resolution=SupportedResolutions[i];string label=resolution.x+" × "+resolution.y+(resolution.x==3840?" · 4K":"");
                GUI.backgroundColor=selectedResolution==i?new Color(.45f,.7f,.62f):Color.white;
                if(GUILayout.Button(label,GUILayout.Height(34)))selectedResolution=i;
            }
            GUI.backgroundColor=Color.white;GUILayout.Space(8);GUILayout.Label("Graphics quality  •  shadows, foliage, draw distance, anti-aliasing");GUILayout.BeginHorizontal();
            for(int i=0;i<4;i++){GUI.backgroundColor=selectedGraphics==i?new Color(.36f,.69f,.64f):Color.white;if(GUILayout.Button(VisualStyle.Label(i),GUILayout.Height(32)))selectedGraphics=i;}
            GUI.backgroundColor=Color.white;GUILayout.EndHorizontal();GUILayout.Label(selectedGraphics<1?"Lean lighting for older laptops.":selectedGraphics<2?"Balanced visuals for most systems.":selectedGraphics<3?"Dense scenery and crisp shadows.":"Maximum foliage, draw distance, and shadow quality.");
            selectedVsync=GUILayout.Toggle(selectedVsync," VSync (match the display refresh rate)");GUILayout.Space(8);GUILayout.BeginHorizontal();
            if(GUILayout.Button("Apply",GUILayout.Height(38)))ApplyResolution();
            if(GUILayout.Button("Close",GUILayout.Height(38)))settingsOpen=false;
            GUILayout.EndHorizontal();GUILayout.EndArea();
        }
        void ApplyResolution()
        {
            selectedResolution=Mathf.Clamp(selectedResolution,0,SupportedResolutions.Length-1);
            var resolution=SupportedResolutions[selectedResolution];
            // A browser owns the WebGL canvas size. Screen.SetResolution changes
            // its backing buffer without resizing the tab and can shrink the
            // rendered viewport, so only apply the selected mode in local builds.
            if(Application.platform!=RuntimePlatform.WebGLPlayer)Screen.SetResolution(resolution.x,resolution.y,Screen.fullScreen);
            if(game!=null&&game.Arena!=null)game.Arena.ApplyGraphics(selectedGraphics);
            QualitySettings.vSyncCount=selectedVsync?1:0;
            PlayerPrefs.SetInt("rl-survival-resolution",selectedResolution);PlayerPrefs.SetInt("rl-survival-graphics",selectedGraphics);PlayerPrefs.SetInt("rl-survival-vsync",selectedVsync?1:0);PlayerPrefs.Save();
        }
        void DrawSignIn()
        {
            GUILayout.BeginArea(new Rect(Screen.width*.5f-255,Screen.height*.5f-225,510,450),GUI.skin.box);GUILayout.Space(22);
            CenterLabel("RL SURVIVAL  •  BETA 2",30);CenterLabel("Local accounts. No email needed.",16);GUILayout.Space(22);
            GUILayout.Label("Game server address");game.ServerUrl=GUILayout.TextField(game.ServerUrl);
            GUILayout.Label("Username (3–20 letters, numbers, _ or -)");game.UsernameInput=GUILayout.TextField(game.UsernameInput,20);
            GUILayout.Label("Password (8 characters minimum)");game.PasswordInput=GUILayout.PasswordField(game.PasswordInput,'•',64);
            GUILayout.BeginHorizontal();GUI.enabled=!game.Account.IsBusy;if(GUILayout.Button("Create account",GUILayout.Height(40)))game.Account.Authenticate(game.UsernameInput,game.PasswordInput,true);if(GUILayout.Button("Log in",GUILayout.Height(40)))game.Account.Authenticate(game.UsernameInput,game.PasswordInput,false);GUI.enabled=true;GUILayout.EndHorizontal();
            GUILayout.Space(10);GUILayout.Label(game.Account.Message);GUILayout.Label("Accounts and saved upgrades live on the LAN host.");GUILayout.EndArea();
        }
        void DrawLobby()
        {
            GUILayout.BeginArea(new Rect(Screen.width*.5f-340,Screen.height*.5f-310,680,620),GUI.skin.box);GUILayout.Space(18);CenterLabel("RL SURVIVAL  •  BETA 2",29);CenterLabel("Hold your plot across the road from Paradise Motel.",16);GUILayout.Space(14);
            GUILayout.Label("Signed in: "+game.Account.Profile.username+"    Credits: $"+game.Credits);
            GUILayout.BeginHorizontal();GUILayout.Label("Room code",GUILayout.Width(100));game.RoomInput=GUILayout.TextField(game.RoomInput,12).ToUpperInvariant();GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();GUILayout.Label("Server URL",GUILayout.Width(100));game.ServerUrl=GUILayout.TextField(game.ServerUrl);GUILayout.EndHorizontal();GUILayout.Space(6);
            GUILayout.Label("Choose the raider learning algorithm");GUILayout.BeginHorizontal();
            AlgorithmButton(RaiderAlgorithm.QLearning,"Q-Learning");AlgorithmButton(RaiderAlgorithm.SARSA,"SARSA");AlgorithmButton(RaiderAlgorithm.DoubleQLearning,"Double Q-Learning");GUILayout.EndHorizontal();
            GUILayout.Label(AlgorithmDescription(game.Options.Algorithm));if(game.Options.deployedModel!=null)GUILayout.Label("Deployed model: "+game.Options.deployedModel.name+"  •  "+game.Options.deployedModel.trainingEpisodes+" training episodes");GUILayout.Space(4);GUILayout.Label("Difficulty profile");GUILayout.BeginHorizontal();
            DifficultyButton(AgentDifficulty.Rookie,"Rookie");DifficultyButton(AgentDifficulty.Standard,"Standard");DifficultyButton(AgentDifficulty.Veteran,"Veteran");GUILayout.EndHorizontal();GUILayout.Label(DifficultyDescription(game.Options.Difficulty));GUILayout.Space(10);
            GUILayout.BeginHorizontal();if(GUILayout.Button(game.Connected?"Leave room":"Join LAN room",GUILayout.Height(42)))game.ToggleRoom();GUI.enabled=!game.Connected||game.IsHost;
            if(GUILayout.Button(game.Connected?(game.IsHost?"Host & start":"Waiting for host") : "Solo run",GUILayout.Height(42)))game.StartFromLobby();GUI.enabled=true;GUILayout.EndHorizontal();
            GUILayout.Space(8);GUILayout.Label(game.Status);foreach(var player in game.Relay.Players){string host=player.id==game.Relay.HostId?"  • HOST":"";GUILayout.Label("  "+player.name+host+(player.id==game.Relay.ClientId?"  (you)":""));}
            GUILayout.Space(6);if(GUILayout.Button("Open Agent Training Studio",GUILayout.Height(36)))trainingStudioOpen=true;
            GUILayout.Label("Controls: WASD move  •  Click build / fire  •  1–8 blocks  •  V first/third  •  B build view");GUILayout.Label("Your saved wallet, unlocks, and loadout carry into future runs.");if(GUILayout.Button("Sign out"))game.SignOut();GUILayout.EndArea();
        }
        void DrawTrainingStudio()
        {
            GUILayout.BeginArea(new Rect(Screen.width*.5f-370,Screen.height*.5f-340,740,680),GUI.skin.box);
            GUILayout.Space(12);CenterLabel("AGENT TRAINING STUDIO",27);CenterLabel("Design a raider, train its policy, and deploy the saved model.",15);
            GUILayout.BeginHorizontal();GUILayout.Label("Model name",GUILayout.Width(95));game.TrainingStudio.DraftName=GUILayout.TextField(game.TrainingStudio.DraftName,24);GUILayout.EndHorizontal();
            GUILayout.Label("Learning algorithm");GUILayout.BeginHorizontal();StudioAlgorithm(RaiderAlgorithm.QLearning,"Q-Learning");StudioAlgorithm(RaiderAlgorithm.SARSA,"SARSA");StudioAlgorithm(RaiderAlgorithm.DoubleQLearning,"Double Q");GUILayout.EndHorizontal();
            GUILayout.Label("Difficulty profile");GUILayout.BeginHorizontal();StudioDifficulty(AgentDifficulty.Rookie,"Rookie");StudioDifficulty(AgentDifficulty.Standard,"Standard");StudioDifficulty(AgentDifficulty.Veteran,"Veteran");GUILayout.EndHorizontal();
            game.TrainingStudio.WallFocus=StudioSlider("Wall pressure",game.TrainingStudio.WallFocus,.2f,3f);
            game.TrainingStudio.BreachFocus=StudioSlider("Breach rewards",game.TrainingStudio.BreachFocus,.2f,3f);
            game.TrainingStudio.CoreFocus=StudioSlider("Core aggression",game.TrainingStudio.CoreFocus,.2f,3f);
            game.TrainingStudio.Exploration=StudioSlider("Exploration",game.TrainingStudio.Exploration,.02f,.6f);
            GUI.enabled=!game.TrainingStudio.IsTraining;if(GUILayout.Button(game.TrainingStudio.IsTraining?"Training…":"Train 600 episodes",GUILayout.Height(38)))game.TrainingStudio.TrainDraft();GUI.enabled=true;
            if(game.TrainingStudio.IsTraining)GUILayout.HorizontalSlider(game.TrainingStudio.Progress,0f,1f);
            GUILayout.Label(game.TrainingStudio.Status);GUILayout.Space(6);GUILayout.Label("Saved models  •  select Deploy to equip one for your next match");
            modelsScroll=GUILayout.BeginScrollView(modelsScroll,GUILayout.Height(205));
            var models=game.Account.Profile.agentModels;
            if(models!=null)foreach(var model in models)
            {
                if(model==null)continue;
                GUILayout.BeginHorizontal(GUI.skin.box);GUILayout.Label(model.name+"  •  "+model.Algorithm+" / "+model.Difficulty+"  •  "+model.trainingEpisodes+" episodes  •  "+Mathf.RoundToInt(model.breakthroughRate*100)+"% breakthrough",GUILayout.MinWidth(460));
                string deployLabel=game.Connected&&!game.IsHost?"Share with host":game.Account.Profile.equippedAgentModel==model.id?"Deployed":"Deploy";
                if(GUILayout.Button(deployLabel,GUILayout.Width(120)))game.TrainingStudio.Deploy(model);GUILayout.EndHorizontal();
            }
            if(game.IsHost&&game.SharedAgentModels.Count>0)
            {
                GUILayout.Space(5);GUILayout.Label("Models shared by crew");
                foreach(var model in game.SharedAgentModels)
                {
                    GUILayout.BeginHorizontal(GUI.skin.box);GUILayout.Label(model.name+"  •  "+model.Algorithm+" / "+model.Difficulty+"  •  "+model.trainingEpisodes+" episodes",GUILayout.MinWidth(460));
                    if(GUILayout.Button("Deploy to match",GUILayout.Width(120)))game.TrainingStudio.Deploy(model);GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndScrollView();
            GUILayout.FlexibleSpace();if(GUILayout.Button("Back to lobby",GUILayout.Height(34)))trainingStudioOpen=false;GUILayout.EndArea();
        }
        void DrawMatch()
        {
            GUI.Box(new Rect(14,14,390,136),"");GUI.Label(new Rect(28,22,360,24),"RL SURVIVAL   •   WAVE "+game.Wave+"   •   "+game.Phase.ToString().ToUpper());
            GUI.Label(new Rect(28,49,360,23),"$"+game.Credits+"   Ammo "+game.Weapons.Ammo+" / "+game.Weapons.Magazine+"   Kills "+game.Kills);
            GUI.Label(new Rect(28,75,360,23),"Core "+game.CoreHealth+" / "+game.MaxCoreHealth+"   You "+game.LocalHealth+" / 100   Raiders "+game.Raiders.AliveCount);
            GUI.Label(new Rect(28,103,360,22),game.Phase==MatchPhase.Wave?"Wave time "+Mathf.CeilToInt(game.PhaseRemaining)+"s":"Next wave "+Mathf.CeilToInt(game.PhaseRemaining)+"s");GUI.Label(new Rect(28,126,360,21),game.Status);
            DrawUpgradePanel();
            string agentLabel=game.Options.deployedModel==null?game.Options.Algorithm+" / "+game.Options.Difficulty:game.Options.deployedModel.name+"  •  "+game.Options.Algorithm+" / "+game.Options.Difficulty;
            GUI.Label(new Rect(Screen.width-370,15,355,50),agentLabel+"   •   "+game.Arena.CameraModeLabel+" view   •   V switch   •   B build view");
            if(game.Connected&&GUI.Button(new Rect(Screen.width-130,Screen.height-48,118,32),"Leave room"))game.LeaveRoom();
            if(game.Phase==MatchPhase.GameOver){GUI.Box(new Rect(Screen.width*.5f-220,Screen.height*.5f-90,440,180),"");CenterLabelAt("CORE BREACHED  •  WAVES SURVIVED: "+game.Wave,Screen.width*.5f-210,Screen.height*.5f-68,420,32,20);if(GUI.Button(new Rect(Screen.width*.5f-100,Screen.height*.5f+6,200,40),game.IsHost?"Return to lobby":"Waiting for host")){if(game.IsHost)game.ReturnToLobby();}}
        }
        void DrawUpgradePanel()
        {
            GUILayout.BeginArea(new Rect(14,160,290,Screen.height-175));GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("LOADOUT  •  buy once, equip any time");GUILayout.BeginHorizontal();TabButton("Weapons",0);TabButton("Blocks",1);TabButton("Perks",2);GUILayout.EndHorizontal();
            upgradesScroll=GUILayout.BeginScrollView(upgradesScroll,GUILayout.Height(Mathf.Max(190,Screen.height-290)));
            UpgradeKind kind=(UpgradeKind)upgradeTab;int count=UpgradeCatalog.Size(kind);
            for(int i=0;i<count;i++)
            {
                bool unlocked=game.IsUnlocked(kind,i),equipped=game.Equipped(kind)==i;int cost=UpgradeCatalog.UnlockCost(kind,i);
                string text=(equipped?"▶ ":"")+UpgradeCatalog.Name(kind,i)+(unlocked?"  •  OWNED":"  •  unlock $"+cost);
                if(GUILayout.Button(text,GUILayout.Height(32)))game.SelectUpgrade(kind,i);
            }
            GUILayout.EndScrollView();
            if(game.Phase==MatchPhase.Wave||game.Phase==MatchPhase.Intermission)if(GUILayout.Button("Buy ammo  •  $25"))game.Weapons.BuyAmmo();
            if(game.Phase.IsPlaying())if(GUILayout.Button("Medkit  •  $40"))game.BuyMedkit();
            if(game.Phase==MatchPhase.Build||game.Phase==MatchPhase.Intermission)if(GUILayout.Button("Repair nearby block  •  R / $20"))game.Building.RepairNearest();
            GUILayout.EndVertical();GUILayout.EndArea();
        }
        void AlgorithmButton(RaiderAlgorithm algorithm,string label){GUI.enabled=game.CanChooseMatchOptions;GUI.backgroundColor=game.Options.Algorithm==algorithm?new Color(.35f,.72f,.68f):Color.white;if(GUILayout.Button(label,GUILayout.Height(34)))game.ChooseAlgorithm(algorithm);GUI.backgroundColor=Color.white;GUI.enabled=true;}
        void DifficultyButton(AgentDifficulty difficulty,string label){GUI.enabled=game.CanChooseMatchOptions;GUI.backgroundColor=game.Options.Difficulty==difficulty?new Color(.82f,.66f,.34f):Color.white;if(GUILayout.Button(label,GUILayout.Height(32)))game.ChooseDifficulty(difficulty);GUI.backgroundColor=Color.white;GUI.enabled=true;}
        void TabButton(string label,int tab){if(GUILayout.Toggle(upgradeTab==tab,label,GUI.skin.button))upgradeTab=tab;}
        void StudioAlgorithm(RaiderAlgorithm value,string label){GUI.backgroundColor=game.TrainingStudio.DraftAlgorithm==value?new Color(.35f,.72f,.68f):Color.white;if(GUILayout.Button(label))game.TrainingStudio.DraftAlgorithm=value;GUI.backgroundColor=Color.white;}
        void StudioDifficulty(AgentDifficulty value,string label){GUI.backgroundColor=game.TrainingStudio.DraftDifficulty==value?new Color(.82f,.66f,.34f):Color.white;if(GUILayout.Button(label))game.TrainingStudio.DraftDifficulty=value;GUI.backgroundColor=Color.white;}
        static float StudioSlider(string label,float value,float min,float max){GUILayout.BeginHorizontal();GUILayout.Label(label,GUILayout.Width(125));value=GUILayout.HorizontalSlider(value,min,max);GUILayout.Label(value.ToString("0.00"),GUILayout.Width(42));GUILayout.EndHorizontal();return value;}
        static string AlgorithmDescription(RaiderAlgorithm algorithm){switch(algorithm){case RaiderAlgorithm.SARSA:return "SARSA learns from the next action the raider actually takes; behavior stays policy-aware.";case RaiderAlgorithm.DoubleQLearning:return "Double Q-Learning uses two value tables to reduce optimistic value estimates.";default:return "Q-Learning learns the highest-value future action, even when exploring.";}}
        static string DifficultyDescription(AgentDifficulty difficulty){switch(difficulty){case AgentDifficulty.Rookie:return "Lower health, slower mining, smaller groups, and more exploration.";case AgentDifficulty.Veteran:return "High health, faster mining, denser waves, and more focused learning.";default:return "Balanced enemy stats and a moderate exploration rate.";}}
        static void CenterLabel(string text,int size){GUILayout.Label(text,new GUIStyle(GUI.skin.label){fontSize=size,alignment=TextAnchor.MiddleCenter});}
        static void CenterLabelAt(string text,float x,float y,float width,float height,int size){GUI.Label(new Rect(x,y,width,height),text,new GUIStyle(GUI.skin.label){fontSize=size,alignment=TextAnchor.MiddleCenter});}
    }
}
