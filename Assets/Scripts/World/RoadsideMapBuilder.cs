using UnityEngine;

namespace RLSurvival
{
    /// <summary>Creates the 3D roadside motel, public road, parking court, and the crew's build plot.</summary>
    public sealed class RoadsideMapBuilder : MonoBehaviour
    {
        public Vector3[] HotelExitPoints { get; private set; }
        readonly System.Collections.Generic.List<Transform> doorHinges=new System.Collections.Generic.List<Transform>();
        readonly System.Collections.Generic.List<float> doorOpenUntil=new System.Collections.Generic.List<float>();

        void Awake()
        {
            HotelExitPoints=new[]
            {
                new Vector3(-7.75f,0f,-13.8f), new Vector3(-3.75f,0f,-13.8f), new Vector3(.25f,0f,-13.8f),
                new Vector3(4.25f,0f,-13.8f), new Vector3(8.25f,0f,-13.8f)
            };
        }

        readonly System.Collections.Generic.Dictionary<Color,Material> materials = new System.Collections.Generic.Dictionary<Color,Material>();
        readonly System.Collections.Generic.List<GameObject> mediumDetail=new System.Collections.Generic.List<GameObject>();
        readonly System.Collections.Generic.List<GameObject> highDetail=new System.Collections.Generic.List<GameObject>();
        Transform root;

        public void Build(Transform parent)
        {
            root = new GameObject("Roadside Motel District").transform;
            root.SetParent(parent, false);
            BuildGroundAndPlot();
            BuildRoad();
            BuildMotel();
            BuildParkingCourt();
            BuildLandscaping();
            AddMotelLighting();
        }
        void AddMotelLighting()
        {
            PointLight("Warm motel entrance light",new Vector3(-3f,3.25f,-12.5f),VisualStyle.MotelWarmth,8f,14f);
            PointLight("Warm motel entrance light",new Vector3(3f,3.25f,-12.5f),VisualStyle.MotelWarmth,8f,14f);
            PointLight("Paradise sign glow",new Vector3(11.2f,9.5f,-15.2f),VisualStyle.MotelWarmth,16f,24f);
            for(int x=-10;x<=10;x+=5)PointLight("Porch sconce",new Vector3(x,2.9f,-13.2f),VisualStyle.MotelWarmth,3.2f,5.5f);
        }
        void PointLight(string label,Vector3 position,Color color,float intensity,float range)
        {
            var light=new GameObject(label).AddComponent<Light>();light.transform.SetParent(root,false);light.transform.localPosition=position;light.type=LightType.Point;light.color=color;light.intensity=intensity;light.range=range;light.shadows=LightShadows.None;
        }
        public void OpenDoor(int index)
        {
            if(index>=0&&index<doorOpenUntil.Count)doorOpenUntil[index]=Time.time+1.3f;
        }
        void Update()
        {
            for(int i=0;i<doorHinges.Count;i++)if(doorHinges[i])doorHinges[i].localRotation=Quaternion.Slerp(doorHinges[i].localRotation,Quaternion.Euler(0,Time.time<doorOpenUntil[i]?-72f:0f,0),Time.deltaTime*9f);
        }

        void BuildGroundAndPlot()
        {
            Box("District ground", new Vector3(0,-.14f,0), new Vector3(82,.28f,66), new Color(.28f,.32f,.25f));
            Box("Crew build plot", new Vector3(0,.012f,14.25f), new Vector3(29,.035f,22), new Color(.34f,.37f,.28f));
            Box("Plot north boundary", new Vector3(0,.055f,25.25f), new Vector3(29,.08f,.16f), new Color(.78f,.68f,.48f));
            Box("Plot west boundary", new Vector3(-14.42f,.055f,14.25f), new Vector3(.16f,.08f,22), new Color(.78f,.68f,.48f));
            Box("Plot east boundary", new Vector3(14.42f,.055f,14.25f), new Vector3(.16f,.08f,22), new Color(.78f,.68f,.48f));
            for (int x=-12; x<=12; x+=3) Box("Plot guide", new Vector3(x,.035f,14.25f), new Vector3(.025f,.02f,21.6f), new Color(.44f,.45f,.34f));
            for (int z=5; z<=24; z+=3) Box("Plot guide", new Vector3(0,.035f,z), new Vector3(28.8f,.02f,.025f), new Color(.44f,.45f,.34f));
            Box("Crew plot sign", new Vector3(-12.6f,1.05f,4f), new Vector3(.18f,2f,.18f), new Color(.25f,.23f,.19f));
            Box("Crew plot sign board", new Vector3(-12.6f,2.15f,4f), new Vector3(3.2f,.8f,.18f), new Color(.12f,.18f,.17f));
            Text("CREW BUILD PLOT", new Vector3(-12.6f,2.06f,4.105f), 0f, 3.2f, new Color(.86f,.8f,.62f));
        }

