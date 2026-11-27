using System.Collections.Generic;
using UnityEngine;

namespace RLSurvival
{
    public enum HumanoidStyle { Survivor, SuitedRaider }

    /// <summary>Instantiates the editable Blender character rigs and animates their named joints.</summary>
    public sealed class HumanoidModelBuilder : MonoBehaviour
    {
        static readonly Dictionary<Color,Material> modelMaterials=new Dictionary<Color,Material>();
        Transform leftArm, rightArm, leftLeg, rightLeg, rig, gunMount;
        float gait;
        float stride;
        float attackUntil;
        bool raider;
        bool communityCharacter;
        GameObject heldGun;
        static readonly string[] gunModels={"Carbine","Shotgun","Railgun","SMG","GrenadeLauncher","LMG"};

        public static HumanoidModelBuilder Create(GameObject actor, HumanoidStyle style, Color jacketTint)
        {
            var model = actor.AddComponent<HumanoidModelBuilder>();
            model.Build(style, jacketTint);
            actor.AddComponent<CombatHealthBar>().Set(100,100,style==HumanoidStyle.SuitedRaider);
            return model;
        }

        public void Animate(Vector3 movement, float speed)
        {
            stride = Mathf.MoveTowards(stride, movement.sqrMagnitude > .0025f ? Mathf.Clamp01(speed / 5f) : 0f, Time.deltaTime * 4f);
            gait += Time.deltaTime * (5f + stride * 8f);
            float swing = Mathf.Sin(gait) * 34f * stride;
            if(leftLeg)leftLeg.localRotation = Quaternion.Euler(swing, 0f, 0f);
            if(rightLeg)rightLeg.localRotation = Quaternion.Euler(-swing, 0f, 0f);
            if(communityCharacter)
            {
                if(leftArm)leftArm.localRotation=Quaternion.Euler(-swing*.25f,0,58f+swing*.18f);
                if(rightArm)rightArm.localRotation=Quaternion.Euler(Time.time<attackUntil?-105f:swing*.25f,0,-58f-swing*.18f);
            }
            else
            {
                if(leftArm)leftArm.localRotation = Quaternion.Euler((raider?0f:-52f)-swing * .3f, 0f, -5f);
                if(rightArm)rightArm.localRotation = Quaternion.Euler(Time.time<attackUntil?-105f:(raider?0f:-52f)+swing * .3f, 0f, 5f);
            }
            rig.localPosition = Vector3.up * (Mathf.Abs(Mathf.Sin(gait * 2f)) * .045f * stride+Mathf.Sin(Time.time*2f)*.018f);
        }

        public void Attack(){attackUntil=Time.time+.36f;}
        public void SetMeleeWeapon(bool knife)
        {
            if(!raider)return;
            foreach(var renderer in rig.GetComponentsInChildren<Renderer>(true))if(renderer.name.ToLowerInvariant().Contains("knife"))renderer.enabled=knife;
        }
        public void SetWeapon(int index)
        {
            if(raider||gunMount==null)return;
            if(heldGun)Destroy(heldGun);
            var prefab=Resources.Load<GameObject>("Models/"+gunModels[Mathf.Clamp(index,0,gunModels.Length-1)]);
            if(!prefab)return;
            heldGun=Instantiate(prefab,gunMount);heldGun.transform.localPosition=Vector3.zero;heldGun.transform.localRotation=Quaternion.identity;
        }
        public Vector3 MuzzlePosition
        {
            get
            {
                if(heldGun){var muzzle=FindPart(heldGun.transform,"Muzzle");if(muzzle)return muzzle.position;}
                return transform.position+transform.forward*.8f+Vector3.up*1.25f;
            }
        }
        static Transform FindPart(Transform root,string name)
        {
            foreach(var part in root.GetComponentsInChildren<Transform>(true))if(part.name==name)return part;
            return null;
        }

