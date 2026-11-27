using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace RLSurvival
{
    public sealed class PlayerAccountService : MonoBehaviour
    {
        SurvivalGame game;
        float saveAt;
        bool dirty, saving;
        bool signingOut;
        public string Token { get; private set; }
        public PlayerProfile Profile { get; private set; }
        public bool SignedIn { get; private set; }
        public bool IsBusy { get; private set; }
        public string Message { get; private set; }

        public void Initialize(SurvivalGame owner){game=owner;Message="Sign in or create a local account.";}
        public void Authenticate(string username,string password,bool create){if(!IsBusy)StartCoroutine(AuthRoutine(username,password,create));}
        public void Tick(float now)
        {
            if(dirty&&!saving&&now>=saveAt)StartCoroutine(SaveRoutine());
        }
        public bool Spend(int amount)
        {
            if(Profile==null||amount<0||Profile.credits<amount)return false;Profile.credits-=amount;MarkDirty();return true;
        }
        public void Earn(int amount){if(Profile==null)return;Profile.credits=Mathf.Clamp(Profile.credits+Mathf.Max(0,amount),0,10000000);MarkDirty();}
        public bool IsUnlocked(UpgradeKind kind,int index)
        {
            if(Profile==null)return false;var flags=Flags(kind);return index>=0&&index<flags.Length&&flags[index];
        }
        public bool Unlock(UpgradeKind kind,int index)
        {
            if(Profile==null||index<0||index>=UpgradeCatalog.Size(kind))return false;
            var flags=Flags(kind);if(flags[index])return true;int cost=UpgradeCatalog.UnlockCost(kind,index);if(!Spend(cost))return false;flags[index]=true;MarkDirty();return true;
        }
        public int Equipped(UpgradeKind kind)
        {
            if(Profile==null)return 0;switch(kind){case UpgradeKind.Weapon:return Profile.equippedGun;case UpgradeKind.Block:return Profile.equippedBlock;default:return Profile.equippedPerk;}
        }
        public void Equip(UpgradeKind kind,int index)
        {
            if(!IsUnlocked(kind,index))return;switch(kind){case UpgradeKind.Weapon:Profile.equippedGun=index;break;case UpgradeKind.Block:Profile.equippedBlock=index;break;default:Profile.equippedPerk=index;break;}MarkDirty();
        }
        public void SaveSoon(){MarkDirty();}
        public void SaveAgentModel(RaiderModel model)
        {
            if(Profile==null||model==null)return;
            if(Profile.agentModels==null)Profile.agentModels=new RaiderModel[0];
            var models=new System.Collections.Generic.List<RaiderModel>(Profile.agentModels);
            int existing=models.FindIndex(candidate=>candidate!=null&&candidate.id==model.id);
            if(existing>=0)models[existing]=model.Copy();
            else{if(models.Count>=8)models.RemoveAt(0);models.Add(model.Copy());}
            Profile.agentModels=models.ToArray();Profile.equippedAgentModel=model.id;MarkDirty();
        }
        public RaiderModel FindAgentModel(string id)
        {
            if(Profile==null||Profile.agentModels==null)return null;
            foreach(var model in Profile.agentModels)if(model!=null&&model.id==id)return model;
            return null;
        }
        public void SignOut(){if(SignedIn&&!signingOut)StartCoroutine(SignOutRoutine());}
        void MarkDirty(){dirty=true;saveAt=Time.unscaledTime+1f;}
        bool[] Flags(UpgradeKind kind){switch(kind){case UpgradeKind.Weapon:return Profile.guns;case UpgradeKind.Block:return Profile.blocks;default:return Profile.perks;}}
        IEnumerator AuthRoutine(string username,string password,bool create)
        {
            IsBusy=true;Message="Connecting to the game server…";string json=JsonUtility.ToJson(new Credentials{username=username.Trim(),password=password});
            using(var request=JsonRequest(game.ServerUrl+"/auth/"+(create?"register":"login"),json,null))
            {
                request.timeout=12;yield return request.SendWebRequest();AuthResponse response=null;try{response=JsonUtility.FromJson<AuthResponse>(request.downloadHandler.text);}catch{}
                if(request.result!=UnityWebRequest.Result.Success||response==null||string.IsNullOrEmpty(response.token)||response.profile==null){Message=response!=null&&!string.IsNullOrEmpty(response.error)?response.error:"Could not reach the relay: "+request.error;IsBusy=false;yield break;}
                Token=response.token;Profile=response.profile;NormalizeProfile();SignedIn=true;dirty=false;game.OnSignedIn(Profile.username);Message="Welcome, "+Profile.username+".";
            }
            IsBusy=false;
        }
        void NormalizeProfile()
        {
            Profile.guns=Resize(Profile.guns,UpgradeCatalog.Weapons.Length);Profile.blocks=Resize(Profile.blocks,UpgradeCatalog.Blocks.Length);Profile.perks=Resize(Profile.perks,UpgradeCatalog.Perks.Length);
            Profile.guns[0]=true;Profile.blocks[0]=true;
            if(Profile.agentModels==null)Profile.agentModels=new RaiderModel[0];
            if(FindAgentModel(Profile.equippedAgentModel)==null)Profile.equippedAgentModel="";
            if(!Profile.guns[Mathf.Clamp(Profile.equippedGun,0,Profile.guns.Length-1)])Profile.equippedGun=0;
            if(!Profile.blocks[Mathf.Clamp(Profile.equippedBlock,0,Profile.blocks.Length-1)])Profile.equippedBlock=0;
            if(!Profile.perks[Mathf.Clamp(Profile.equippedPerk,0,Profile.perks.Length-1)])Profile.equippedPerk=0;
        }
        static bool[] Resize(bool[] source,int count){var result=new bool[count];if(source!=null)for(int i=0;i<Mathf.Min(source.Length,count);i++)result[i]=source[i];return result;}
        IEnumerator SaveRoutine()
        {
            if(Profile==null||string.IsNullOrEmpty(Token))yield break;saving=true;dirty=false;saveAt=Time.unscaledTime+2f;
            using(var request=JsonRequest(game.ServerUrl+"/profile",JsonUtility.ToJson(Profile),Token)){request.timeout=8;yield return request.SendWebRequest();if(request.result!=UnityWebRequest.Result.Success){dirty=true;Message="Profile save failed. Check the host connection.";}}
            saving=false;
        }
        IEnumerator SignOutRoutine()
        {
            signingOut=true;
            while(saving)yield return null;
            if(dirty)yield return SaveRoutine();
            string token=Token;
            if(!string.IsNullOrEmpty(token))yield return LogoutRoutine(token);
            Token="";Profile=null;SignedIn=false;dirty=false;signingOut=false;game.PasswordInput="";
        }
        IEnumerator LogoutRoutine(string token){using(var request=JsonRequest(game.ServerUrl+"/auth/logout","{}",token))yield return request.SendWebRequest();}
        static UnityWebRequest JsonRequest(string url,string json,string token)
        {
            var request=new UnityWebRequest(url,"POST");request.uploadHandler=new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));request.downloadHandler=new DownloadHandlerBuffer();request.SetRequestHeader("Content-Type","application/json");if(!string.IsNullOrEmpty(token))request.SetRequestHeader("Authorization","Bearer "+token);return request;
        }
    }
}
