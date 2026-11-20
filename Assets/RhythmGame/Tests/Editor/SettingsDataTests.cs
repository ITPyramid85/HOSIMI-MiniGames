using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace RhythmGame.Tests
{
    public sealed class SettingsDataTests
    {
        private string directory, path;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "RhythmSettingsTests-" + Guid.NewGuid().ToString("N"));
            path = Path.Combine(directory, "player.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [Test]
        public void SettingsScreenListsEveryLanguageOnceInChineseJapaneseEnglishKoreanOrder()
        {
            CollectionAssert.AreEqual(new[] { GameLanguage.SimplifiedChinese, GameLanguage.Japanese, GameLanguage.English, GameLanguage.Korean },
                PlayerSettings.DisplayOrder);
            CollectionAssert.AreEquivalent(Enum.GetValues(typeof(GameLanguage)), PlayerSettings.DisplayOrder);
            Assert.AreEqual(4, PlayerSettings.DisplayOrder.Select(PlayerSettings.CodeOf).Distinct().Count());
            Assert.AreEqual(3, (int)GameLanguage.Japanese, "Saved as a number: existing values must not move.");
        }

        [Test]
        public void LanguageAndVolumesPersistThroughTheSaveFile()
        {
            RhythmGameData data = RhythmGameData.Open(path);
            int events = 0;
            data.SettingsChanged += () => events++;
            Assert.IsTrue(data.SetLanguage(GameLanguage.Japanese));
            Assert.IsFalse(data.SetLanguage(GameLanguage.Japanese), "No change, no event.");
            Assert.AreEqual(1, events);
            Assert.AreEqual(GameLanguage.Japanese, RhythmGameData.Open(path).Settings.language, "Language saves at once.");

            Assert.IsTrue(data.PreviewVolumes(.5f, .3f, .8f));
            Assert.AreEqual(2, events);
            Assert.AreEqual(.15f, data.Settings.EffectiveMusicVolume, 1e-6, "Applied in memory immediately.");
            Assert.IsTrue(data.HasUnsavedChanges);
            Assert.AreEqual(1f, RhythmGameData.Open(path).Settings.masterVolume, "A preview does not write the file.");
            data.Save();
            PlayerSettings loaded = RhythmGameData.Open(path).Settings;
            Assert.AreEqual(.5f, loaded.masterVolume);
            Assert.AreEqual(.3f, loaded.musicVolume);
            Assert.AreEqual(.8f, loaded.soundEffectsVolume);
            Assert.AreEqual(GameLanguage.Japanese, loaded.language);
        }

        [Test]
        public void JapaneseTextComesFromItsOwnColumn()
        {
            Assert.AreEqual("設定", Strings.Get("settings.title", GameLanguage.Japanese));
            Assert.AreEqual("空クジラ", RhythmGameData.LoadDefaultConfig().Character("whale").Name.Text(GameLanguage.Japanese));
            Assert.AreEqual("日本語", Strings.Get("language.ja", GameLanguage.Korean), "Language names stay in their own script.");
        }
    }
}
