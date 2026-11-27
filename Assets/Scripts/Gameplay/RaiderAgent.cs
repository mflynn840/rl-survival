using UnityEngine;

namespace RLSurvival
{
    public sealed class RaiderAgent : MonoBehaviour
    {
        public string NetworkId;
        public FortBlock Target;
        public int Health;
        public int MaxHealth;
        public bool HasKnife;
        public int Action;
        public float DecisionAt, MiningProgress, ShockCooldown, CoreAttackCooldown, PlayerAttackCooldown, ExitUntil;
        public RlAgentBrain Brain;
        public HumanoidModelBuilder Visual;
        public AgentDifficulty Difficulty;
        public RaiderAlgorithm Algorithm;
    }
}
