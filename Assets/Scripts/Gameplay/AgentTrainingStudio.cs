using System;
using System.Collections;
using UnityEngine;

namespace RLSurvival
{
    /// <summary>Offline, repeatable training sandbox for authoring and saving custom raider policies.</summary>
    public sealed class AgentTrainingStudio : MonoBehaviour
    {
        const int StateCount = 8;
        const int ActionCount = 3;
        const int EpisodeCount = 600;
        const int StepsPerEpisode = 56;

        SurvivalGame game;
        public string DraftName = "My Raider";
        public RaiderAlgorithm DraftAlgorithm = RaiderAlgorithm.QLearning;
        public AgentDifficulty DraftDifficulty = AgentDifficulty.Standard;
        public float WallFocus = 1f;
        public float BreachFocus = 1f;
        public float CoreFocus = 1f;
        public float Exploration = .18f;
        public bool IsTraining { get; private set; }
        public float Progress { get; private set; }
        public string Status { get; private set; } = "Design a policy, train it, then deploy it from the lobby.";

        public void Initialize(SurvivalGame owner) { game = owner; }

        public void TrainDraft()
        {
            if (IsTraining || game == null || !game.Account.SignedIn) return;
            StartCoroutine(TrainRoutine());
        }

        public void Deploy(RaiderModel model)
        {
            if (model == null) return;
            if(game.Connected&&!game.IsHost)
            {
                game.DeployAgentModel(model);
                Status = "Shared “" + model.name + "” with the match host. Ask the host to deploy it.";
                return;
            }
            game.DeployAgentModel(model);
            Status = "Deployed “" + model.name + "” for the next match.";
        }

        IEnumerator TrainRoutine()
        {
            IsTraining = true;
            Progress = 0f;
            Status = "Training in the fortress simulation…";

            var model = new RaiderModel
            {
                id = Guid.NewGuid().ToString("N"),
                name = string.IsNullOrWhiteSpace(DraftName) ? "Custom Raider" : DraftName.Trim().Substring(0, Mathf.Min(24, DraftName.Trim().Length)),
                Algorithm = DraftAlgorithm,
                Difficulty = DraftDifficulty,
                wallFocus = Mathf.Clamp(WallFocus, .2f, 3f),
                breachFocus = Mathf.Clamp(BreachFocus, .2f, 3f),
                coreFocus = Mathf.Clamp(CoreFocus, .2f, 3f),
                exploration = Mathf.Clamp(Exploration, .02f, .6f),
                qValues = new float[StateCount * ActionCount]
            };
            var qa = new float[StateCount * ActionCount];
            var qb = new float[StateCount * ActionCount];
            var tuning = AgentTuningCatalog.Get(model.Difficulty, 1);
            int completed = 0;

            for (int episode = 0; episode < EpisodeCount; episode++)
            {
                int wallHealth = UnityEngine.Random.Range(70, 131);
                int coreHealth = 100;
                bool coreApproach = false;
                int state = TrainingState(wallHealth, coreHealth, coreApproach);
                int action = Choose(qa, qb, state, model.Algorithm, model.exploration);

                for (int step = 0; step < StepsPerEpisode; step++)
                {
                    float reward = ApplyTrainingAction(action, ref wallHealth, ref coreHealth, ref coreApproach, model);
                    bool terminal = wallHealth <= 0 || coreHealth <= 0;
                    int nextState = TrainingState(wallHealth, coreHealth, coreApproach);
                    int nextAction = terminal ? 0 : Choose(qa, qb, nextState, model.Algorithm, model.exploration);
                    Update(qa, qb, state, action, reward, nextState, nextAction, terminal, model.Algorithm, tuning.LearningRate, tuning.Discount);
                    state = nextState;
                    action = nextAction;
                    if (terminal) break;
                }

                completed++;
                if (episode % 12 == 0)
                {
                    Progress = (float)completed / EpisodeCount;
                    yield return null;
                }
            }

            for (int i = 0; i < model.qValues.Length; i++)
                model.qValues[i] = model.Algorithm == RaiderAlgorithm.DoubleQLearning ? (qa[i] + qb[i]) * .5f : qa[i];
            model.trainingEpisodes = EpisodeCount;
            model.breakthroughRate = Evaluate(model);
            game.Account.SaveAgentModel(model);
            Deploy(model);
            Progress = 1f;
            string deployment=game.Connected&&!game.IsHost?"saved and shared with the host":"saved and deployed";
            Status = "Training complete: " + model.trainingEpisodes + " episodes, " + Mathf.RoundToInt(model.breakthroughRate * 100f) + "% simulated core breakthroughs. Model " + deployment + ".";
            IsTraining = false;
        }