        void BuildRoad()
        {
            Box("Two lane asphalt road", new Vector3(0,.022f,-3.5f), new Vector3(82,.045f,9f), new Color(.16f,.17f,.17f));
            Box("Road center yellow line", new Vector3(0,.051f,-3.5f), new Vector3(82,.018f,.11f), new Color(.89f,.67f,.12f));
            Box("Road center yellow line", new Vector3(0,.051f,-3.78f), new Vector3(82,.018f,.045f), new Color(.89f,.67f,.12f));
            Box("Road north edge line", new Vector3(0,.051f,.95f), new Vector3(82,.018f,.075f), new Color(.83f,.82f,.73f));
            Box("Road south edge line", new Vector3(0,.051f,-7.95f), new Vector3(82,.018f,.075f), new Color(.83f,.82f,.73f));
            for(int i=-38;i<=38;i+=8) Box("Broken lane marker",new Vector3(i,.053f,-1.65f),new Vector3(3.2f,.018f,.065f),new Color(.79f,.78f,.7f));
            GroundText("STOP",new Vector3(0,.058f,-6.8f),2.8f,new Color(.84f,.82f,.75f));
            Box("Motel driveway", new Vector3(0,.009f,-9.9f), new Vector3(27,.025f,3.8f), new Color(.23f,.24f,.22f));
            Box("Plot driveway", new Vector3(0,.009f,2.25f), new Vector3(23,.025f,2.2f), new Color(.23f,.24f,.22f));
            for(int x=-10;x<=10;x+=2) Box("Crosswalk stripe",new Vector3(x,.052f,.8f),new Vector3(.85f,.02f,.25f),new Color(.8f,.79f,.72f));
        }

