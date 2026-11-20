using System;
using System.Collections.Generic;

namespace RhythmGame
{
    public enum Judgement { Perfect, Great, Good, Miss }

    [Serializable]
    public sealed class CharacterConfig
    {
        public string id;
        public string portraitResource;
        public int purchasePrice;
        public string worldColorHtml;
        // Drumsticks for the physical-merch claim, and the official-store page for this character.
        public int physicalPrice;
        public string merchUrl;
        public int maxLevel;
        public double baseScoreAtLevel1;
        public double baseScoreAtMaxLevel;

        // Text lives in the string table under character.<id>.<part> (plan 2-2 / 2-4 story card).
        public CharacterLine Name { get { return Text("name"); } }
        public CharacterLine World { get { return Text("world"); } }
        public CharacterLine Tags { get { return Text("tags"); } }   // chips separated by '|'
        public CharacterLine Story { get { return Text("story"); } }
        public CharacterLine MissLine { get { return Text("miss"); } }
        public CharacterLine ComboLine { get { return Text("combo"); } }
        public string DisplayName { get { return Name.Current; } }
        private CharacterLine Text(string part) { return new CharacterLine("character." + id + "." + part); }

        public double BaseScore(int level)
        {
            if (level < 1 || level > maxLevel) throw new ArgumentOutOfRangeException("level");
            return maxLevel == 1 ? baseScoreAtLevel1 : baseScoreAtLevel1 +
                (baseScoreAtMaxLevel - baseScoreAtLevel1) * (level - 1) / (maxLevel - 1);
        }
    }

    [Serializable]
    public sealed class ChartConfig
    {
        public string id;
        public string songId;
        public string songTitle;
        public string difficulty;
        public int difficultyLevel;
        public int noteCount;
        public string audioResource;
        public string chartResource;
        public bool isExample;
        public string requiredClearChartId;
    }

    [Serializable]
    public sealed class JudgementConfig
    {
        public Judgement judgement;
        public double windowMs;
        public double scoreWeight;
        public double accuracyWeight;
        public bool keepsCombo;
        public string colorHtml;
    }

    [Serializable]
    public sealed class RulesConfig
    {
        // Audio timestamps are seconds converted to milliseconds. Keep inclusive edges stable under rounding.
        internal const double TimingToleranceMs = 0.000001;
        public int comboStep;
        public double comboBonusPerStep;
        public double maxComboBonus;
        public double clearAccuracy;
        public double maxCalibrationMs;
        // Optional late-only extension; zero keeps older configurations symmetric.
        public double lateGoodWindowMs;
        public double LateGoodWindowMs
        {
            get { return lateGoodWindowMs > 0 ? lateGoodWindowMs : GetJudgement(Judgement.Good).windowMs; }
        }
        public double gradeS;
        public double gradeA;
        public double gradeB;
        public double gradeC;
        public JudgementConfig[] judgements;
        public int maxHealth;
        public int missDamage;
        public int[] comboDialogueThresholds;
        public int missDialogueCooldownNotes;

        public JudgementConfig GetJudgement(Judgement judgement)
        {
            foreach (JudgementConfig row in judgements)
                if (row.judgement == judgement) return row;
            throw new ArgumentException("Unknown judgement: " + judgement);
        }

        // Positive calibration means the player's input tends to arrive late.
        public Judgement Judge(double rawOffsetMs, double calibrationMs)
        {
            if (!GameConfig.IsFinite(rawOffsetMs) || !GameConfig.IsFinite(calibrationMs) ||
                Math.Abs(calibrationMs) > maxCalibrationMs) throw new ArgumentOutOfRangeException("calibrationMs");
            double signedError = rawOffsetMs - calibrationMs;
            double error = Math.Abs(signedError);
            if (error <= GetJudgement(Judgement.Perfect).windowMs + TimingToleranceMs) return Judgement.Perfect;
            if (error <= GetJudgement(Judgement.Great).windowMs + TimingToleranceMs) return Judgement.Great;
            double goodWindow = signedError > 0 ? LateGoodWindowMs : GetJudgement(Judgement.Good).windowMs;
            if (error <= goodWindow + TimingToleranceMs) return Judgement.Good;
            return Judgement.Miss;
        }

