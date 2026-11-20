using System;
using System.IO;
using UnityEngine;

namespace RhythmGame
{
    // Keep one instance in the game's application/bootstrap object.
    public sealed class RhythmGameData
    {
        private readonly JsonPlayerStore store;
        private readonly ProgressionService progression;
        public GameConfig Config { get; private set; }
        public PlayerData Player { get { return progression.Player; } }
        public PlayerSettings Settings { get { return Player.settings; } }
        public bool HasUnsavedChanges { get; private set; }
        public bool RecoveredFromBackup { get { return store.RecoveredFromBackup; } }
        public string SavePath { get { return store.FilePath; } }
        // Language or volume changed: open screens relabel, audio sources re-apply their volume.
        public event Action SettingsChanged;

        public static GameConfig LoadDefaultConfig()
        {
            TextAsset asset = Resources.Load<TextAsset>("RhythmGame/default_config");
            if (asset == null) throw new InvalidOperationException("Missing RhythmGame/default_config.json.");
            GameConfig config = JsonUtility.FromJson<GameConfig>(asset.text);
            if (config == null) throw new InvalidOperationException("Empty rhythm config.");
            config.Validate();
            return config;
        }

        public static RhythmGameData Open(string savePath = null)
        {
            GameConfig config = LoadDefaultConfig();
            var store = new JsonPlayerStore(savePath ?? Path.Combine(Application.persistentDataPath, "rhythm-player-v1.json"));
            return new RhythmGameData(config, store, store.LoadOrCreate(config));
        }

        private RhythmGameData(GameConfig config, JsonPlayerStore store, PlayerData player)
        {
            Config = config;
            this.store = store;
            progression = new ProgressionService(config, player);
        }

        public bool Purchase(string id) { return SaveIfChanged(progression.Purchase(id)); }
        public bool Upgrade(string id) { return SaveIfChanged(progression.Upgrade(id)); }
        public bool Equip(string id) { return SaveIfChanged(progression.Equip(id)); }
        public bool AddDrumsticks(long amount) { return SaveIfChanged(progression.AddDrumsticks(amount)); }
        public bool DiscoverFish(string species) { return SaveIfChanged(Player.DiscoverFish(species)); }
        public bool DiscoverMochi(string kind) { return SaveIfChanged(Player.DiscoverMochi(kind)); }

        // Keeps the longest echoed phrase. The shared record still stores stage and time.
        public bool RememberEchoBeats(int beats)
        {
            if (beats < 1) return false;
            MiniGameRecord record = Player.FindMiniGame("echo");
            if (record == null || beats <= record.bestBeats) return false;
            record.bestBeats = beats;
            return SaveIfChanged(true);
        }

        // Highest Tapping Beat stage the player has opened. The menu shows this with the longest time.
        public bool RememberEchoStage(int stage)
        {
            if (stage < 1 || stage > 3) return false;
            MiniGameRecord record = Player.FindMiniGame("echo");
            if (record == null || stage <= record.echoStage) return false;
            record.echoStage = stage;
            return SaveIfChanged(true);
        }

        // One tortoise-race win. The next race uses the course and shell this count unlocks.
        public int RememberRaceWin()
        {
            MiniGameRecord record = Player.FindMiniGame("race");
            if (record == null) return 0;
            record.raceWins++;
            SaveIfChanged(true);
            return record.raceWins;
        }

        // Furthest Wall Breaker distance. No-ops until the shared record exists, and never shortens it.
        public int RememberWallDistance(int meters)
        {
            if (meters < 0) return 0;
            MiniGameRecord record = Player.FindMiniGame("wall");
            if (record == null) return 0;
            if (meters > record.wallMeters)
            {
                record.wallMeters = meters;
                SaveIfChanged(true);
            }
            return record.wallMeters;
        }
        public MiniGameResult RecordMiniGame(string gameId, double seconds, int stage, bool cleared)
        {
            MiniGameResult result = progression.RecordMiniGame(gameId, seconds, stage, cleared);
            SaveIfChanged(true);
            return result;
        }
        public bool SetLanguage(GameLanguage language) { return NotifySettings(SaveIfChanged(progression.SetLanguage(language))); }
        public bool SetVolumes(float master, float music, float soundEffects)
        {
            return NotifySettings(SaveIfChanged(progression.SetVolumes(master, music, soundEffects)));
        }

        // Applies volumes immediately without writing to disk, for slider drags; Save() persists them.
        public bool PreviewVolumes(float master, float music, float soundEffects)
        {
            bool changed = progression.SetVolumes(master, music, soundEffects);
            if (changed) HasUnsavedChanges = true;
            return NotifySettings(changed);
        }

        private bool NotifySettings(bool changed)
        {
            if (changed && SettingsChanged != null) SettingsChanged();
            return changed;
        }
        public bool IsChartUnlocked(string id) { return progression.IsChartUnlocked(id); }
        public void SetInputOffset(double ms) { progression.SetInputOffset(ms); SaveIfChanged(true); }
        public bool SetBluetoothCompensation(bool enabled, double ms)
        {
            return SaveIfChanged(progression.SetBluetoothCompensation(enabled, ms));
        }
        public PlaySession BeginPlay(string chartId) { return progression.BeginPlay(chartId); }
        public void Abort(PlaySession session) { progression.Abort(session); }

        public PlayResult Complete(PlaySession session)
        {
            PlayResult result = progression.Complete(session);
            SaveIfChanged(true);
            return result;
        }

        private bool SaveIfChanged(bool changed)
        {
            if (changed)
            {
                HasUnsavedChanges = true;
                // Retain the result in memory if the disk is unavailable. UI can offer Save() retry.
                try { Save(); }
                catch (IOException e) { Debug.LogError("Rhythm save failed; retry Save(): " + e.Message); }
                catch (UnauthorizedAccessException e) { Debug.LogError("Rhythm save access denied; retry Save(): " + e.Message); }
            }
            return changed;
        }

        public void Save()
        {
            store.Save(Player, Config);
            HasUnsavedChanges = false;
        }
    }
}
