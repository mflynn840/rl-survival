using UnityEngine;

namespace RLSurvival
{
    public sealed class RlAgentMemory
    {
        internal readonly float[,] QA=new float[8,3];
        internal readonly float[,] QB=new float[8,3];
        public RaiderModel Model { get; private set; }
        public RaiderAlgorithm Algorithm { get; private set; }
        public RlAgentMemory(RaiderAlgorithm algorithm,RaiderModel model=null)
        {
            Algorithm=algorithm;Model=model==null?null:model.Copy();
            if(Model==null||Model.qValues==null)return;
            for(int state=0;state<8;state++)for(int action=0;action<3;action++)
            {
                int index=state*3+action;if(index>=Model.qValues.Length)continue;
                QA[state,action]=Model.qValues[index];QB[state,action]=Model.qValues[index];
            }
        }
    }

    /// <summary>Small per-agent tabular learner. Actions select nearest wall, weakest wall, or the crew core.</summary>
    public sealed class RlAgentBrain
    {
        const int StateCount = 8;
        const int ActionCount = 3;
        readonly RlAgentMemory memory;
        readonly RaiderAlgorithm algorithm;
        readonly AgentTuning tuning;
        int previousState, previousAction;
        float pendingReward;
        bool hasPrevious;

        public RlAgentBrain(RaiderAlgorithm algorithm, AgentTuning tuning,RlAgentMemory sharedMemory)
        { this.algorithm=algorithm; this.tuning=tuning;memory=sharedMemory; }

        public void AddReward(float reward) { pendingReward=Mathf.Clamp(pendingReward+reward,-4f,8f); }

        public int ChooseAction(int state)
        {
            state=Mathf.Clamp(state,0,StateCount-1);
            float exploration=memory.Model==null?tuning.Exploration:memory.Model.exploration;
            int nextAction=UnityEngine.Random.value<exploration?UnityEngine.Random.Range(0,ActionCount):GreedyPolicy(state);
            if(hasPrevious)Update(previousState,previousAction,pendingReward,state,nextAction);
            previousState=state;previousAction=nextAction;hasPrevious=true;pendingReward=0;
            return nextAction;
        }

        public void EndEpisode(float terminalReward)
        {
            if(!hasPrevious)return;
            if(algorithm==RaiderAlgorithm.DoubleQLearning)
            {
                if(UnityEngine.Random.value<.5f){float value=memory.QA[previousState,previousAction];memory.QA[previousState,previousAction]=value+tuning.LearningRate*(pendingReward+terminalReward-value);}
                else{float value=memory.QB[previousState,previousAction];memory.QB[previousState,previousAction]=value+tuning.LearningRate*(pendingReward+terminalReward-value);}
            }
            else{float value=memory.QA[previousState,previousAction];memory.QA[previousState,previousAction]=value+tuning.LearningRate*(pendingReward+terminalReward-value);}
            hasPrevious=false;pendingReward=0;
        }

        void Update(int state,int action,float reward,int nextState,int nextAction)
        {
            if(algorithm==RaiderAlgorithm.SARSA)
            {
                float target=reward+tuning.Discount*Read(memory.QA,nextState,nextAction);
                memory.QA[state,action]+=tuning.LearningRate*(target-memory.QA[state,action]);
                return;
            }
            if(algorithm==RaiderAlgorithm.DoubleQLearning)
            {
                if(UnityEngine.Random.value<.5f)
                {
                    int best=GreedyRaw(memory.QA,nextState);float target=reward+tuning.Discount*memory.QB[nextState,best];
                    memory.QA[state,action]+=tuning.LearningRate*(target-memory.QA[state,action]);
                }
                else
                {
                    int best=GreedyRaw(memory.QB,nextState);float target=reward+tuning.Discount*memory.QA[nextState,best];
                    memory.QB[state,action]+=tuning.LearningRate*(target-memory.QB[state,action]);
                }
                return;
            }
            float qTarget=reward+tuning.Discount*Max(memory.QA,nextState);
            memory.QA[state,action]+=tuning.LearningRate*(qTarget-memory.QA[state,action]);
        }

        float Read(float[,] table,int state,int action) { return algorithm==RaiderAlgorithm.DoubleQLearning?(memory.QA[state,action]+memory.QB[state,action])*.5f:table[state,action]; }
        int GreedyRaw(float[,] table,int state)
        {
            int best=0;float value=table[state,0];
            for(int action=1;action<ActionCount;action++){float next=table[state,action];if(next>value){best=action;value=next;}}
            return best;
        }
        int GreedyPolicy(int state)
        {
            if(algorithm!=RaiderAlgorithm.DoubleQLearning)return GreedyRaw(memory.QA,state);
            int best=0;float value=Read(memory.QA,state,0);for(int i=1;i<ActionCount;i++){float next=Read(memory.QA,state,i);if(next>value){best=i;value=next;}}return best;
        }
        float Max(float[,] table,int state){float best=table[state,0];for(int i=1;i<ActionCount;i++)best=Mathf.Max(best,table[state,i]);return best;}
    }
}
