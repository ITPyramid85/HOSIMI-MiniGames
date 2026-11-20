using System;
using System.Collections.Generic;

namespace RhythmGame
{
    [Serializable]
    public sealed class OwnedCharacter
    {
        public string characterId;
        public int level;
    }

    [Serializable]
    public sealed class ChartRecord
    {
        public string chartId;
        public long bestScore;
        public double bestAccuracy;
        public int maxCombo;
        public int playCount;
        public bool cleared;
        public bool fullCombo;
        public bool allPerfect;
    }

    // Best results per mini-game (client reply v5 §1-4: records use best stage and longest time).
    [Serializable]
    public sealed class MiniGameRecord
    {
        public string gameId;
        public int bestStage;
        public double bestSeconds;
        public int plays;
        public long totalDrumsticks;
        // Longest echoed phrase, and the highest Tapping Beat stage reached (1..3). Older saves leave both at 0.
        public int bestBeats;
        public int echoStage;
        // Tortoise-race wins. Older saves leave this at 0, which is the meadow and a plain shell.
        public int raceWins;
        // Furthest distance reached in Wall Breaker. Older saves leave this at 0.
        public int wallMeters;
    }

    [Serializable]
    public sealed class PlayerData
    {
        public const int CurrentSaveVersion = 2;
        public int saveVersion = CurrentSaveVersion;
        public long coins;
        public string equippedCharacterId;
        public double inputOffsetMs;
        public PlayerSettings settings = new PlayerSettings();
        public List<OwnedCharacter> characters = new List<OwnedCharacter>();
        public List<ChartRecord> records = new List<ChartRecord>();
        // Optional: saves from before mini-games have none.
        public List<MiniGameRecord> miniGames = new List<MiniGameRecord>();
        // Species ids caught in ice fishing. Missing on older saves.
        public List<string> fishDex = new List<string>();
        // Rice-cake kinds collected in mochi pounding. Missing on older saves.
        public List<string> mochiDex = new List<string>();

        public OwnedCharacter FindCharacter(string id) { return characters.Find(c => c.characterId == id); }
        public ChartRecord FindRecord(string id) { return records.Find(r => r.chartId == id); }
        public MiniGameRecord FindMiniGame(string id) { return miniGames.Find(r => r.gameId == id); }

        // Records one ice-fishing species. A repeat catch stays a single dex entry.
        public bool DiscoverFish(string species)
        {
            if (fishDex == null) fishDex = new List<string>();
            if (string.IsNullOrEmpty(species) || Array.IndexOf(IceChart.Species, species) < 0)
                throw new ArgumentException("Unknown fish.");
            if (fishDex.Contains(species)) return false;
            fishDex.Add(species);
            return true;
        }

        // Records one rice-cake kind. Bean powder comes first. Rainbow is the last.
        public bool DiscoverMochi(string kind)
        {
            if (mochiDex == null) mochiDex = new List<string>();
            if (kind != "bean" && kind != "rainbow") throw new ArgumentException("Unknown rice cake.");
            if (mochiDex.Contains(kind)) return false;
            mochiDex.Add(kind);
            return true;
        }

        public static PlayerData Create(GameConfig config)
        {
            config.Validate();
            var player = new PlayerData { coins = config.economy.startingCoins, equippedCharacterId = config.starterCharacterId };
            player.characters.Add(new OwnedCharacter { characterId = config.starterCharacterId, level = 1 });
            return player;
        }

        // v1 had no settings. Keep all progression and calibration data when upgrading.
        public void MigrateToCurrentVersion()
        {
            if (saveVersion == 1)
            {
                settings = new PlayerSettings();
                saveVersion = CurrentSaveVersion;
            }
            else if (saveVersion != CurrentSaveVersion)
                throw new InvalidOperationException("Unsupported player save version.");
            // The three placeholder characters became the first three of the six drum animals.
            foreach (OwnedCharacter c in characters ?? new List<OwnedCharacter>()) if (c != null) c.characterId = RenamedCharacter(c.characterId);
            equippedCharacterId = RenamedCharacter(equippedCharacterId);
        }

        private static string RenamedCharacter(string id)
        {
            return id == "steady" ? "whale" : id == "precise" ? "turtle" : id == "combo" ? "lion" : id;
        }

        public void Validate(GameConfig config)
        {
            if (saveVersion != CurrentSaveVersion || coins < 0 || characters == null || records == null || settings == null ||
                !GameConfig.IsFinite(inputOffsetMs) || Math.Abs(inputOffsetMs) > config.rules.maxCalibrationMs)
                throw new InvalidOperationException("Invalid or unsupported player save.");
            settings.Validate();
            if (miniGames == null) miniGames = new List<MiniGameRecord>();
            if (fishDex == null) fishDex = new List<string>();
            if (mochiDex == null) mochiDex = new List<string>();
            var discovered = new HashSet<string>();
            foreach (string species in fishDex)
                if (string.IsNullOrEmpty(species) || !discovered.Add(species) || Array.IndexOf(IceChart.Species, species) < 0)
                    throw new InvalidOperationException("Invalid fish dex.");
            discovered.Clear();
            foreach (string kind in mochiDex)
                if ((kind != "bean" && kind != "rainbow") || !discovered.Add(kind))
                    throw new InvalidOperationException("Invalid rice-cake collection.");
            var games = new HashSet<string>();
            foreach (MiniGameRecord g in miniGames)
                if (g == null || string.IsNullOrEmpty(g.gameId) || !games.Add(g.gameId) || g.bestStage < 1 || g.bestStage > StageTable.Count ||
                    !GameConfig.IsFinite(g.bestSeconds) || g.bestSeconds < 0 || g.plays < 0 || g.totalDrumsticks < 0 || g.bestBeats < 0 ||
                    g.echoStage < 0 || g.echoStage > 3 || g.raceWins < 0 || g.wallMeters < 0)
                    throw new InvalidOperationException("Invalid mini-game record.");
            var ids = new HashSet<string>();
            foreach (OwnedCharacter c in characters)
            {
                if (c == null || string.IsNullOrEmpty(c.characterId) || !ids.Add(c.characterId) || c.level < 1 ||
                    c.level > config.Character(c.characterId).maxLevel) throw new InvalidOperationException("Invalid owned character.");
            }
            if (FindCharacter(equippedCharacterId) == null) throw new InvalidOperationException("Equipped character is not owned.");
            ids.Clear();
            foreach (ChartRecord r in records)
            {
                if (r == null || string.IsNullOrEmpty(r.chartId) || !ids.Add(r.chartId)) throw new InvalidOperationException("Invalid chart record.");
                ChartConfig chart = config.Chart(r.chartId);
                if (r.bestScore < 0 || !GameConfig.IsFinite(r.bestAccuracy) || r.bestAccuracy < 0 || r.bestAccuracy > 1 ||
                    r.maxCombo < 0 || r.maxCombo > chart.noteCount || r.playCount < 0 ||
                    (r.fullCombo && !r.cleared) || (r.allPerfect && !r.fullCombo)) throw new InvalidOperationException("Invalid chart statistics.");
            }
        }
    }
}