        void BuildMotel()
        {
            const float motelZ=-18.2f;
            Box("Motel foundation",new Vector3(0,.22f,motelZ),new Vector3(23,.42f,9.4f),new Color(.38f,.35f,.3f));
            Box("Motel lower floor",new Vector3(0,1.95f,motelZ),new Vector3(22.5f,3.1f,8.9f),new Color(.42f,.34f,.27f));
            Box("Motel upper floor",new Vector3(0,5.02f,motelZ),new Vector3(22.5f,2.95f,8.9f),new Color(.34f,.28f,.24f));
            GableRoof("Motel gable roof",new Vector3(0,6.48f,motelZ),23f,9.6f,2.35f,new Color(.19f,.18f,.17f));
            Box("Motel front porch canopy",new Vector3(0,3.32f,-12.75f),new Vector3(23.5f,.28f,2.7f),new Color(.22f,.2f,.18f));
            for(int x=-10;x<=10;x+=5) Box("Porch timber post",new Vector3(x,1.7f,-11.55f),new Vector3(.23f,3.2f,.23f),new Color(.25f,.2f,.15f));
            Box("Upper gallery floor",new Vector3(0,3.62f,-13.05f),new Vector3(22.4f,.2f,1.6f),new Color(.28f,.26f,.23f));
            Box("Gallery front handrail",new Vector3(0,4.05f,-12.25f),new Vector3(22.3f,.11f,.11f),new Color(.62f,.58f,.49f));
            for(int x=-10;x<=10;x+=2) Box("Gallery baluster",new Vector3(x,3.86f,-12.25f),new Vector3(.075f,.48f,.075f),new Color(.62f,.58f,.49f));

            for(int x=-9;x<=9;x+=4)
            {
                Window(x,1.95f,-13.69f);
                Door(x+1.25f,1.22f,-13.67f);
                Window(x,5.02f,-13.69f);
                Door(x+1.25f,4.42f,-13.67f);
                Box("Room number plaque",new Vector3(x+1.25f,5.82f,-13.66f),new Vector3(.45f,.22f,.045f),new Color(.73f,.67f,.53f));
            }

            Box("Motel entrance fascia",new Vector3(0,6.12f,-13.68f),new Vector3(6.6f,.65f,.22f),new Color(.24f,.19f,.15f));
            Text("PARADISE MOTEL",new Vector3(0,6.12f,-13.54f),0f,4.8f,new Color(.94f,.77f,.42f));
            Box("High motel sign pole",new Vector3(11.2f,5f,-17f),new Vector3(.28f,10f,.28f),new Color(.2f,.19f,.17f));
            Box("High motel sign",new Vector3(11.2f,10.2f,-17f),new Vector3(3.5f,2.6f,.3f),new Color(.12f,.14f,.14f));
            Text("PARADISE\nMOTEL",new Vector3(11.2f,10.55f,-16.82f),0f,2.6f,new Color(1f,.8f,.43f));
            for(int z=-20;z<=-15;z+=2) Box("Motel side stair",new Vector3(11.8f,1.1f+(z+20)*.32f,z),new Vector3(2.1f,.2f,.7f),new Color(.28f,.27f,.25f));
            Box("Motel stair rail",new Vector3(10.8f,2.1f,-16.8f),new Vector3(.09f,1.8f,4.8f),new Color(.55f,.52f,.45f),new Vector3(25,0,0));

            // Side wings and balcony doors give the roadside block the scale of a two-story motel.
            Box("Motel west wing",new Vector3(-11.55f,2.7f,-18.2f),new Vector3(.55f,5.2f,8.9f),new Color(.3f,.26f,.23f));
            Box("Motel east wing",new Vector3(11.55f,2.7f,-18.2f),new Vector3(.55f,5.2f,8.9f),new Color(.3f,.26f,.23f));
        }

        void BuildParkingCourt()
        {
            Box("Motel parking court",new Vector3(0,.007f,-10.05f),new Vector3(29,.02f,4.2f),new Color(.25f,.25f,.23f));
            for(int x=-12;x<=12;x+=4) Box("Parking bay line",new Vector3(x,.023f,-10.1f),new Vector3(.06f,.015f,3.2f),new Color(.79f,.77f,.67f));
            Vehicle("Blue parked car",new Vector3(-9,.15f,-9.75f),new Color(.16f,.31f,.39f));
            Vehicle("Red parked car",new Vector3(-3,.15f,-9.75f),new Color(.45f,.18f,.14f));
            Vehicle("Cream parked car",new Vector3(5,.15f,-9.75f),new Color(.7f,.66f,.53f));
            Vehicle("Olive parked car",new Vector3(10,.15f,-9.75f),new Color(.31f,.34f,.24f));
            Box("Room side dumpster",new Vector3(-12, .65f,-13.5f),new Vector3(1.7f,1.1f,1.3f),new Color(.2f,.27f,.23f));
            for(int i=0;i<9;i++)
            {
                float x=-13f+i*3.2f;
                Sphere("Parking court shrub",new Vector3(x,.35f,-7.6f),new Vector3(1.3f,.72f,.9f),i%2==0?new Color(.22f,.28f,.18f):new Color(.31f,.32f,.19f));
            }
        }

