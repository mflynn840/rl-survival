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
        }
        public int Damage(int amount){Health=Mathf.Max(0,Health-Mathf.Max(0,amount));return Health;}
        public int Repair(int amount){Health=Mathf.Min(MaxHealth,Health+Mathf.Max(0,amount));return Health;}
    }
}