        void Build(HumanoidStyle style, Color jacketTint)
        {
            rig = new GameObject("3D Character Rig").transform;
            rig.SetParent(transform, false);
            bool suit = style == HumanoidStyle.SuitedRaider;
            raider=suit;
            var community=Resources.Load<GameObject>(suit?"Community/KayKit/Barbarian":"Community/KayKit/RogueHooded");
            if(community)
            {
                communityCharacter=true;
                var instance=Instantiate(community,rig);instance.name=suit?"KayKit Raider":"KayKit Survivor";instance.transform.localPosition=Vector3.zero;instance.transform.localRotation=Quaternion.Euler(0,180,0);instance.transform.localScale=Vector3.one*.96f;
                ApplyCommunityPalette(instance.transform,suit,jacketTint);
                leftArm=FindPart(instance.transform,"upperarm.l");rightArm=FindPart(instance.transform,"upperarm.r");
                leftLeg=FindPart(instance.transform,"upperleg.l");rightLeg=FindPart(instance.transform,"upperleg.r");
                gunMount=FindPart(instance.transform,"handslot.r");
                if(!suit)
                {
                    if(!gunMount){gunMount=new GameObject("GunMount").transform;gunMount.SetParent(rig,false);gunMount.localPosition=new Vector3(.34f,1.15f,.22f);gunMount.localRotation=Quaternion.Euler(-68,0,0);}
                    SetWeapon(0);
                }
                return;
            }
            var authored=Resources.Load<GameObject>(suit?"Models/Raider":"Models/Survivor");
            if(authored)
            {
                var instance=Instantiate(authored,rig);instance.transform.localPosition=Vector3.zero;instance.transform.localRotation=Quaternion.identity;
                leftArm=FindPart(instance.transform,"LeftArm");rightArm=FindPart(instance.transform,"RightArm");
                leftLeg=FindPart(instance.transform,"LeftLeg");rightLeg=FindPart(instance.transform,"RightLeg");
                gunMount=FindPart(instance.transform,"GunMount");
                if(!suit)SetWeapon(0);
                return;
            }
            Color jacket = suit ? new Color(.16f,.17f,.19f) : jacketTint;
            Color trousers = suit ? new Color(.09f,.1f,.12f) : new Color(.12f,.16f,.19f);
            Color skin = new Color(.64f,.43f,.31f);
            Color shoes = new Color(.08f,.07f,.065f);

            Part(PrimitiveType.Capsule, "Jacket torso", new Vector3(0,1.12f,0), new Vector3(.57f,.62f,.34f), jacket);
            Part(PrimitiveType.Capsule, "Neck", new Vector3(0,1.62f,.015f), new Vector3(.16f,.15f,.16f), skin);
            Part(PrimitiveType.Sphere, "Head", new Vector3(0,1.82f,.03f), new Vector3(.38f,.46f,.34f), skin);
            Part(PrimitiveType.Sphere, "Hair", new Vector3(0,2.015f,-.005f), new Vector3(.39f,.23f,.36f), suit?new Color(.12f,.09f,.075f):new Color(.12f,.085f,.055f));
            Part(PrimitiveType.Capsule, "Left sleeve", new Vector3(-.39f,1.2f,0), new Vector3(.18f,.48f,.18f), jacket);
            Part(PrimitiveType.Capsule, "Right sleeve", new Vector3(.39f,1.2f,0), new Vector3(.18f,.48f,.18f), jacket);
            leftArm = rig.Find("Left sleeve");rightArm = rig.Find("Right sleeve");
            Part(PrimitiveType.Sphere, "Left hand", new Vector3(-.39f,.89f,.02f), new Vector3(.14f,.16f,.14f), skin);
            Part(PrimitiveType.Sphere, "Right hand", new Vector3(.39f,.89f,.02f), new Vector3(.14f,.16f,.14f), skin);
            Part(PrimitiveType.Capsule, "Left trouser leg", new Vector3(-.16f,.52f,0), new Vector3(.2f,.52f,.22f), trousers);
            Part(PrimitiveType.Capsule, "Right trouser leg", new Vector3(.16f,.52f,0), new Vector3(.2f,.52f,.22f), trousers);
            leftLeg = rig.Find("Left trouser leg");rightLeg = rig.Find("Right trouser leg");
            Part(PrimitiveType.Cube, "Left boot", new Vector3(-.16f,.095f,.07f), new Vector3(.2f,.16f,.36f), shoes);
            Part(PrimitiveType.Cube, "Right boot", new Vector3(.16f,.095f,.07f), new Vector3(.2f,.16f,.36f), shoes);

            if (suit)
            {
                Part(PrimitiveType.Cube, "White shirt", new Vector3(0,1.27f,.177f), new Vector3(.19f,.55f,.035f), new Color(.76f,.74f,.67f));
                Part(PrimitiveType.Cube, "Tie", new Vector3(0,1.2f,.205f), new Vector3(.065f,.36f,.035f), jacketTint);
                Part(PrimitiveType.Cube, "Left lapel", new Vector3(-.105f,1.37f,.19f), new Vector3(.17f,.34f,.05f), new Color(.12f,.13f,.15f), new Vector3(0,0,-13));
                Part(PrimitiveType.Cube, "Right lapel", new Vector3(.105f,1.37f,.19f), new Vector3(.17f,.34f,.05f), new Color(.12f,.13f,.15f), new Vector3(0,0,13));
            }
            else
            {
                Part(PrimitiveType.Sphere, "Hood", new Vector3(0,1.58f,-.13f), new Vector3(.43f,.4f,.27f), new Color(.15f,.14f,.13f));
                Part(PrimitiveType.Cube, "Hoodie pocket", new Vector3(0,.99f,.17f), new Vector3(.29f,.15f,.035f), new Color(jacket.r*.78f,jacket.g*.78f,jacket.b*.78f));
            }
            Part(PrimitiveType.Sphere, "Left eye", new Vector3(-.075f,1.84f,.34f), new Vector3(.035f,.04f,.025f), new Color(.07f,.055f,.045f));
            Part(PrimitiveType.Sphere, "Right eye", new Vector3(.075f,1.84f,.34f), new Vector3(.035f,.04f,.025f), new Color(.07f,.055f,.045f));
        }

