using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace RhythmGame.Tests
{
    public sealed class LocalizationTests
    {
        private static string TableText { get { return Resources.Load<TextAsset>(Strings.Resource).text; } }

        [Test]
        public void EveryKeyHasTextInEverySupportedLanguage()
        {
            var table = Strings.Parse(TableText);
            Assert.Greater(table.Count, 100);
            foreach (GameLanguage language in Enum.GetValues(typeof(GameLanguage)))
            {
                string code = PlayerSettings.CodeOf(language);
                string[] missing = table.Where(row => !row.Value.ContainsKey(code) || string.IsNullOrEmpty(row.Value[code]))
                    .Select(row => row.Key).ToArray();
                Assert.IsEmpty(missing, "Untranslated for " + code + ": " + string.Join(", ", missing));
            }
        }

        [Test]
        public void EveryKeyUsedInCodeExistsInTheTable()
        {
            var table = Strings.Parse(TableText);
            foreach (string file in Directory.GetFiles("Assets/RhythmGame/Runtime", "*.cs", SearchOption.AllDirectories))
                foreach (Match match in Regex.Matches(File.ReadAllText(file), "Strings\\.T\\(\"([^\"]+)\"\\)"))
                    Assert.IsTrue(table.ContainsKey(match.Groups[1].Value), Path.GetFileName(file) + " uses missing key " + match.Groups[1].Value);
        }

        [Test]
        public void RuntimeCodeHasNoHardCodedChineseOrKoreanText()
        {
            foreach (string file in Directory.GetFiles("Assets/RhythmGame/Runtime", "*.cs", SearchOption.AllDirectories))
                foreach (string line in File.ReadAllLines(file))
                {
                    string code = line.Split(new[] { "//" }, StringSplitOptions.None)[0];
                    Assert.IsFalse(Regex.IsMatch(code, "\"[^\"]*[\\u4e00-\\u9fff\\uac00-\\ud7af][^\"]*\""),
                        Path.GetFileName(file) + ": move this text into strings.csv\n" + line.Trim());
                }
        }

        [Test]
        public void CsvCellsKeepCommasQuotesAndLineBreaksAndRejectDuplicateKeys()
        {
            var table = Strings.Parse("﻿key,zh-Hans,en\r\na,\"一，二, 三\",\"say \"\"hi\"\"\nthere\"\r\nb,只有中文,\n");
            Assert.AreEqual("一，二, 三", table["a"]["zh-Hans"]);
            Assert.AreEqual("say \"hi\"\nthere", table["a"]["en"]);
            Assert.AreEqual("", table["b"]["en"]);
            Assert.Throws<InvalidOperationException>(() => Strings.Parse("key,zh-Hans\nx,1\nx,2\n"));
        }

        [Test]
        public void CharacterTextComesFromTheTableInEachLanguage()
        {
            GameConfig config = RhythmGameData.LoadDefaultConfig();
            CharacterConfig whale = config.Character("whale");
            Assert.AreEqual("天空鲸", whale.Name.Text(GameLanguage.SimplifiedChinese));
            Assert.AreEqual("Sky Whale", whale.Name.Text(GameLanguage.English));
            Assert.AreEqual("하늘고래", whale.Name.Text(GameLanguage.Korean));
            foreach (CharacterConfig character in config.characters)
                Assert.AreEqual(3, character.Tags.Text(GameLanguage.English).Split('|').Length, character.id);
        }
    }
}
