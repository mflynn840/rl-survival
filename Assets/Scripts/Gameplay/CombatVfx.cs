using System.Collections.Generic;
using UnityEngine;

namespace RLSurvival
{
    /// <summary>Pooled 3D projectiles, muzzle flashes and blood particles for combat.</summary>
    public sealed class CombatVfx : MonoBehaviour
    {
        sealed class Bullet
        {
            public GameObject object3D;
            public Vector3 from,to;
            public float elapsed,duration;
            public bool hit;
        }

        readonly List<Bullet> active=new List<Bullet>();
        readonly Queue<Bullet> available=new Queue<Bullet>();
        ParticleSystem blood,flash;

        public void Initialize()
        {
            var gunmetal=new Material(Shader.Find("Standard"));gunmetal.color=new Color(1f,.72f,.27f);gunmetal.EnableKeyword("_EMISSION");gunmetal.SetColor("_EmissionColor",new Color(1f,.42f,.08f));
            for(int i=0;i<48;i++)
            {
                var object3D=GameObject.CreatePrimitive(PrimitiveType.Sphere);
                object3D.name="Pooled bullet";object3D.transform.localScale=Vector3.one*.09f;
                var collider=object3D.GetComponent<Collider>();if(collider)Destroy(collider);
                object3D.GetComponent<Renderer>().sharedMaterial=gunmetal;
                object3D.SetActive(false);available.Enqueue(new Bullet{object3D=object3D});
            }
            blood=Particles("Blood impact",new Color(.65f,.035f,.025f),.47f,2.6f,.06f);
            flash=Particles("Muzzle flash",new Color(1f,.68f,.23f),.12f,.8f,.13f);
        }

        ParticleSystem Particles(string label,Color color,float lifetime,float speed,float size)
        {
            var host=new GameObject(label);host.transform.SetParent(transform,false);
            var system=host.AddComponent<ParticleSystem>();var main=system.main;main.playOnAwake=false;main.loop=false;
            main.simulationSpace=ParticleSystemSimulationSpace.World;main.startLifetime=lifetime;
            main.startSpeed=speed;main.startSize=size;main.startColor=color;main.gravityModifier=.7f;
            var emission=system.emission;emission.enabled=false;
            var shape=system.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.06f;
            var renderer=system.GetComponent<ParticleSystemRenderer>();renderer.material=new Material(Shader.Find("Standard"));
            return system;
        }

        public void PlayShot(Vector3 origin,Vector3 target,bool hit,bool grenade)
        {
            if(available.Count==0)return;
            var bullet=available.Dequeue();bullet.from=origin;bullet.to=target;bullet.elapsed=0;
            bullet.duration=grenade?.35f:.12f;bullet.hit=hit;bullet.object3D.transform.position=origin;
            bullet.object3D.transform.localScale=Vector3.one*(grenade?.22f:.09f);bullet.object3D.SetActive(true);active.Add(bullet);
            flash.transform.position=origin;if(!flash.isPlaying)flash.Play();flash.Emit(5);
        }
        public void PlayBlood(Vector3 position){blood.transform.position=position;if(!blood.isPlaying)blood.Play();blood.Emit(13);}

        void Update()
        {
            for(int index=active.Count-1;index>=0;index--)
            {
                var bullet=active[index];bullet.elapsed+=Time.deltaTime;
                float progress=Mathf.Clamp01(bullet.elapsed/bullet.duration);
                bullet.object3D.transform.position=Vector3.Lerp(bullet.from,bullet.to,progress);
                if(progress<1f)continue;
                if(bullet.hit)PlayBlood(bullet.to);
                bullet.object3D.SetActive(false);active.RemoveAt(index);available.Enqueue(bullet);
            }
        }
    }
}