        void BuildLandscaping()
        {
            for(int x=-36;x<=36;x+=9)
            {
                if(Mathf.Abs(x)<17)continue;
                Palm(new Vector3(x,0,-8f),x%18==0?"tree_palmTall":"tree_palmBend");
                Palm(new Vector3(x+3f,0,3f),"tree_palmTall");
            }
            for(int i=-4;i<=4;i++)
            {
                Tree(new Vector3(-31f+i*2.3f,0,-26f-Mathf.Abs(i)%2*2f),i%2==0?"tree_pineTallA_detailed":"tree_pineRoundB");
                Tree(new Vector3(31f+i*2.3f,0,-23f-Mathf.Abs(i)%2*2f),i%2==0?"tree_oak":"tree_pineRoundB");
                Tree(new Vector3(-31f+i*2.3f,0,27f),"tree_oak");
                Tree(new Vector3(31f+i*2.3f,0,27f),"tree_pineTallA_detailed");
            }
            for(int i=0;i<18;i++)
            {
                float x=-29f+i*3.4f;
                string bush=i%3==0?"plant_bushDetailed":i%3==1?"plant_bushLarge":"plant_bushTriangle";
                Community(bush,new Vector3(x,0,-6.3f+(i%2)*.7f),Vector3.one*(i%3==1?1.15f:.85f),new Vector3(0,i*29f,0),1);
                if(i%2==0)Community(i%4==0?"rock_largeA":"rock_smallC",new Vector3(x+.7f,0,-5.4f),Vector3.one*(i%4==0?.75f:.5f),new Vector3(0,i*47f,0),1);
            }
            for(int i=0;i<42;i++)
            {
                float x=-38f+(i*7.1f)%76f,z=i%2==0?-1.7f+(i%5)*1.1f:25.8f+(i%4)*1.4f;
                Community(i%3==0?"grass_large":i%3==1?"grass_leafsLarge":"flower_yellowA",new Vector3(x,0,z),Vector3.one*(.42f+(i%3)*.08f),new Vector3(0,i*19f,0),2);
            }
            for(int i=0;i<16;i++)
            {
                float x=-35f+i*4.6f;
                Community(i%2==0?"fence_simpleLow":"fence_simpleHigh",new Vector3(x,0,29.8f),Vector3.one,new Vector3(0,90,0),1);
            }
            Community("log_stack",new Vector3(-11f,0,-8.1f),Vector3.one,new Vector3(0,18,0),1);
        }

        void Window(float x,float y,float z)
        {
            Box("Room window trim",new Vector3(x,y,z),new Vector3(1.5f,1.2f,.12f),new Color(.19f,.18f,.16f));
            Box("Room window glass",new Vector3(x,y,z+.075f),new Vector3(1.24f,.94f,.035f),new Color(.3f,.43f,.45f));
            Box("Window divider",new Vector3(x,y,z+.1f),new Vector3(.06f,.96f,.04f),new Color(.64f,.6f,.5f));
        }

        void Door(float x,float y,float z)
        {
            Box("Motel room door frame",new Vector3(x,y,z),new Vector3(1.1f,2.35f,.16f),new Color(.18f,.16f,.14f));
            var hinge=new GameObject("Motel door hinge").transform;hinge.SetParent(root,false);hinge.localPosition=new Vector3(x-.43f,y,z+.1f);
            Box(hinge,"Motel room door",new Vector3(.43f,0,0),new Vector3(.86f,2.1f,.08f),new Color(.35f,.24f,.17f));
            Box(hinge,"Door knob",new Vector3(.72f,0,.07f),new Vector3(.09f,.09f,.06f),new Color(.78f,.62f,.28f));
            if(y<3f){doorHinges.Add(hinge);doorOpenUntil.Add(0);}
        }

        void Vehicle(string label,Vector3 position,Color paint)
        {
            var car=new GameObject(label).transform;car.SetParent(root,false);car.localPosition=position;
            Box(car,"Car lower body",new Vector3(0,.45f,0),new Vector3(3.5f,.72f,1.55f),paint);
            Box(car,"Car cabin",new Vector3(-.15f,1.0f,0),new Vector3(1.85f,.68f,1.34f),new Color(.18f,.22f,.23f));
            Box(car,"Car windshield",new Vector3(.8f,1f,0),new Vector3(.04f,.5f,1.2f),new Color(.35f,.48f,.5f));
            Box(car,"Car front bumper",new Vector3(1.82f,.3f,0),new Vector3(.14f,.2f,1.5f),new Color(.49f,.48f,.43f));
            for(int side=-1;side<=1;side+=2)for(int axle=-1;axle<=1;axle+=2)
                Cylinder(car,"Car tire",new Vector3(axle*1.1f,.27f,side*.81f),new Vector3(.28f,.14f,.28f),new Color(.06f,.065f,.06f),new Vector3(90,0,0));
        }

