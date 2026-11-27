using UnityEngine;

namespace RLSurvival
{
    public sealed class MatchDirector : MonoBehaviour
    {
        SurvivalGame game;
        public void Initialize(SurvivalGame owner){game=owner;}
        public void StartMatch(MatchOptions options)
        {
            game.Options=options??new MatchOptions();game.ResetMatchState();game.SetPhase(MatchPhase.Build,35f,"Build phase — fortify the base.");
        }
        public void Tick(float deltaTime)
        {
            if(game.Phase==MatchPhase.Lobby||game.Phase==MatchPhase.GameOver)return;
            game.PhaseRemaining=Mathf.Max(0,game.PhaseRemaining-deltaTime);
            if(game.PhaseRemaining>0)return;
            if(!game.IsHost)return;
            if(game.Phase==MatchPhase.Build||game.Phase==MatchPhase.Intermission)StartWave();
            else if(game.Phase==MatchPhase.Wave)game.SetPhase(MatchPhase.GameOver,0,"The raiders broke through. Your account progress is saved.");
        }
        public void StartWave()
        {
            game.Wave++;game.Raiders.BeginWave(game.Wave);game.SetPhase(MatchPhase.Wave,120f,"Wave "+game.Wave+" — raiders incoming.");
            if(game.Connected)game.PublishAction("wave",Vector3.zero,game.Wave);
        }
        public void CompleteWave()
        {
            if(!game.IsHost||game.Phase!=MatchPhase.Wave)return;
            game.EarnCredits(50+game.Wave*12);
            game.HealCrewBetweenWaves();
            if(game.HasPerk(4))game.CoreHealth=Mathf.Min(game.MaxCoreHealth,game.CoreHealth+18);
            game.SetPhase(MatchPhase.Intermission,20f,"Wave cleared — repair, resupply, and upgrade.");
        }
    }
}
