using System;
using System.IO;
using NUnit.Framework;

namespace RhythmGame.Tests
{
    public sealed class ProgressionTests
    {
        private GameConfig config;
        private PlayerData player;
        private ProgressionService service;

        [SetUp]
        public void SetUp()
        {
#if UNITY_EDITOR
            config = RhythmGameData.LoadDefaultConfig();
#else
            config = new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<GameConfig>(
                File.ReadAllText("RhythmGame/Resources/RhythmGame/default_config.json"));
#endif
            player = PlayerData.Create(config);
            service = new ProgressionService(config, player);
        }

        private PlayResult PerfectRun()
        {
            PlaySession session = service.BeginPlay("first_beat_easy");
            for (int i = 0; i < session.NoteCount; i++) session.SubmitTiming(i, 0);
            return service.Complete(session);
        }

        [Test]
        public void SettingsSupportFourLanguagesAndIndependentVolumes()
        {
            Assert.AreEqual(GameLanguage.SimplifiedChinese, player.settings.language);
            Assert.AreEqual("zh-Hans", player.settings.LanguageCode);
            Assert.AreEqual(1f, player.settings.masterVolume);
            Assert.AreEqual(1f, player.settings.musicVolume);
            Assert.AreEqual(1f, player.settings.soundEffectsVolume);
            Assert.IsTrue(service.SetLanguage(GameLanguage.English));
            Assert.AreEqual("en", player.settings.LanguageCode);
            Assert.IsTrue(service.SetLanguage(GameLanguage.Korean));
            Assert.AreEqual("ko", player.settings.LanguageCode);
            Assert.IsFalse(service.SetLanguage(GameLanguage.Korean));
            Assert.IsTrue(service.SetLanguage(GameLanguage.Japanese));
            Assert.AreEqual("ja", player.settings.LanguageCode);
            Assert.IsTrue(service.SetVolumes(0.5f, 0.8f, 0.2f));
            Assert.AreEqual(0.4f, player.settings.EffectiveMusicVolume, 0.000001);
            Assert.AreEqual(0.1f, player.settings.EffectiveSoundEffectsVolume, 0.000001);
            Assert.IsFalse(service.SetVolumes(0.5f, 0.8f, 0.2f));
            service.SetVolumes(0f, 0.8f, 0.2f);
            Assert.AreEqual(0f, player.settings.EffectiveMusicVolume);
            Assert.AreEqual(0.8f, player.settings.musicVolume);
        }