        void Palm(Vector3 position,string asset)
        {
            if(Community(asset,position,Vector3.one,new Vector3(0,Random.Range(0,360),0),0))return;
            Cylinder(root,"Palm trunk",position+Vector3.up*3.4f,new Vector3(.55f,3.4f,.55f),new Color(.4f,.31f,.2f));
            for(int i=0;i<7;i++)
            {
                float angle=i*51.4f;
                var leaf=Box("Palm frond",position+Vector3.up*6.2f+new Vector3(Mathf.Sin(angle*Mathf.Deg2Rad)*1.2f,.08f,Mathf.Cos(angle*Mathf.Deg2Rad)*1.2f),new Vector3(.27f,.1f,3.2f),new Color(.17f,.29f,.17f),new Vector3(-18,angle,0));
                leaf.transform.localScale=new Vector3(.28f,.11f,3.7f);
            }
        }

        void Tree(Vector3 position,string asset)
        {
            if(Community(asset,position,Vector3.one,new Vector3(0,Random.Range(0,360),0),0))return;
            Cylinder(root,"Pine trunk",position+Vector3.up*3f,new Vector3(.75f,3f,.75f),new Color(.27f,.2f,.14f));
            for(int i=0;i<3;i++)
                Cone("Pine canopy",position+Vector3.up*(3.1f+i*1.55f),new Vector3(5.2f-i*.75f,3.3f,5.2f-i*.75f),new Color(.12f+i*.018f,.22f+i*.018f,.15f));
        }

        GameObject Community(string asset,Vector3 position,Vector3 scale,Vector3 euler,int detailTier)
        {
            var prefab=Resources.Load<GameObject>("Community/KenneyNature/"+asset);if(!prefab)return null;
            var obj=Instantiate(prefab,root);obj.name="Kenney Nature • "+asset;obj.transform.localPosition=position;obj.transform.localRotation=Quaternion.Euler(euler);obj.transform.localScale=scale;
            if(detailTier==1)mediumDetail.Add(obj);else if(detailTier>=2)highDetail.Add(obj);
            return obj;
        }

        public void SetDetailLevel(int preset)
        {
            bool showMedium=preset>=1,showHigh=preset>=2;
            foreach(var item in mediumDetail)if(item)item.SetActive(showMedium);
            foreach(var item in highDetail)if(item)item.SetActive(showHigh);
        }

        void GableRoof(string label,Vector3 center,float width,float depth,float rise,Color color)
        {
            var mesh=new Mesh { name=label+" mesh" };
            float x=width*.5f,z=depth*.5f,eave=0f;
            mesh.vertices=new[]
            {
                new Vector3(-x,eave,-z),new Vector3(-x,eave,z),new Vector3(-x,rise*.55f,0),
                new Vector3(x,eave,-z),new Vector3(x,eave,z),new Vector3(x,rise*.55f,0)
            };
            mesh.triangles=new[]{0,1,2,3,5,4,0,5,3,0,2,5,2,4,5,2,1,4};
            mesh.RecalculateNormals();mesh.RecalculateBounds();
            var roof=new GameObject(label);roof.transform.SetParent(root,false);roof.transform.localPosition=center;
            roof.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=roof.AddComponent<MeshRenderer>();renderer.sharedMaterial=Material(color);
        }

