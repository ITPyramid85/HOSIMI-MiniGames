using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace RhythmGame.Tests
{
    public sealed class SaveTests
    {
        private string directory;
        private GameConfig config;
        private JsonPlayerStore store;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "RhythmDataTests-" + Guid.NewGuid().ToString("N"));
            config = RhythmGameData.LoadDefaultConfig();
            store = new JsonPlayerStore(Path.Combine(directory, "player.json"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [Test]
        public void JsonRoundTripPreservesProgressAndCalibration()
        {
            PlayerData player = PlayerData.Create(config);
            player.coins = 2000;
            var service = new ProgressionService(config, player);
            service.Purchase("turtle");
            service.Equip("turtle");
            service.Upgrade("turtle");
            service.SetInputOffset(-25);
            service.SetLanguage(GameLanguage.Korean);
            service.SetVolumes(0f, 0.75f, 0.25f);
            PlaySession session = service.BeginPlay("first_beat_easy");
            for (int i = 0; i < session.NoteCount; i++) session.SubmitTiming(i, -25);
            service.Complete(session);
            store.Save(player, config);
            PlayerData loaded = store.LoadOrCreate(config);
            Assert.AreEqual(JsonUtility.ToJson(player), JsonUtility.ToJson(loaded));
            Assert.IsFalse(File.Exists(store.FilePath + ".tmp"));
        }

        [Test]
        public void LegacyJsonLoadsWithSettingsDefaultsAndPreservesProgress()
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(store.FilePath, "{\"saveVersion\":1,\"coins\":700,\"equippedCharacterId\":\"steady\"," +
                "\"inputOffsetMs\":25,\"characters\":[{\"characterId\":\"steady\",\"level\":40}],\"records\":[]}");
            PlayerData loaded = store.LoadOrCreate(config);
            Assert.AreEqual(2, loaded.saveVersion);
            Assert.AreEqual(700, loaded.coins);
            Assert.AreEqual(40, loaded.FindCharacter("whale").level, "Placeholder ids map onto the new characters.");
            Assert.AreEqual("whale", loaded.equippedCharacterId);
            Assert.AreEqual(25, loaded.inputOffsetMs);
            Assert.AreEqual(GameLanguage.SimplifiedChinese, loaded.settings.language);
            Assert.AreEqual(1f, loaded.settings.masterVolume);
            Assert.AreEqual(1f, loaded.settings.musicVolume);
            Assert.AreEqual(1f, loaded.settings.soundEffectsVolume);
            store.Save(loaded, config);
            Assert.AreEqual(JsonUtility.ToJson(loaded), JsonUtility.ToJson(store.LoadOrCreate(config)));
        }

        [Test]
        public void CorruptPrimaryRecoversBackupWithoutOverwritingFiles()
        {
            PlayerData player = PlayerData.Create(config);
            store.Save(player, config);
            player.coins = 123;
            store.Save(player, config);
            File.WriteAllText(store.FilePath, "broken");
            Assert.AreEqual(0, store.LoadOrCreate(config).coins);
            Assert.IsTrue(store.RecoveredFromBackup);
            Assert.AreEqual("broken", File.ReadAllText(store.FilePath));
        }

        [Test]
        public void CorruptSaveWithoutBackupFailsInsteadOfResetting()
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(store.FilePath, "broken");
            Assert.Throws<InvalidDataException>(() => store.LoadOrCreate(config));
            Assert.AreEqual("broken", File.ReadAllText(store.FilePath));
        }
    }
}