        GameObject Part(PrimitiveType primitive, string label, Vector3 position, Vector3 scale, Color color, Vector3? euler = null)
        {
            var part = GameObject.CreatePrimitive(primitive);
            part.name = label;
            part.transform.SetParent(rig, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            if (euler.HasValue) part.transform.localRotation = Quaternion.Euler(euler.Value);
            var collider = part.GetComponent<Collider>();
            if (collider) Destroy(collider);
            var renderer = part.GetComponent<Renderer>();
            if (renderer) renderer.sharedMaterial = ModelMaterial(color);
            return part;
        }

        static void ApplyCommunityPalette(Transform character,bool suit,Color accent)
        {
            Color skin=new Color(.54f,.31f,.20f),primary=suit?new Color(.09f,.11f,.13f):accent,secondary=suit?accent:new Color(.09f,.13f,.18f),metal=new Color(.18f,.21f,.22f);
            foreach(var renderer in character.GetComponentsInChildren<Renderer>(true))
            {
                string label=renderer.name.ToLowerInvariant();Color color=primary;
                if(label.Contains("skin")||label.Contains("head")||label.Contains("hand"))color=skin;
                else if(label.Contains("leg")||label.Contains("belt"))color=secondary;
                else if(label.Contains("cape")||label.Contains("hat")||label.Contains("hood"))color=suit?new Color(accent.r*.62f,accent.g*.62f,accent.b*.62f):new Color(primary.r*.55f,primary.g*.55f,primary.b*.55f);
                else if(label.Contains("knife")||label.Contains("shield"))color=metal;
                renderer.sharedMaterial=ModelMaterial(color);
            }
        }

        static Material ModelMaterial(Color color)
        {
            if(modelMaterials.TryGetValue(color,out var material))return material;
            material=new Material(Shader.Find("Standard"));material.color=color;modelMaterials[color]=material;return material;
        }
    }
}
