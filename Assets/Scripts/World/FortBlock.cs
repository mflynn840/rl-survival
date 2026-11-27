using UnityEngine;

namespace RLSurvival
{
    public sealed class FortBlock : MonoBehaviour
    {
        public int Type { get; private set; }
        public int Health { get; private set; }
        public int MaxHealth { get; private set; }
        public void Configure(int type,int health,int maxHealth,Color color)
        {
            Type=type;Health=health;MaxHealth=maxHealth;
            var renderer=GetComponent<Renderer>();if(renderer)renderer.material.color=color;
            string[] art={"BlockScrap","BlockBrick","BlockSteel","BlockTitanium","BlockShock","BlockConcrete","BlockRoof","BlockSpikes"};
            var texture=Resources.Load<Texture2D>("Art/"+art[Mathf.Clamp(type,0,art.Length-1)]);
            foreach(var part in GetComponentsInChildren<Renderer>(true))
            {
                var material=part.material;if(texture){material.mainTexture=texture;material.mainTextureScale=Vector2.one;material.color=Color.white;}
                material.SetFloat("_Metallic",(type==2||type==3) ? .72f : (type==4 ? .48f : .08f));
                material.SetFloat("_Glossiness",(type==2||type==3)?.46f:.18f);
                if(type==4){material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",new Color(.04f,.5f,.66f));}
                else material.DisableKeyword("_EMISSION");
            }
        }
        public int Damage(int amount){Health=Mathf.Max(0,Health-Mathf.Max(0,amount));return Health;}
        public int Repair(int amount){Health=Mathf.Min(MaxHealth,Health+Mathf.Max(0,amount));return Health;}
    }
}
