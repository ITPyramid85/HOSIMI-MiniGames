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
        public AccountInfo Account { get { return Player.account; } }
        // Swap for the server backend once it exists; the default keeps guests on this device.
        public IAccountBackend Accounts { get; set; }
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
            Accounts = new LocalAccountBackend();
        }

        // Signs in (or back in) and saves the account with the rest of the player data.
        public void SignIn(AccountProvider provider, Action<AccountResult> done)
        {
            Accounts.SignIn(provider, Account, result =>
            {
                if (result.Ok)
                {
                    Player.account = result.Account;
                    SaveIfChanged(true);
                    Accounts.Upload(Player, uploaded =>
                    {
                        if (uploaded) { Account.lastSyncedUtc = DateTime.UtcNow.ToString("o"); SaveIfChanged(true); }
                    });
                }
                if (done != null) done(result);
            });
        }

        // Back to the login screen; progress stays in the save for the next sign-in.
        public void SignOut()
        {
            if (!Account.IsSignedIn) return;
            Account.provider = AccountProvider.None;
            SaveIfChanged(true);
        }

        public bool Purchase(string id) { return SaveIfChanged(progression.Purchase(id)); }
        public bool Upgrade(string id) { return SaveIfChanged(progression.Upgrade(id)); }
        public bool Equip(string id) { return SaveIfChanged(progression.Equip(id)); }
        public bool AddDrumsticks(long amount) { return SaveIfChanged(progression.AddDrumsticks(amount)); }
        public MiniGameResult RecordMiniGame(string gameId, double seconds, int stage, bool cleared, bool earnsDrumsticks = true, long? earned = null)
        {
            MiniGameResult result = progression.RecordMiniGame(gameId, seconds, stage, cleared, earnsDrumsticks, earned);
            SaveIfChanged(true);
            return result;
        }

        public bool DiscoverFish(string id) { return SaveIfChanged(Player.DiscoverFish(id)); }
        public bool DiscoverMochi(string id) { return SaveIfChanged(Player.DiscoverMochi(id)); }

        public void RememberEchoBeats(int beats)
        {
            MiniGameRecord record = Player.EnsureMiniGame("echo");
            if (beats <= record.bestBeats) return;
            record.bestBeats = beats;
            SaveIfChanged(true);
        }

        public void RememberEchoStage(int stage)
        {
            MiniGameRecord record = Player.EnsureMiniGame("echo");
            stage = Math.Max(0, Math.Min(3, stage));
            if (stage <= record.echoStage) return;
            record.echoStage = stage;
            SaveIfChanged(true);
        }

        public int RememberRaceWin()
        {
            MiniGameRecord record = Player.EnsureMiniGame("race");
            record.raceWins++;
            SaveIfChanged(true);
            return record.raceWins;
        }

        public void RememberWallDistance(int meters)
        {
            if (meters < 0) return;
            MiniGameRecord record = Player.EnsureMiniGame("wall");
            if (meters <= record.wallMeters) return;
            record.wallMeters = meters;
            SaveIfChanged(true);
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
        public bool SetPadOffset(bool bluetoothAudio, double ms)
        {
            PlayerSettings.ValidatePadOffset(ms);
            ms = Math.Round(ms);
            if (Settings.PadOffsetMs(bluetoothAudio) == ms) return false;
            if (bluetoothAudio) Settings.padOffsetBluetoothAudioMs = ms; else Settings.padOffsetMs = ms;
            return SaveIfChanged(true);
        }
        public PlaySession BeginPlay(string chartId) { return progression.BeginPlay(chartId); }
        public void Abort(PlaySession session) { progression.Abort(session); }

        public PlayResult Complete(PlaySession session, bool earnsCoins = true)
        {
            PlayResult result = progression.Complete(session, earnsCoins);
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