        static float ApplyTrainingAction(int action, ref int wallHealth, ref int coreHealth, ref bool coreApproach, RaiderModel model)
        {
            if (action == 2)
            {
                int damage = coreApproach ? 13 : 6;
                coreApproach = true;
                coreHealth -= damage;
                return damage * .08f * model.coreFocus;
            }

            int blockDamage = action == 1 && wallHealth < 55 ? 17 : action == 0 ? 9 : 6;
            wallHealth -= blockDamage;
            float reward = blockDamage * .035f * model.wallFocus;
            if (wallHealth <= 0) reward += 2.5f * model.breachFocus;
            return reward;
        }

        static int TrainingState(int wallHealth, int coreHealth, bool coreApproach)
        {
            int wallState = wallHealth < 35 ? 0 : wallHealth < 75 ? 1 : wallHealth < 110 ? 2 : 3;
            int coreState = coreApproach || coreHealth < 65 ? 1 : 0;
            return wallState * 2 + coreState;
        }

        static int Choose(float[] qa, float[] qb, int state, RaiderAlgorithm algorithm, float exploration)
        {
            if (UnityEngine.Random.value < exploration) return UnityEngine.Random.Range(0, ActionCount);
            int best = 0;
            float bestValue = Value(qa, qb, state, 0, algorithm);
            for (int action = 1; action < ActionCount; action++)
            {
                float value = Value(qa, qb, state, action, algorithm);
                if (value > bestValue) { best = action; bestValue = value; }
            }
            return best;
        }

        static void Update(float[] qa, float[] qb, int state, int action, float reward, int nextState, int nextAction, bool terminal, RaiderAlgorithm algorithm, float alpha, float gamma)
        {
            int index = state * ActionCount + action;
            float discount = terminal ? 0f : gamma;
            if (algorithm == RaiderAlgorithm.DoubleQLearning)
            {
                if (UnityEngine.Random.value < .5f)
                {
                    int best = Greedy(qa, nextState);
                    float target = reward + discount * qb[nextState * ActionCount + best];
                    qa[index] += alpha * (target - qa[index]);
                }
                else
                {
                    int best = Greedy(qb, nextState);
                    float target = reward + discount * qa[nextState * ActionCount + best];
                    qb[index] += alpha * (target - qb[index]);
                }
                return;
            }

            float future = algorithm == RaiderAlgorithm.SARSA
                ? qa[nextState * ActionCount + nextAction]
                : Mathf.Max(qa[nextState * ActionCount], qa[nextState * ActionCount + 1], qa[nextState * ActionCount + 2]);
            float qTarget = reward + discount * future;
            qa[index] += alpha * (qTarget - qa[index]);
        }

        static int Greedy(float[] values, int state)
        {
            int offset = state * ActionCount;
            int best = 0;
            for (int action = 1; action < ActionCount; action++) if (values[offset + action] > values[offset + best]) best = action;
            return best;
        }

        static float Value(float[] qa, float[] qb, int state, int action, RaiderAlgorithm algorithm)
        {
            int index = state * ActionCount + action;
            return algorithm == RaiderAlgorithm.DoubleQLearning ? (qa[index] + qb[index]) * .5f : qa[index];
        }

        static float Evaluate(RaiderModel model)
        {
            float breakthroughs = 0f;
            const int trials = 80;
            for (int trial = 0; trial < trials; trial++)
            {
                int wallHealth = UnityEngine.Random.Range(70, 131);
                int coreHealth = 100;
                bool coreApproach = false;
                for (int step = 0; step < StepsPerEpisode && wallHealth > 0 && coreHealth > 0; step++)
                {
                    int state = TrainingState(wallHealth, coreHealth, coreApproach);
                    int action = Greedy(model.qValues, state);
                    ApplyTrainingAction(action, ref wallHealth, ref coreHealth, ref coreApproach, model);
                }
                if (coreHealth <= 0) breakthroughs++;
            }
            return breakthroughs / trials;
        }
    }
}
