using UnityEngine;

namespace RLSurvival
{
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartGame()
        {
            if(Object.FindObjectOfType<SurvivalGame>()==null)new GameObject("RL Survival").AddComponent<SurvivalGame>();
        }
    }
}
