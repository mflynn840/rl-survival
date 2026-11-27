using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RLSurvival
{
    /// <summary>Owns the 3D roadside level, player views, camera, and crew core.</summary>
    public sealed class ProceduralArena : MonoBehaviour
    {
        readonly Dictionary<string,GameObject> remotePlayers=new Dictionary<string,GameObject>();
        readonly Dictionary<string,Vector3> remotePositions=new Dictionary<string,Vector3>();
        RoadsideMapBuilder district;
        enum ViewMode { Third, First, Tactical }
        ViewMode viewMode=ViewMode.Third;
        float viewYaw, viewPitch;
        public string CameraModeLabel { get { return viewMode.ToString(); } }
        public Vector3 ViewForward { get { return Quaternion.Euler(0,-viewYaw,0)*Vector3.back; } }
        public Vector3 MoveDirection(Vector3 input){return Quaternion.Euler(0,-viewYaw,0)*input;}
        public GameObject LocalPlayer { get; private set; }
        public Transform Core { get; private set; }
        public Vector3 CorePosition { get { return Core ? Core.position : new Vector3(0,0,10); } }
        public Camera GameCamera { get; private set; }
        public Transform WorldRoot { get; private set; }
        public int GraphicsPreset { get; private set; }
        public Vector3[] HotelExitPoints { get { return district==null?new Vector3[0]:district.HotelExitPoints; } }
        public void OpenHotelDoor(int index){if(district)district.OpenDoor(index);}
        public void SetPlayerHealth(string id,int health)
        {
            GameObject actor=id==GetComponent<SurvivalGame>().LocalPlayerId?LocalPlayer:null;
            if(actor==null)remotePlayers.TryGetValue(id,out actor);
            if(actor){var bar=actor.GetComponent<CombatHealthBar>();if(bar)bar.Set(health,100,false);}
        }

        public void Build()
        {
            WorldRoot=new GameObject("Roadside Motel Survival World").transform;
            WorldRoot.SetParent(transform,false);
            ConfigureAtmosphere();
            district=gameObject.AddComponent<RoadsideMapBuilder>();district.Build(WorldRoot);
            var light=new GameObject("Late Afternoon Sun").AddComponent<Light>();light.transform.SetParent(WorldRoot,false);light.type=LightType.Directional;light.intensity=1.2f;light.color=VisualStyle.Sun;light.shadows=LightShadows.Soft;light.shadowStrength=.78f;light.transform.rotation=Quaternion.Euler(43,-32,0);
            var core=GameObject.CreatePrimitive(PrimitiveType.Cube);core.name="Crew Core";core.transform.SetParent(WorldRoot,false);core.transform.position=new Vector3(0,.9f,10.25f);core.transform.localScale=new Vector3(1.5f,1.8f,1.5f);Tint(core,new Color(.17f,.68f,.62f));Core=core.transform;
            LocalPlayer=new GameObject("Player Survivor");LocalPlayer.transform.SetParent(WorldRoot,false);LocalPlayer.transform.position=new Vector3(0,0,19f);
            HumanoidModelBuilder.Create(LocalPlayer,HumanoidStyle.Survivor,new Color(.25f,.21f,.17f));
            GameCamera=new GameObject("District Camera").AddComponent<Camera>();GameCamera.tag="MainCamera";GameCamera.transform.SetParent(WorldRoot,false);GameCamera.fieldOfView=57;GameCamera.nearClipPlane=.1f;GameCamera.farClipPlane=180;GameCamera.backgroundColor=VisualStyle.SkyTop;GameCamera.allowHDR=true;FollowCamera(1f);
            ApplyGraphics(PlayerPrefs.GetInt("rl-survival-graphics",2));
        }

        void ConfigureAtmosphere()
        {
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=VisualStyle.SkyTop;
            RenderSettings.ambientEquatorColor=VisualStyle.SkyHorizon*.62f;
            RenderSettings.ambientGroundColor=VisualStyle.GroundBounce;
            RenderSettings.fog=true;RenderSettings.fogColor=VisualStyle.SkyHorizon;RenderSettings.fogDensity=.0032f;
            var shader=Shader.Find("Skybox/Procedural");
            if(shader)
            {
                var skybox=new Material(shader);skybox.SetColor("_SkyTint",VisualStyle.SkyTop);skybox.SetColor("_GroundColor",VisualStyle.GroundBounce);skybox.SetFloat("_AtmosphereThickness",.7f);skybox.SetFloat("_Exposure",1.18f);RenderSettings.skybox=skybox;
            }
            DynamicGI.UpdateEnvironment();
        }

        public void ApplyGraphics(int preset)
        {
            GraphicsPreset=Mathf.Clamp(preset,0,3);VisualStyle.ApplyQuality(GraphicsPreset);
            if(GameCamera)GameCamera.allowHDR=GraphicsPreset>=2;
            if(district)district.SetDetailLevel(GraphicsPreset);
        }

        public void TickCamera(float deltaTime)
        {
            if(Input.GetKeyDown(KeyCode.V))viewMode=viewMode==ViewMode.First?ViewMode.Third:ViewMode.First;
            if(Input.GetKeyDown(KeyCode.B))viewMode=viewMode==ViewMode.Tactical?ViewMode.Third:ViewMode.Tactical;
            if(Input.GetMouseButton(1)){viewYaw+=Input.GetAxis("Mouse X")*3.2f;viewPitch=Mathf.Clamp(viewPitch+Input.GetAxis("Mouse Y")*2f,-25f,25f);}
            FollowCamera(deltaTime);
        }
        public void AnimateLocal(Vector3 movement,float speed)
        {
            var rig=LocalPlayer?LocalPlayer.GetComponent<HumanoidModelBuilder>():null;
            if(rig!=null)rig.Animate(movement,speed);
            LocalPlayer.transform.forward=ViewForward;
        }

        public void SetRemotePlayer(string id,Vector3 position)
        {
            if(string.IsNullOrEmpty(id)||id=="local")return;
            if(!remotePlayers.TryGetValue(id,out var actor))
            {
                actor=new GameObject("Crew Survivor "+id);actor.transform.SetParent(WorldRoot,false);
                Color[] jackets={new Color(.18f,.27f,.31f),new Color(.36f,.25f,.16f),new Color(.22f,.3f,.22f)};
                HumanoidModelBuilder.Create(actor,HumanoidStyle.Survivor,jackets[remotePlayers.Count%jackets.Length]);remotePlayers[id]=actor;
            }
            Vector3 old=remotePositions.TryGetValue(id,out var previous)?previous:position;
            Vector3 movement=position-old;actor.transform.position=position;
            var visual=actor.GetComponent<HumanoidModelBuilder>();if(visual!=null)visual.Animate(movement,movement.magnitude*6f);
            if(movement.sqrMagnitude>.001f)actor.transform.forward=movement.normalized;
            remotePositions[id]=position;
        }

        public void RemoveRemotePlayer(string id)
        {
            if(remotePlayers.TryGetValue(id,out var actor)){Destroy(actor);remotePlayers.Remove(id);}
            remotePositions.Remove(id);
        }

        public static Color BlockColor(int type)
        {
            switch(type){case 1:return new Color(.55f,.56f,.53f);case 2:return new Color(.27f,.46f,.57f);case 3:return new Color(.54f,.63f,.72f);case 4:return new Color(.2f,.72f,.78f);case 5:return new Color(.57f,.48f,.37f);default:return new Color(.5f,.37f,.24f);}
        }

        static void Tint(GameObject obj,Color color){var renderer=obj.GetComponent<Renderer>();if(renderer){var material=new Material(Shader.Find("Standard"));material.color=color;renderer.sharedMaterial=material;}}
        void FollowCamera(float deltaTime)
        {
            if(!GameCamera||!LocalPlayer)return;
            Vector3 player=LocalPlayer.transform.position,forward=ViewForward;
            Vector3 target=viewMode==ViewMode.Tactical?new Vector3(4,40,38):viewMode==ViewMode.First?player+Vector3.up*1.72f+forward*.15f:player+Vector3.up*3f-forward*4.8f;
            Vector3 focus=viewMode==ViewMode.Tactical?new Vector3(4,0,0):player+Vector3.up*1.7f+forward*(viewMode==ViewMode.First?10f:6f)-Vector3.up*viewPitch*.12f;
            GameCamera.fieldOfView=viewMode==ViewMode.First?68f:viewMode==ViewMode.Third?57f:47f;
            GameCamera.transform.position=Vector3.Lerp(GameCamera.transform.position,target,Mathf.Clamp01(deltaTime*8f));
            GameCamera.transform.LookAt(focus);
        }
    }
}
