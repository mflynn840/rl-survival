using System.Collections.Generic;
using UnityEngine;

namespace RLSurvival
{
    public sealed class RaiderDirector : MonoBehaviour
    {
        readonly List<RaiderAgent> agents=new List<RaiderAgent>();
        SurvivalGame game;
        float nextSpawn;
        int nextId=1;
        RlAgentMemory policyMemory;
        public int RemainingToSpawn { get; private set; }
        public int AliveCount { get { return agents.FindAll(agent=>agent!=null).Count; } }
        public void Initialize(SurvivalGame owner){game=owner;policyMemory=new RlAgentMemory(RaiderAlgorithm.QLearning);}

        public void BeginWave(int wave)
        {
            var tuning=AgentTuningCatalog.Get(game.Options.Difficulty,wave);
            RemainingToSpawn=Mathf.Clamp(Mathf.RoundToInt((6+wave*3)*tuning.SpawnScale),4,40);
            nextSpawn=Time.time+1f;
        }
        public void SetRemainingToSpawn(int count){RemainingToSpawn=Mathf.Max(0,count);}
        public void Tick(float deltaTime)
        {
            if(!game.IsHost)return;
            var tuning=AgentTuningCatalog.Get(game.Options.Difficulty,game.Wave);
            if(RemainingToSpawn>0&&Time.time>=nextSpawn&&agents.Count<Mathf.Min(36,Mathf.CeilToInt((2+game.Wave*2)*tuning.SpawnScale)))
            {
                nextSpawn=Time.time+Mathf.Max(.28f,(2.1f-game.Wave*.075f)/tuning.SpawnScale);
                Spawn(tuning);RemainingToSpawn--;
            }
            foreach(var agent in new List<RaiderAgent>(agents))if(agent)TickAgent(agent,deltaTime,tuning);
            if(agents.Count==0&&RemainingToSpawn==0)game.MatchDirector.CompleteWave();
        }
        void Spawn(AgentTuning tuning)
        {
            var exits=game.Arena.HotelExitPoints;int door=exits.Length>0?UnityEngine.Random.Range(0,exits.Length):0;
            Vector3 position=exits.Length>0?exits[door]:new Vector3(0,0,-13f);
            game.Arena.OpenHotelDoor(door);
            string id=nextId.ToString();nextId++;SpawnAt(position,Mathf.RoundToInt((42+game.Wave*14)*tuning.HealthScale),id);
        }
        void SpawnAt(Vector3 position,int health,string id)
        {
            if(agents.Exists(agent=>agent&&agent.NetworkId==id))return;
            var actor=new GameObject("Hotel Raider "+id);actor.transform.SetParent(game.Arena.WorldRoot);actor.transform.position=new Vector3(position.x,0,position.z);
            var color=AlgorithmColor(game.Options.Algorithm);var visual=HumanoidModelBuilder.Create(actor,HumanoidStyle.SuitedRaider,color);
            var agent=actor.AddComponent<RaiderAgent>();agent.NetworkId=id;agent.Health=health;agent.MaxHealth=health;agent.ExitUntil=Time.time+.8f;agent.HasKnife=int.TryParse(id,out var parsed)&&parsed%2==0;agent.Algorithm=game.Options.Algorithm;agent.Difficulty=game.Options.Difficulty;agent.Brain=new RlAgentBrain(agent.Algorithm,AgentTuningCatalog.Get(agent.Difficulty,game.Wave),policyMemory);agents.Add(agent);
            agent.Visual=visual;visual.SetMeleeWeapon(agent.HasKnife);actor.GetComponent<CombatHealthBar>().Set(health,health,true);
            int numericId;if(int.TryParse(id,out numericId))nextId=Mathf.Max(nextId,numericId+1);
            if(game.Connected)game.PublishAction("spawn",position,health,id);
        }
        void TickAgent(RaiderAgent agent,float deltaTime,AgentTuning tuning)
        {
            if(Time.time<agent.ExitUntil)
            {
                Vector3 before=agent.transform.position;
                agent.transform.position=Vector3.MoveTowards(before,new Vector3(before.x,0,-11.7f),deltaTime*2.8f);
                if(agent.Visual)agent.Visual.Animate(agent.transform.position-before,2.8f);
                return;
            }
            agent.PlayerAttackCooldown=Mathf.Max(0,agent.PlayerAttackCooldown-deltaTime);
            if(agent.ShockCooldown<=0)
            {
                foreach(var hazard in game.Building.Blocks)if(hazard&&hazard.Type==7&&Vector3.Distance(agent.transform.position,hazard.transform.position)<1.08f)
                {
                    agent.Health-=20;agent.ShockCooldown=.48f;agent.GetComponent<CombatHealthBar>().Set(agent.Health,agent.MaxHealth,true);
                    if(agent.Health<=0){Kill(agent);return;}
                    break;
                }
            }
            if(Time.time>=agent.DecisionAt)
            {
                int state=ReadState(agent);agent.Action=agent.Brain.ChooseAction(state);agent.DecisionAt=Time.time+Mathf.Max(.65f,2.0f-game.Wave*.055f);
                agent.Target=agent.Action==2?null:ChooseBlock(agent,agent.Action);
            }
            bool nearPlayer=game.FindLivingPlayer(agent.transform.position,4.8f,out var playerId,out var playerPosition);
            Vector3 destination=nearPlayer?playerPosition:agent.Target?new Vector3(agent.Target.transform.position.x,0,agent.Target.transform.position.z):new Vector3(game.Arena.CorePosition.x,0,game.Arena.CorePosition.z);
            Vector3 previousPosition=agent.transform.position;
            float oldDistance=Vector3.Distance(agent.transform.position,destination);
            if(!nearPlayer&&agent.Target&&oldDistance<1.55f)
            {
                agent.MiningProgress+=deltaTime*(8+game.Wave*2)*tuning.MiningScale;
                var target=agent.Target.GetComponent<FortBlock>();
                var blockDefinition=UpgradeCatalog.Blocks[target.Type];
                if(blockDefinition.ShockDamage>0&&agent.ShockCooldown<=0){agent.Health-=blockDefinition.ShockDamage;agent.ShockCooldown=.7f;agent.Brain.AddReward(.45f);if(agent.Health<=0){Kill(agent);return;}}
                if(agent.MiningProgress>=1f){agent.MiningProgress=0;int damage=Mathf.Max(1,Mathf.RoundToInt((12+game.Wave*2)*tuning.MiningScale));bool broken=target.Health<=damage;game.Building.DamageAt(agent.Target.transform.position,damage);agent.Brain.AddReward(.65f);if(broken)agent.Brain.AddReward(2.5f);}
            }
            else if(oldDistance>1.1f)agent.transform.position=Vector3.MoveTowards(agent.transform.position,destination,deltaTime*(1.7f+Mathf.Min(game.Wave*.07f,1.8f))*tuning.SpeedScale);
            agent.ShockCooldown=Mathf.Max(0,agent.ShockCooldown-deltaTime);
            float distance=Vector3.Distance(agent.transform.position,game.Arena.CorePosition);
            if(nearPlayer&&Vector3.Distance(agent.transform.position,playerPosition)<1.55f&&agent.PlayerAttackCooldown<=0){agent.PlayerAttackCooldown=1.12f;game.DamagePlayer(playerId,(agent.HasKnife?12:7)+game.Wave);if(agent.Visual)agent.Visual.Attack();agent.Brain.AddReward(.8f);}
            else if(!nearPlayer&&distance<1.4f){agent.CoreAttackCooldown-=deltaTime;if(agent.CoreAttackCooldown<=0){agent.CoreAttackCooldown=1.5f;game.DamageCore(8+game.Wave);agent.Brain.AddReward(1.2f);}}
            Vector3 movement=agent.transform.position-previousPosition;if(agent.Visual!=null)agent.Visual.Animate(movement,deltaTime>0?movement.magnitude/deltaTime:0f);if(movement.sqrMagnitude>.001f)agent.transform.forward=movement.normalized;
            agent.Brain.AddReward(Mathf.Clamp((oldDistance-Vector3.Distance(agent.transform.position,destination))*.08f,-.05f,.12f));
        }
        int ReadState(RaiderAgent agent)
        {
            if(game.Building.Blocks.Count==0)return 0;
            float weakest=1f;foreach(var block in game.Building.Blocks)if(block)weakest=Mathf.Min(weakest,(float)block.Health/block.MaxHealth);
            int wallState=weakest<.3f?1:weakest>.72f?2:3;int coreNear=Vector3.Distance(agent.transform.position,game.Arena.CorePosition)<6f?1:0;
            return wallState*2+coreNear;
        }
        FortBlock ChooseBlock(RaiderAgent agent,int policy)
        {
            FortBlock selected=null;float best=float.MaxValue;
            foreach(var block in game.Building.Blocks)
            {
                if(!block||block.Health<=0)continue;float distance=Vector3.Distance(agent.transform.position,block.transform.position);
                float score=policy==0?distance:distance*.36f+(float)block.Health/block.MaxHealth*8f;
                if(score<best){best=score;selected=block;}
            }
            return selected;
        }
        void Kill(RaiderAgent agent)
        {
            agent.Brain.EndEpisode(3f);agents.Remove(agent);game.EarnCredits(15+(game.HasPerk(1)?5:0));game.Kills++;
            if(game.Connected)game.PublishAction("kill",agent.transform.position,0,agent.NetworkId);Destroy(agent.gameObject);
        }
        public void ApplyHostDamage(string id,int damage)
        {
            var agent=agents.Find(candidate=>candidate&&candidate.NetworkId==id);if(agent==null)return;agent.Health-=damage;agent.GetComponent<CombatHealthBar>().Set(agent.Health,agent.MaxHealth,true);
            if(agent.Health<=0){Kill(agent);return;}if(game.Connected)game.PublishAction("raider_damage",agent.transform.position,agent.Health,id);
        }
        public void DamageInRadius(Vector3 center,int damage,float radius)
        {
            foreach(var agent in new List<RaiderAgent>(agents))if(agent&&Vector3.Distance(agent.transform.position,center)<=radius)ApplyHostDamage(agent.NetworkId,damage);
        }
        public RaiderAgent FindAimTarget(Ray ray,float aimDegrees)
        {
            RaiderAgent selected=null;float nearest=float.MaxValue;
            foreach(var agent in agents){if(!agent)continue;Vector3 to=agent.transform.position+Vector3.up*1.05f-ray.origin;float distance=to.magnitude;if(distance<nearest&&Vector3.Angle(ray.direction,to)<=aimDegrees){nearest=distance;selected=agent;}}
            return selected;
        }
        public RaiderAgent Find(string id){return agents.Find(agent=>agent&&agent.NetworkId==id);}
        public void ApplySpawn(Vector3 position,int health,string id)
        {
            SpawnAt(position,health,id);
            var exits=game.Arena.HotelExitPoints;int nearest=0;float distance=float.MaxValue;
            for(int index=0;index<exits.Length;index++){float candidate=Mathf.Abs(exits[index].x-position.x);if(candidate<distance){distance=candidate;nearest=index;}}
            game.Arena.OpenHotelDoor(nearest);
        }
        public void SetHealth(string id,int health){var agent=Find(id);if(agent!=null){agent.Health=health;agent.GetComponent<CombatHealthBar>().Set(health,agent.MaxHealth,true);}}
        public void ApplyHostKill(string id){var agent=agents.Find(candidate=>candidate&&candidate.NetworkId==id);if(agent!=null)Kill(agent);}
        public void Clear(){foreach(var agent in agents)if(agent)Destroy(agent.gameObject);agents.Clear();RemainingToSpawn=0;nextId=1;policyMemory=new RlAgentMemory(game.Options.Algorithm,game.Options.deployedModel);}
        public RaiderSnapshot[] Capture(){var result=new List<RaiderSnapshot>();foreach(var agent in agents)if(agent)result.Add(new RaiderSnapshot{id=agent.NetworkId,x=agent.transform.position.x,z=agent.transform.position.z,hp=agent.Health,maxHp=agent.MaxHealth,weapon=agent.HasKnife?"knife":"fists"});return result.ToArray();}
        public void Apply(RaiderSnapshot[] snapshot)
        {
            var seen=new HashSet<string>();if(snapshot!=null)foreach(var data in snapshot){var agent=agents.Find(candidate=>candidate&&candidate.NetworkId==data.id);if(agent==null){SpawnAt(new Vector3(data.x,0,data.z),data.maxHp>0?data.maxHp:data.hp,data.id);agent=agents.Find(candidate=>candidate&&candidate.NetworkId==data.id);}agent.transform.position=new Vector3(data.x,0,data.z);agent.Health=data.hp;agent.MaxHealth=data.maxHp>0?data.maxHp:agent.MaxHealth;agent.HasKnife=data.weapon=="knife";if(agent.Visual)agent.Visual.SetMeleeWeapon(agent.HasKnife);agent.GetComponent<CombatHealthBar>().Set(agent.Health,agent.MaxHealth,true);seen.Add(data.id);}
            foreach(var agent in new List<RaiderAgent>(agents))if(agent&&!seen.Contains(agent.NetworkId)){agents.Remove(agent);Destroy(agent.gameObject);}
        }
        static Color AlgorithmColor(RaiderAlgorithm algorithm){switch(algorithm){case RaiderAlgorithm.SARSA:return new Color(1f,.56f,.18f);case RaiderAlgorithm.DoubleQLearning:return new Color(.74f,.3f,1f);default:return new Color(1f,.28f,.18f);}}
    }
}
