using System.Collections.Generic;
using UnityEngine;

namespace RLSurvival
{
    public sealed class BuildingSystem : MonoBehaviour
    {
        readonly List<FortBlock> blocks=new List<FortBlock>();
        SurvivalGame game;
        public IReadOnlyList<FortBlock> Blocks { get { return blocks; } }
        public void Initialize(SurvivalGame owner){game=owner;}
        public void TickInput()
        {
            for(int index=0;index<UpgradeCatalog.Blocks.Length&&index<8;index++)
                if(Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1+index))&&game.IsUnlocked(UpgradeKind.Block,index))game.SelectedBlock=index;
            if(Input.GetKeyDown(KeyCode.R))RepairNearest();
            if(Input.GetMouseButtonDown(0)&&WorldPointer())
            {
                Ray ray=game.Arena.GameCamera.ScreenPointToRay(Input.mousePosition);var hits=Physics.RaycastAll(ray,120f);
                System.Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
                foreach(var hit in hits)
                {
                    if(!hit.collider.GetComponentInParent<FortBlock>())continue;
                    Vector3 point=hit.point+hit.normal*.51f;
                    point=new Vector3(Mathf.Round(point.x),Mathf.Round(point.y-.5f)+.5f,Mathf.Round(point.z));
                    TryPlace(point,game.SelectedBlock);return;
                }
                var plotPlane=new Plane(Vector3.up,Vector3.zero);float distance;
                if(plotPlane.Raycast(ray,out distance)){Vector3 point=ray.GetPoint(distance);TryPlace(new Vector3(Mathf.Round(point.x),.5f,Mathf.Round(point.z)),game.SelectedBlock);}
            }
        }
        public bool TryPlace(Vector3 position,int type,bool remote=false,bool publish=true)
        {
            type=Mathf.Clamp(type,0,UpgradeCatalog.Blocks.Length-1);
            if(position.x < -13 || position.x > 13 || position.z < 4 || position.z > 24 || position.y < .5f || position.y > 8.5f)return false;
            if(Find(position)!=null)return false;
            if(!remote&&(!game.IsUnlocked(UpgradeKind.Block,type)||!game.TrySpend(UpgradeCatalog.Blocks[type].PlaceCost)))return false;
            Create(position,type,UpgradeCatalog.Blocks[type].Durability,UpgradeCatalog.Blocks[type].Durability);
            if(publish&&game.Connected)
            {
                if(game.IsHost)game.PublishAction("build",position,type);
                else game.SendCommand("input",new GameCommand{action="build",x=position.x,y=position.y,z=position.z,value=type});
            }
            return true;
        }
        public void RepairNearest()
        {
            FortBlock nearest=null;float distance=3.4f;Vector3 player=game.Arena.LocalPlayer.transform.position;
            foreach(var block in blocks){if(block==null||block.Health>=block.MaxHealth)continue;float candidate=Vector3.Distance(player,block.transform.position);if(candidate<distance){distance=candidate;nearest=block;}}
            if(nearest==null)return;
            int cost=game.HasPerk(2)?0:20;if(!game.TrySpend(cost))return;
            if(game.Connected&&!game.IsHost){game.SendCommand("input",new GameCommand{action="repair",x=nearest.transform.position.x,y=nearest.transform.position.y,z=nearest.transform.position.z});return;}
            RepairAt(nearest.transform.position,true);
        }
        public void RepairAt(Vector3 position,bool publish)
        {
            var block=Find(position);if(block==null)return;block.Repair(90);
            if(publish&&game.Connected)game.PublishAction("repair",block.transform.position,90);
        }
        public void DamageAt(Vector3 position,int damage)
        {
            var block=Find(position);if(block==null)return;int health=block.Damage(damage);
            if(game.Connected)game.PublishAction(health<=0?"block_destroy":"damage",block.transform.position,health);
            if(health<=0){blocks.Remove(block);Destroy(block.gameObject);}
        }
        public void SetHealth(Vector3 position,int health){var block=Find(position);if(block!=null)block.Configure(block.Type,health,block.MaxHealth,ProceduralArena.BlockColor(block.Type));}
        public void RemoveAt(Vector3 position){var block=Find(position);if(block!=null){blocks.Remove(block);Destroy(block.gameObject);}}
        public FortBlock Find(Vector3 position)
        {
            foreach(var block in blocks)if(block&&Vector3.Distance(block.transform.position,position)<.35f)return block;
            return null;
        }
        public void Clear(){foreach(var block in blocks)if(block)Destroy(block.gameObject);blocks.Clear();}
        public BlockSnapshot[] Capture()
        {
            var result=new List<BlockSnapshot>();foreach(var block in blocks)if(block)result.Add(new BlockSnapshot{x=block.transform.position.x,y=block.transform.position.y,z=block.transform.position.z,type=block.Type,hp=block.Health,maxHp=block.MaxHealth});return result.ToArray();
        }
        public void Apply(BlockSnapshot[] snapshot)
        {
            var seen=new HashSet<FortBlock>();
            if(snapshot!=null)foreach(var data in snapshot){var position=new Vector3(data.x,data.y>0?data.y:.5f,data.z);var block=Find(position);if(block!=null&&block.Type!=data.type){blocks.Remove(block);Destroy(block.gameObject);block=null;}if(block==null)block=Create(position,data.type,data.hp,data.maxHp);block.Configure(data.type,data.hp,data.maxHp,ProceduralArena.BlockColor(data.type));seen.Add(block);}
            foreach(var block in new List<FortBlock>(blocks))if(block&&!seen.Contains(block)){blocks.Remove(block);Destroy(block.gameObject);}
        }
        FortBlock Create(Vector3 position,int type,int health,int maxHealth)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=UpgradeCatalog.Blocks[type].Name;obj.transform.SetParent(game.Arena.WorldRoot);obj.transform.position=position;obj.transform.localScale=new Vector3(.95f,1,.95f);
            string[] names={"Scrap","Brick","Steel","Titanium","ShockGrid","ReinforcedConcrete","Roof","Spikes"};
            var prefab=Resources.Load<GameObject>("Models/"+names[Mathf.Clamp(type,0,names.Length-1)]);
            if(prefab)
            {
                var visual=Instantiate(prefab,obj.transform);visual.transform.localPosition=Vector3.zero;visual.transform.localRotation=Quaternion.identity;
                var renderer=obj.GetComponent<Renderer>();if(renderer)renderer.enabled=false;
                if(type==6)
                {
                    var box=obj.GetComponent<BoxCollider>();if(box)box.enabled=false;
                    foreach(var filter in visual.GetComponentsInChildren<MeshFilter>())
                    {
                        if(!filter.sharedMesh)continue;
                        var collider=filter.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=filter.sharedMesh;
                    }
                }
            }
            var block=obj.AddComponent<FortBlock>();block.Configure(type,health,maxHealth,ProceduralArena.BlockColor(type));blocks.Add(block);return block;
        }
        bool WorldPointer(){var point=Input.mousePosition;float guiY=Screen.height-point.y;var hud=game.GetComponent<SurvivalHud>();if(point.x<=310||point.y>=Screen.height-150||point.x>=Screen.width-150&&guiY<55||hud!=null&&hud.SettingsOpen)return false;return true;}
    }
}