        public string Grade(double accuracy)
        {
            if (accuracy >= gradeS) return "S";
            if (accuracy >= gradeA) return "A";
            if (accuracy >= gradeB) return "B";
            if (accuracy >= gradeC) return "C";
            return "D";
        }
    }

    [Serializable]
    public sealed class EconomyConfig
    {
        public int startingCoins;
        public int upgradeBaseCost;
        public int upgradeCostPerLevel;
        public int clearBaseCoins;
        public int coinsPerDifficultyLevel;
        public double minimumClearRewardRatio;
        public int firstClearCoins;
        public int firstFullComboCoins;

        public int UpgradeCost(int currentLevel)
        {
            if (currentLevel < 1) throw new ArgumentOutOfRangeException("currentLevel");
            return checked(upgradeBaseCost + upgradeCostPerLevel * currentLevel);
        }
    }

    [Serializable]
    public sealed class GameConfig
    {
        public int schemaVersion;
        public string starterCharacterId;
        public CharacterConfig[] characters;
        public ChartConfig[] charts;
        public RulesConfig rules;
        public EconomyConfig economy;

        public CharacterConfig Character(string id)
        {
            foreach (CharacterConfig row in characters) if (row.id == id) return row;
            throw new ArgumentException("Unknown character: " + id);
        }

        public ChartConfig Chart(string id)
        {
            foreach (ChartConfig row in charts) if (row.id == id) return row;
            throw new ArgumentException("Unknown chart: " + id);
        }

        public static bool IsFinite(double value) { return !double.IsNaN(value) && !double.IsInfinity(value); }
        private static void Require(bool valid, string message)
        {
            if (!valid) throw new InvalidOperationException("Invalid rhythm config: " + message);
        }