        [Test]
        public void InvalidSettingsAreRejectedWithoutPartialChanges()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => service.SetLanguage((GameLanguage)99));
            Assert.AreEqual(GameLanguage.SimplifiedChinese, player.settings.language);
            Assert.Throws<ArgumentOutOfRangeException>(() => service.SetVolumes(0.2f, 0.3f, float.NaN));
            Assert.AreEqual(1f, player.settings.masterVolume);
            Assert.Throws<ArgumentOutOfRangeException>(() => service.SetVolumes(-0.1f, 1f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => service.SetVolumes(1f, 1.1f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => service.SetVolumes(1f, 1f, float.PositiveInfinity));
        }

        [Test]
        public void SettingsCanChangeDuringSongWithoutChangingScore()
        {
            PlaySession session = service.BeginPlay("first_beat_easy");
            session.SubmitTiming(0, 0);
            service.SetVolumes(0f, 0f, 0f);
            service.SetLanguage(GameLanguage.Korean);
            for (int i = 1; i < session.NoteCount; i++) session.SubmitTiming(i, 0);
            Assert.AreEqual(10460, service.Complete(session).Score);
        }

        [Test]
        public void LegacySettingsMigrationPreservesProgressAndDoesNotResetMute()
        {
            player.saveVersion = 1;
            player.settings = null;
            player.coins = 700;
            player.inputOffsetMs = 25;
            player.FindCharacter("whale").level = 40;
            player.MigrateToCurrentVersion();
            player.Validate(config);
            Assert.AreEqual(2, player.saveVersion);
            Assert.AreEqual(700, player.coins);
            Assert.AreEqual(40, player.FindCharacter("whale").level);
            Assert.AreEqual(25, player.inputOffsetMs);
            Assert.AreEqual(1f, player.settings.masterVolume);
            service.SetVolumes(0f, 0f, 0f);
            service.SetLanguage(GameLanguage.Korean);
            player.MigrateToCurrentVersion();
            Assert.AreEqual(0f, player.settings.masterVolume);
            Assert.AreEqual(GameLanguage.Korean, player.settings.language);
            player.saveVersion = 99;
            Assert.Throws<InvalidOperationException>(() => player.MigrateToCurrentVersion());
        }

        [Test]
        public void EveryCharacterStartsWith100HealthRegardlessOfLevel()
        {
            foreach (CharacterConfig character in config.characters)
            {
                PlayerData other = PlayerData.Create(config);
                if (other.FindCharacter(character.id) == null)
                    other.characters.Add(new OwnedCharacter { characterId = character.id, level = character.maxLevel });
                other.FindCharacter(character.id).level = character.maxLevel;
                other.equippedCharacterId = character.id;
                PlaySession session = new ProgressionService(config, other).BeginPlay("first_beat_easy");
                Assert.AreEqual(100, session.Health);
                Assert.AreEqual(100, session.MaxHealth);
            }
        }

        [Test]
        public void TenMissesKillAndCannotBeRejudgedOrRevived()
        {
            PlaySession session = service.BeginPlay("first_beat_easy");
            for (int i = 0; i < 9; i++)
            {
                HitResult hit = session.SubmitMiss(i);
                Assert.AreEqual(-10, hit.HealthDelta);
                Assert.AreEqual(100 - (i + 1) * 10, hit.Health);
                Assert.IsFalse(hit.HasFailed);
            }
            Assert.Throws<InvalidOperationException>(() => session.SubmitMiss(0));
            Assert.AreEqual(10, session.Health);
            Assert.IsFalse(session.IsFinished);
            Assert.IsTrue(session.SubmitMiss(9).HasFailed);
            Assert.IsTrue(session.IsFinished);
            Assert.IsFalse(session.IsComplete);
            Assert.Throws<InvalidOperationException>(() => session.SubmitTiming(19, 0));
            PlayResult result = service.Complete(session);
            Assert.AreEqual(0, result.RemainingHealth);
            Assert.AreEqual(0, result.TotalCoins);
            Assert.Throws<InvalidOperationException>(() => service.Complete(session));
            Assert.AreEqual(100, service.BeginPlay("first_beat_easy").Health);
        }

        [Test]
        public void DeathOnLastNoteFailsEvenWithHighAccuracy()
        {
            PlaySession session = service.BeginPlay("first_beat_easy");
            for (int i = 0; i < 90; i++) session.SubmitTiming(i, 0);
            for (int i = 90; i < 100; i++) session.SubmitMiss(i);
            PlayResult result = service.Complete(session);
            Assert.AreEqual(0.9, result.Accuracy);
            Assert.IsTrue(result.FailedByHealth);
            Assert.IsFalse(result.Cleared);
            Assert.AreEqual(0, result.TotalCoins);
            Assert.IsFalse(service.IsChartUnlocked("first_beat_normal"));
        }

        [Test]
        public void CharactersHaveNoSkillsSoEveryGuideScoresAndHealsTheSame()
        {
            PlayResult whale = PerfectRun();
            player.coins = 1200;
            service.Purchase("turtle");
            service.Equip("turtle");
            Assert.AreEqual(whale.Score, PerfectRun().Score, "Characters do not change the score.");

            PlaySession session = service.BeginPlay("first_beat_easy");
            for (int i = 0; i < 3; i++) session.SubmitMiss(i);
            for (int i = 3; i < 20; i++) Assert.AreEqual(70, session.SubmitTiming(i, 0).Health, "Hits never restore health.");
            service.Abort(session);
        }

        [Test]
        public void MissDialogueIsThrottledAndComboMilestonesOnlySpeakOncePerRun()
        {
            config.rules.comboDialogueThresholds = new[] { 3 };
            PlaySession session = service.BeginPlay("first_beat_easy");
            session.SubmitTiming(0, 0);
            session.SubmitTiming(1, 0);
            DialogueCue combo = session.SubmitTiming(2, 0).Dialogue;
            Assert.AreEqual(DialogueReason.ComboMilestone, combo.Reason);
            Assert.AreEqual(3, combo.Combo);
            Assert.IsNotEmpty(combo.Line.Text(GameLanguage.Korean));
            Assert.AreEqual(DialogueReason.Miss, session.SubmitMiss(3).Dialogue.Reason);
            Assert.IsNull(session.SubmitMiss(4).Dialogue);
            session.SubmitTiming(5, 0);
            session.SubmitTiming(6, 0);
            Assert.IsNull(session.SubmitTiming(7, 0).Dialogue);
            Assert.AreEqual(DialogueReason.Miss, session.SubmitMiss(8).Dialogue.Reason);
        }

        [Test]
        public void ValidationRejectsBadHealth()
        {
            config.rules.maxHealth = 0;
            Assert.Throws<InvalidOperationException>(() => config.Validate());
        }

        [Test]
        public void GrowthHasExactEndpoints()
        {
            Assert.AreEqual(100, config.Character("whale").BaseScore(1));
            Assert.AreEqual(200, config.Character("whale").BaseScore(100));
            Assert.AreEqual(150.5050505, config.Character("whale").BaseScore(51), 0.000001);
            Assert.AreEqual(120, config.economy.UpgradeCost(1));
            Assert.AreEqual(2080, config.economy.UpgradeCost(99));
        }

        [Test]
        public void JudgementIncludesBoundaryAndAppliesSignedCalibration()
        {
            Assert.AreEqual(Judgement.Perfect, config.rules.Judge(40, 0));
            Assert.AreEqual(Judgement.Great, config.rules.Judge(-40.001, 0));
            Assert.AreEqual(Judgement.Great, config.rules.Judge(-80, 0));
            Assert.AreEqual(Judgement.Good, config.rules.Judge(80.001, 0));
            Assert.AreEqual(Judgement.Good, config.rules.Judge(200, 0));
            Assert.AreEqual(Judgement.Good, config.rules.Judge(350, 0));
            Assert.AreEqual(Judgement.Miss, config.rules.Judge(350.001, 0));
            Assert.AreEqual(Judgement.Miss, config.rules.Judge(-200.001, 0));
            Assert.AreEqual(Judgement.Perfect, config.rules.Judge(65, 30));
            Assert.AreEqual(Judgement.Good, config.rules.Judge(-65, 30));
            Assert.Throws<ArgumentOutOfRangeException>(() => config.rules.Judge(double.NaN, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => service.SetInputOffset(301));
        }

        [Test]
        public void PurchaseUpgradeEquipRespectOwnershipAndBalance()
        {
            Assert.IsFalse(service.Purchase("turtle"));
            Assert.IsFalse(service.Equip("turtle"));
            player.coins = 1320;
            Assert.IsTrue(service.Purchase("turtle"));
            Assert.AreEqual(120, player.coins);
            Assert.IsFalse(service.Purchase("turtle"));
            Assert.IsTrue(service.Equip("turtle"));
            Assert.AreEqual("turtle", player.equippedCharacterId);
            Assert.IsTrue(service.Upgrade("turtle"));
            Assert.AreEqual(2, player.FindCharacter("turtle").level);
            Assert.AreEqual(0, player.coins);
            Assert.IsFalse(service.Upgrade("turtle"));
            player.FindCharacter("turtle").level = 100;
            player.coins = 10000;
            Assert.IsFalse(service.Upgrade("turtle"));
            Assert.AreEqual(10000, player.coins);
        }

        [Test]
        public void PerfectRunHasExpectedScoreRewardsAndUnlocks()
        {
            Assert.IsFalse(service.IsChartUnlocked("first_beat_normal"));
            PlayResult result = PerfectRun();
            Assert.AreEqual(10460, result.Score);
            Assert.AreEqual(1, result.Accuracy);
            Assert.AreEqual("S", result.Grade);
            Assert.AreEqual(100, result.Perfect);
            Assert.AreEqual(100, result.MaxCombo);
            Assert.IsTrue(result.Cleared && result.FullCombo && result.AllPerfect);
            Assert.AreEqual(125, result.BaseCoins);
            Assert.AreEqual(200, result.FirstClearCoins);
            Assert.AreEqual(300, result.FirstFullComboCoins);
            Assert.AreEqual(625, player.coins);
            Assert.IsTrue(service.IsChartUnlocked("first_beat_normal"));
            Assert.IsFalse(service.IsChartUnlocked("first_beat_hard"));
            PlayResult replay = PerfectRun();
            Assert.AreEqual(125, replay.TotalCoins);
            Assert.AreEqual(2, player.FindRecord("first_beat_easy").playCount);
        }

        [Test]
        public void RewardsAndAccuracyDoNotDependOnCharacterLevel()
        {
            PlayResult first = PerfectRun();
            player.FindCharacter("whale").level = 100;
            PlayResult second = PerfectRun();
            Assert.AreEqual(first.Score * 2, second.Score);
            Assert.AreEqual(first.Accuracy, second.Accuracy);
            Assert.AreEqual(first.BaseCoins, second.BaseCoins);
        }

        [Test]
        public void GoodBreaksComboAndUsesItsColour()
        {
            PlaySession session = service.BeginPlay("first_beat_easy");
            HitResult hit = null;
            for (int i = 0; i < 50; i++) hit = session.SubmitTiming(i, 0);
            Assert.AreEqual(50, hit.Combo);
            hit = session.SubmitTiming(50, 100);
            Assert.AreEqual(0, hit.Combo);
            Assert.AreEqual(40, hit.RawScore);
            Assert.AreEqual("#188CC4", hit.ColorHtml);
            Assert.AreEqual(50, session.MaxCombo);
        }

        [Test]
        public void DuplicateNotePartialRunAndRepeatedSettlementAreRejected()
        {
            PlaySession session = service.BeginPlay("first_beat_easy");
            session.SubmitTiming(0, 0);
            Assert.Throws<InvalidOperationException>(() => session.SubmitTiming(0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => session.SubmitMiss(100));
            Assert.Throws<InvalidOperationException>(() => service.Complete(session));
            Assert.AreEqual(0, player.coins);
            for (int i = 1; i < session.NoteCount; i++) session.SubmitTiming(i, 0);
            service.Complete(session);
            Assert.Throws<InvalidOperationException>(() => service.Complete(session));
            Assert.Throws<InvalidOperationException>(() => session.SubmitMiss(0));
            Assert.AreEqual(625, player.coins);
        }

        [Test]
        public void AbortGrantsNothingAndClosesSession()
        {
            PlaySession session = service.BeginPlay("first_beat_easy");
            session.SubmitTiming(0, 0);
            Assert.Throws<InvalidOperationException>(() => service.BeginPlay("first_beat_easy"));
            Assert.Throws<InvalidOperationException>(() => service.Upgrade("whale"));
            service.Abort(session);
            Assert.AreEqual(0, player.records.Count);
            Assert.AreEqual(0, player.coins);
            Assert.Throws<InvalidOperationException>(() => session.SubmitMiss(1));
            Assert.IsNotNull(service.BeginPlay("first_beat_easy"));
        }

        [Test]
        public void FailureKeepsHistoryButGrantsNoCoins()
        {
            PlaySession session = service.BeginPlay("first_beat_easy");
            for (int i = 0; !session.IsFinished; i++) session.SubmitMiss(i);
            PlayResult result = service.Complete(session);
            Assert.AreEqual(0, result.Score);
            Assert.AreEqual(10, result.Miss);
            Assert.AreEqual(90, result.UnplayedNotes);
            Assert.IsTrue(result.FailedByHealth);
            Assert.AreEqual(0, result.TotalCoins);
            Assert.IsFalse(result.Cleared || result.FullCombo || result.AllPerfect);
            Assert.IsFalse(service.IsChartUnlocked("first_beat_normal"));
            Assert.AreEqual(1, player.records.Count);
        }

        [Test]
        public void FullComboBonusCanBeEarnedAfterFirstClear()
        {
            PlaySession session = service.BeginPlay("first_beat_easy");
            for (int i = 0; i < 99; i++) session.SubmitTiming(i, 0);
            session.SubmitTiming(99, 100);
            PlayResult clear = service.Complete(session);
            Assert.IsTrue(clear.Cleared);
            Assert.IsFalse(clear.FullCombo);
            Assert.AreEqual(200, clear.FirstClearCoins);
            PlayResult fc = PerfectRun();
            Assert.AreEqual(0, fc.FirstClearCoins);
            Assert.AreEqual(300, fc.FirstFullComboCoins);
        }

        [Test]
        public void BestRecordsNeverRegressAndComboBonusCaps()
        {
            PerfectRun();
            PlaySession longSong = service.BeginPlay("first_beat_normal");
            HitResult hit = null;
            for (int i = 0; i < longSong.NoteCount; i++) hit = longSong.SubmitTiming(i, 0);
            Assert.AreEqual(110, hit.RawScore, 0.000001);
            service.Complete(longSong);
            PlaySession failed = service.BeginPlay("first_beat_easy");
            for (int i = 0; !failed.IsFinished; i++) failed.SubmitMiss(i);
            service.Complete(failed);
            ChartRecord record = player.FindRecord("first_beat_easy");
            Assert.AreEqual(10460, record.bestScore);
            Assert.AreEqual(1, record.bestAccuracy);
            Assert.IsTrue(record.allPerfect);
        }

        [Test]
        public void ValidationRejectsCyclicUnlockAndInvalidSave()
        {
            config.charts[0].requiredClearChartId = "first_beat_hard";
            Assert.Throws<InvalidOperationException>(() => config.Validate());
            config.charts[0].requiredClearChartId = "";
            player.equippedCharacterId = "turtle";
            Assert.Throws<InvalidOperationException>(() => player.Validate(config));
        }
    }
}
