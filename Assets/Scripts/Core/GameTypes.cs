using System;

namespace RLSurvival
{
    public enum MatchPhase { Lobby, Build, Wave, Intermission, GameOver }
    public enum RaiderAlgorithm { QLearning, SARSA, DoubleQLearning }
    public enum AgentDifficulty { Rookie, Standard, Veteran }
    public enum UpgradeKind { Weapon, Block, Perk }

    [Serializable]
    public sealed class RaiderModel
    {
        public string id;
        public string name;
        public string algorithm = RaiderAlgorithm.QLearning.ToString();
        public string difficulty = AgentDifficulty.Standard.ToString();
        public float wallFocus = 1f;
        public float breachFocus = 1f;
        public float coreFocus = 1f;
        public float exploration = .18f;
        public int trainingEpisodes;
        public float breakthroughRate;
        public float[] qValues = new float[24];

        public RaiderAlgorithm Algorithm
        {
            get { RaiderAlgorithm parsed; return Enum.TryParse(algorithm, out parsed) ? parsed : RaiderAlgorithm.QLearning; }
            set { algorithm = value.ToString(); }
        }

        public AgentDifficulty Difficulty
        {
            get { AgentDifficulty parsed; return Enum.TryParse(difficulty, out parsed) ? parsed : AgentDifficulty.Standard; }
            set { difficulty = value.ToString(); }
        }

        public RaiderModel Copy()
        {
            return new RaiderModel
            {
                id = id, name = name, algorithm = algorithm, difficulty = difficulty,
                wallFocus = wallFocus, breachFocus = breachFocus, coreFocus = coreFocus,
                exploration = exploration, trainingEpisodes = trainingEpisodes,
                breakthroughRate = breakthroughRate, qValues = qValues == null ? new float[24] : (float[])qValues.Clone()
            };
        }
    }

    [Serializable]
    public sealed class MatchOptions
    {
        public string algorithm = RaiderAlgorithm.QLearning.ToString();
        public string difficulty = AgentDifficulty.Standard.ToString();
        public RaiderModel deployedModel;

        public RaiderAlgorithm Algorithm
        {
            get { RaiderAlgorithm value; return Enum.TryParse(algorithm, out value) ? value : RaiderAlgorithm.QLearning; }
            set { algorithm = value.ToString(); }
        }

        public AgentDifficulty Difficulty
        {
            get { AgentDifficulty value; return Enum.TryParse(difficulty, out value) ? value : AgentDifficulty.Standard; }
            set { difficulty = value.ToString(); }
        }
    }

    public struct AgentTuning
    {
        public readonly float LearningRate, Discount, Exploration, HealthScale, MiningScale, SpeedScale, SpawnScale;
        public AgentTuning(float learningRate, float discount, float exploration, float healthScale, float miningScale, float speedScale, float spawnScale)
        {
            LearningRate = learningRate; Discount = discount; Exploration = exploration;
            HealthScale = healthScale; MiningScale = miningScale; SpeedScale = speedScale; SpawnScale = spawnScale;
        }
    }

    public static class AgentTuningCatalog
    {
        public static AgentTuning Get(AgentDifficulty difficulty, int wave)
        {
            switch (difficulty)
            {
                case AgentDifficulty.Rookie: return new AgentTuning(.32f, .78f, MathfClamp(.34f - wave * .012f, .12f, .34f), .78f, .72f, .82f, .82f);
                case AgentDifficulty.Veteran: return new AgentTuning(.19f, .94f, MathfClamp(.10f - wave * .002f, .04f, .10f), 1.55f, 1.5f, 1.22f, 1.24f);
                default: return new AgentTuning(.25f, .87f, MathfClamp(.23f - wave * .008f, .07f, .23f), 1f, 1f, 1f, 1f);
            }
        }

        static float MathfClamp(float value, float min, float max) { return Math.Max(min, Math.Min(max, value)); }
    }

    public static class MatchPhaseExtensions
    {
        public static bool IsPlaying(this MatchPhase phase) { return phase == MatchPhase.Build || phase == MatchPhase.Wave || phase == MatchPhase.Intermission; }
    }
}
