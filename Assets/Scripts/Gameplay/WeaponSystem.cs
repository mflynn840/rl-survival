using UnityEngine;

namespace RLSurvival
{
    public sealed class WeaponSystem : MonoBehaviour
    {
        SurvivalGame game;
        float nextShot;
        int ammo=60;
        int equippedWeapon;
        public int Ammo { get { return ammo; } }
        public int Magazine { get { return UpgradeCatalog.Weapons[equippedWeapon].Magazine; } }
        public void Initialize(SurvivalGame owner){game=owner;equippedWeapon=0;ammo=UpgradeCatalog.Weapons[0].Magazine;}
        public void Equip(int weaponIndex)
        {
            equippedWeapon=Mathf.Clamp(weaponIndex,0,UpgradeCatalog.Weapons.Length-1);
            ammo=Mathf.Min(ammo,UpgradeCatalog.Weapons[equippedWeapon].Magazine);
            var visual=game.Arena.LocalPlayer.GetComponent<HumanoidModelBuilder>();if(visual)visual.SetWeapon(equippedWeapon);
        }
        public void Tick()
        {
            if(Input.GetKeyDown(KeyCode.R))BuyAmmo();
            if(!Input.GetMouseButton(0)||Time.time<nextShot||ammo<=0||!WorldPointer())return;
            var weapon=UpgradeCatalog.Weapons[game.SelectedWeapon];var ray=game.Arena.GameCamera.ScreenPointToRay(Input.mousePosition);var target=game.Raiders.FindAimTarget(ray,weapon.AimDegrees);
            nextShot=Time.time+weapon.SecondsPerShot*(game.HasPerk(0) ? .84f : 1f);ammo--;
            var model=game.Arena.LocalPlayer.GetComponent<HumanoidModelBuilder>();
            if(target!=null)
            {
                Vector3 direction=target.transform.position-game.Arena.LocalPlayer.transform.position;direction.y=0;
                if(direction.sqrMagnitude>.01f)game.Arena.LocalPlayer.transform.forward=direction.normalized;
            }
            Vector3 origin=model?model.MuzzlePosition:game.Arena.LocalPlayer.transform.position+Vector3.up*1.25f;
            Vector3 hitPoint=target?target.transform.position+Vector3.up*1.3f:ray.GetPoint(35);
            if(game.Vfx)game.Vfx.PlayShot(origin,hitPoint,target!=null,game.SelectedWeapon==4);
            if(target==null)return;
            int damage=weapon.Damage*(game.HasPerk(5)?2:1);
            if(game.Connected&&!game.IsHost)game.SendCommand("input",new GameCommand{action="shoot",id=target.NetworkId,value=game.SelectedWeapon,value2=damage});
            else ApplyShot(target.NetworkId,damage,weapon.SplashRadius);
        }
        public void ApplyShot(string targetId,int damage,int radius)
        {
            var target=game.Raiders.Find(targetId);if(target==null)return;
            if(radius>0)game.Raiders.DamageInRadius(target.transform.position,damage,radius);else game.Raiders.ApplyHostDamage(targetId,damage);
        }
        public void BuyAmmo()
        {
            if(game.Phase!=MatchPhase.Wave&&game.Phase!=MatchPhase.Intermission)return;
            if(!game.TrySpend(25))return;ammo=Magazine;
        }
        public void Refill(){ammo=Magazine;}
        bool WorldPointer(){var point=Input.mousePosition;float guiY=Screen.height-point.y;var hud=game.GetComponent<SurvivalHud>();if(point.x<=310||point.y>=Screen.height-150||point.x>=Screen.width-150&&guiY<55||hud!=null&&hud.SettingsOpen)return false;return true;}
    }
}
