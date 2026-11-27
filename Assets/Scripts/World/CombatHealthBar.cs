using UnityEngine;

namespace RLSurvival
{
    /// <summary>World-space health readout shared by survivors and motel raiders.</summary>
    public sealed class CombatHealthBar : MonoBehaviour
    {
        Transform billboard, fill;
        Renderer fillRenderer;
        bool enemy;

        void Awake()
        {
            billboard=new GameObject("Health bar").transform;
            billboard.SetParent(transform,false);
            billboard.localPosition=Vector3.up*2.44f;
            CreateBar("Back",new Vector3(0,0,0),new Vector3(.9f,.12f,.012f),new Color(.11f,.09f,.08f));
            fill=CreateBar("Health",new Vector3(0,0,-.012f),new Vector3(.86f,.075f,.014f),new Color(.35f,.76f,.6f));
            fillRenderer=fill.GetComponent<Renderer>();
        }

        Transform CreateBar(string label,Vector3 position,Vector3 scale,Color color)
        {
            var part=GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name=label;part.transform.SetParent(billboard,false);part.transform.localPosition=position;part.transform.localScale=scale;
            var collider=part.GetComponent<Collider>();if(collider)Destroy(collider);
            var shader=Shader.Find("Standard");
            if(!shader)shader=Shader.Find("Diffuse");
            var material=new Material(shader);material.color=color;
            part.GetComponent<Renderer>().sharedMaterial=material;
            return part.transform;
        }

        public void Set(int health,int maximum,bool isEnemy)
        {
            // EditMode construction and pooled objects can call Set before Awake.
            if(fillRenderer==null)Awake();
            enemy=isEnemy;
            float fraction=Mathf.Clamp01(maximum>0?(float)health/maximum:0f);
            fill.localScale=new Vector3(.86f*fraction,.075f,.014f);
            fill.localPosition=new Vector3(-.43f*(1f-fraction),0,-.012f);
            fillRenderer.material.color=fraction<.3f?new Color(.9f,.26f,.18f):enemy?new Color(.77f,.27f,.22f):new Color(.35f,.76f,.6f);
            billboard.gameObject.SetActive(enemy||fraction<1f);
        }

        void LateUpdate()
        {
            var camera=Camera.main;
            if(camera)billboard.rotation=camera.transform.rotation;
        }
    }
}