        GameObject Cone(string label,Vector3 position,Vector3 scale,Color color)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cylinder);obj.name=label;obj.transform.SetParent(root,false);obj.transform.localPosition=position;obj.transform.localScale=scale;
            var collider=obj.GetComponent<Collider>();if(collider)Destroy(collider);
            obj.GetComponent<Renderer>().sharedMaterial=Material(color);return obj;
        }
        GameObject Box(string label,Vector3 position,Vector3 scale,Color color,Vector3? rotation=null){return Box(root,label,position,scale,color,rotation);}
        GameObject Box(Transform parent,string label,Vector3 position,Vector3 scale,Color color,Vector3? rotation=null)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=label;obj.transform.SetParent(parent,false);obj.transform.localPosition=position;obj.transform.localScale=scale;
            if(rotation.HasValue)obj.transform.localRotation=Quaternion.Euler(rotation.Value);
            var collider=obj.GetComponent<Collider>();if(collider)Destroy(collider);var renderer=obj.GetComponent<Renderer>();renderer.sharedMaterial=Material(color);ApplyArtTexture(renderer,label);return obj;
        }
        static void ApplyArtTexture(Renderer renderer,string label)
        {
            string name=null;Vector2 repeat=Vector2.one;
            switch(label)
            {
                case "District ground":name="GroundDirt";repeat=new Vector2(24,18);break;
                case "Crew build plot":name="GroundGrass";repeat=new Vector2(12,10);break;
                case "Two lane asphalt road":name="RoadAsphalt";repeat=new Vector2(30,4);break;
                case "Motel foundation":name="MotelStone";repeat=new Vector2(14,2);break;
                case "Motel lower floor":case "Motel upper floor":name="MotelPlaster";repeat=new Vector2(14,3);break;
                case "Motel front porch canopy":case "Upper gallery floor":name="MotelTimber";repeat=new Vector2(14,2);break;
            }
            if(string.IsNullOrEmpty(name))return;var texture=Resources.Load<Texture2D>("Art/"+name);if(!texture)return;
            var material=renderer.material;material.mainTexture=texture;material.mainTextureScale=repeat;material.color=Color.white;
        }
        GameObject Sphere(string label,Vector3 position,Vector3 scale,Color color)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Sphere);obj.name=label;obj.transform.SetParent(root,false);obj.transform.localPosition=position;obj.transform.localScale=scale;
            var collider=obj.GetComponent<Collider>();if(collider)Destroy(collider);obj.GetComponent<Renderer>().sharedMaterial=Material(color);return obj;
        }
        GameObject Cylinder(Transform parent,string label,Vector3 position,Vector3 scale,Color color,Vector3? rotation=null)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cylinder);obj.name=label;obj.transform.SetParent(parent,false);obj.transform.localPosition=position;obj.transform.localScale=scale;
            if(rotation.HasValue)obj.transform.localRotation=Quaternion.Euler(rotation.Value);
            var collider=obj.GetComponent<Collider>();if(collider)Destroy(collider);obj.GetComponent<Renderer>().sharedMaterial=Material(color);return obj;
        }
        void Text(string text,Vector3 position,float rotationY,float size,Color color)
        {
            var obj=new GameObject("3D Motel Sign Text");obj.transform.SetParent(root,false);obj.transform.localPosition=position;obj.transform.localRotation=Quaternion.Euler(0,rotationY,0);
            var mesh=obj.AddComponent<TextMesh>();mesh.text=text;mesh.anchor=TextAnchor.MiddleCenter;mesh.alignment=TextAlignment.Center;mesh.fontSize=80;mesh.characterSize=size/Mathf.Max(4f,text.Replace("\n","").Length*.62f);mesh.color=color;
            mesh.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var renderer=obj.GetComponent<MeshRenderer>();renderer.sharedMaterial=Material(color);
        }
        void GroundText(string text,Vector3 position,float width,Color color)
        {
            var obj=new GameObject("Road paint text");obj.transform.SetParent(root,false);obj.transform.localPosition=position;obj.transform.localRotation=Quaternion.Euler(-90,0,0);
            var mesh=obj.AddComponent<TextMesh>();mesh.text=text;mesh.anchor=TextAnchor.MiddleCenter;mesh.alignment=TextAlignment.Center;mesh.fontSize=80;mesh.characterSize=width/Mathf.Max(4f,text.Length*.62f);mesh.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");mesh.color=color;
            obj.GetComponent<MeshRenderer>().sharedMaterial=Material(color);
        }
        Material Material(Color color)
        {
            if(materials.TryGetValue(color,out var material))return material;
            material=new Material(Shader.Find("Standard"));material.color=color;materials[color]=material;return material;
        }
    }
}
