using NUnit.Framework;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;

namespace RLSurvival.Tests
{
    public sealed class GameFlowTests
    {
        GameObject host;

        [TearDown]
        public void TearDown()
        {
            if (host != null) Object.Destroy(host);
        }

        [Test]
        public void PhaseHelpersMatchPlayableLoop()
        {
            Assert.IsFalse(MatchPhase.Lobby.IsPlaying());
            Assert.IsTrue(MatchPhase.Build.IsPlaying());
            Assert.IsTrue(MatchPhase.Wave.IsPlaying());
            Assert.IsTrue(MatchPhase.Intermission.IsPlaying());
            Assert.IsFalse(MatchPhase.GameOver.IsPlaying());
        }

        [Test]
        public void DifficultyProfilesStayOrderedAndBounded()
        {
            var rookie = AgentTuningCatalog.Get(AgentDifficulty.Rookie, 1);
            var standard = AgentTuningCatalog.Get(AgentDifficulty.Standard, 1);
            var veteran = AgentTuningCatalog.Get(AgentDifficulty.Veteran, 1);
            Assert.Less(rookie.HealthScale, standard.HealthScale);
            Assert.Less(standard.HealthScale, veteran.HealthScale);
            Assert.That(rookie.Exploration, Is.InRange(.12f, .34f));
            Assert.That(veteran.SpawnScale, Is.GreaterThan(1f));
            Assert.That(AgentTuningCatalog.Get(AgentDifficulty.Veteran, 200).Exploration, Is.GreaterThanOrEqualTo(.04f));
        }

        [Test]
        public void CatalogAndAgentCopiesPreserveGameData()
        {
            Assert.AreEqual(6, UpgradeCatalog.Size(UpgradeKind.Weapon));
            Assert.AreEqual(8, UpgradeCatalog.Size(UpgradeKind.Block));
            Assert.AreEqual(6, UpgradeCatalog.Size(UpgradeKind.Perk));
            Assert.AreEqual("Carbine", UpgradeCatalog.Name(UpgradeKind.Weapon, 0));
            Assert.AreEqual(1100, UpgradeCatalog.Blocks[5].Durability);

            var model = new RaiderModel { id = "test", name = "Test Raider", trainingEpisodes = 600 };
            model.qValues[0] = 12.5f;
            var copy = model.Copy();
            copy.qValues[0] = 99;
            Assert.AreEqual(12.5f, model.qValues[0]);
            Assert.AreEqual(600, copy.trainingEpisodes);
        }

        [UnityTest]
        public IEnumerator SoloMatchStartsAtBuildPhaseAndResetsMatchState()
        {
            host = new GameObject("Test survival game");
            var game = host.AddComponent<SurvivalGame>();
            yield return null;
            game.Wave = 4;
            game.Kills = 23;
            game.CoreHealth = 5;

            game.MatchDirector.StartMatch(new MatchOptions { algorithm = "SARSA", difficulty = "Veteran" });

            Assert.AreEqual(MatchPhase.Build, game.Phase);
            Assert.AreEqual(0, game.Wave);
            Assert.AreEqual(0, game.Kills);
            Assert.AreEqual(100, game.CoreHealth);
            Assert.AreEqual(100, game.MaxCoreHealth);
            Assert.AreEqual(RaiderAlgorithm.SARSA, game.Options.Algorithm);
            Assert.AreEqual(AgentDifficulty.Veteran, game.Options.Difficulty);
            Assert.That(game.PhaseRemaining, Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator MatchAdvancesThroughWaveIntermissionAndNextWave()
        {
            host = new GameObject("Test wave progression");
            var game = host.AddComponent<SurvivalGame>();
            yield return null;
            game.MatchDirector.StartMatch(new MatchOptions());

            game.MatchDirector.Tick(36f);
            Assert.AreEqual(MatchPhase.Wave, game.Phase);
            Assert.AreEqual(1, game.Wave);

            game.MatchDirector.CompleteWave();
            Assert.AreEqual(MatchPhase.Intermission, game.Phase);

            game.MatchDirector.Tick(21f);
            Assert.AreEqual(MatchPhase.Wave, game.Phase);
            Assert.AreEqual(2, game.Wave);
            Assert.That(game.PhaseRemaining, Is.GreaterThan(0));
        }
    }
}