        public void Validate()
        {
            Require(schemaVersion == 2, "unsupported schemaVersion");
            Require(characters != null && characters.Length > 0 && charts != null && charts.Length > 0,
                "characters and charts must not be empty");
            Require(rules != null && economy != null, "missing rules/economy");
            var ids = new HashSet<string>();
            foreach (CharacterConfig c in characters)
            {
                Require(c != null && !string.IsNullOrEmpty(c.id) && ids.Add(c.id), "duplicate/empty character id");
                Require(c.purchasePrice >= 0 && c.maxLevel >= 1 && c.maxLevel <= 10000,
                    "invalid character: " + c.id);
                Require(IsFinite(c.baseScoreAtLevel1) && IsFinite(c.baseScoreAtMaxLevel) &&
                    c.baseScoreAtLevel1 > 0 && c.baseScoreAtMaxLevel >= c.baseScoreAtLevel1 &&
                    c.baseScoreAtMaxLevel <= 1000000, "invalid base score: " + c.id);
                Require(c.physicalPrice >= 0, "invalid physical price: " + c.id);
                foreach (CharacterLine text in new[] { c.Name, c.World, c.Tags, c.Story, c.MissLine, c.ComboLine }) text.Validate();
                economy.UpgradeCost(c.maxLevel);
            }
            Character(starterCharacterId);
            ids.Clear();
            foreach (ChartConfig c in charts)
            {
                Require(c != null && !string.IsNullOrEmpty(c.id) && ids.Add(c.id), "duplicate/empty chart id");
                Require(!string.IsNullOrEmpty(c.songId) && !string.IsNullOrEmpty(c.songTitle) &&
                    !string.IsNullOrEmpty(c.difficulty) && c.noteCount > 0 && c.noteCount <= 1000000 &&
                    c.difficultyLevel > 0 && c.difficultyLevel <= 100, "invalid chart: " + c.id);
                Require(c.isExample || (!string.IsNullOrEmpty(c.audioResource) && !string.IsNullOrEmpty(c.chartResource)),
                    "production chart needs audio/chart resources: " + c.id);
            }
            foreach (ChartConfig c in charts)
            {
                var chain = new HashSet<string>();
                ChartConfig cursor = c;
                while (cursor != null)
                {
                    Require(chain.Add(cursor.id), "cyclic chart unlock: " + c.id);
                    cursor = string.IsNullOrEmpty(cursor.requiredClearChartId) ? null : Chart(cursor.requiredClearChartId);
                }
            }
            Require(rules.judgements != null && rules.judgements.Length == 4, "need four judgements");
            var kinds = new HashSet<Judgement>();
            foreach (JudgementConfig j in rules.judgements)
            {
                Require(j != null && Enum.IsDefined(typeof(Judgement), j.judgement) && kinds.Add(j.judgement), "duplicate judgement");
                Require(IsFinite(j.windowMs) && j.windowMs >= 0 && IsFinite(j.scoreWeight) && j.scoreWeight >= 0 && j.scoreWeight <= 1 &&
                    IsFinite(j.accuracyWeight) && j.accuracyWeight >= 0 && j.accuracyWeight <= 1, "invalid judgement weights/window");
                Require(IsHtmlColor(j.colorHtml), "color must be #RRGGBB or #RRGGBBAA");
            }
            JudgementConfig p = rules.GetJudgement(Judgement.Perfect);
            JudgementConfig g = rules.GetJudgement(Judgement.Great);
            JudgementConfig good = rules.GetJudgement(Judgement.Good);
            JudgementConfig miss = rules.GetJudgement(Judgement.Miss);
            Require(p.windowMs > 0 && p.windowMs < g.windowMs && g.windowMs < good.windowMs, "windows must increase");
            Require(IsFinite(rules.lateGoodWindowMs) && (rules.lateGoodWindowMs == 0 || rules.lateGoodWindowMs >= good.windowMs),
                "late Good window must be zero (symmetric) or at least the early Good window");
            Require(p.accuracyWeight == 1 && p.scoreWeight == 1 && miss.accuracyWeight == 0 && miss.scoreWeight == 0 &&
                p.keepsCombo && g.keepsCombo && !good.keepsCombo && !miss.keepsCombo, "invalid judgement semantics");
            Require(rules.comboStep > 0 && IsFinite(rules.comboBonusPerStep) && rules.comboBonusPerStep >= 0 &&
                IsFinite(rules.maxComboBonus) && rules.maxComboBonus >= 0 && rules.maxComboBonus <= 10, "invalid combo");
            Require(rules.maxHealth > 0 && rules.maxHealth <= 1000000 && rules.missDamage > 0 &&
                rules.missDamage <= rules.maxHealth, "invalid health rules");
            Require(rules.missDialogueCooldownNotes > 0 && rules.comboDialogueThresholds != null,
                "invalid dialogue rules");
            int previousThreshold = 0;
            foreach (int threshold in rules.comboDialogueThresholds)
            {
                Require(threshold > previousThreshold, "combo dialogue thresholds must be positive and increasing");
                previousThreshold = threshold;
            }
            Require(IsFinite(rules.clearAccuracy) && rules.clearAccuracy > 0 && rules.clearAccuracy <= 1 &&
                IsFinite(rules.maxCalibrationMs) && rules.maxCalibrationMs >= 0, "invalid clear/calibration");
            Require(IsFinite(rules.gradeS) && IsFinite(rules.gradeA) && IsFinite(rules.gradeB) && IsFinite(rules.gradeC) &&
                rules.gradeS <= 1 && rules.gradeS > rules.gradeA && rules.gradeA > rules.gradeB &&
                rules.gradeB > rules.gradeC && rules.gradeC >= 0, "invalid grade thresholds");
            Require(economy.startingCoins >= 0 && economy.upgradeBaseCost >= 0 && economy.upgradeCostPerLevel >= 0 &&
                economy.clearBaseCoins >= 0 && economy.coinsPerDifficultyLevel >= 0 && economy.firstClearCoins >= 0 &&
                economy.firstFullComboCoins >= 0 && IsFinite(economy.minimumClearRewardRatio) &&
                economy.minimumClearRewardRatio >= 0 && economy.minimumClearRewardRatio <= 1, "invalid economy");
        }

        private static bool IsHtmlColor(string value)
        {
            if (string.IsNullOrEmpty(value) || (value.Length != 7 && value.Length != 9) || value[0] != '#') return false;
            for (int i = 1; i < value.Length; i++) if (!Uri.IsHexDigit(value[i])) return false;
            return true;
        }
    }
}
