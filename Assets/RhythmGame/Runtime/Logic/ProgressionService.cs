using System;

namespace RhythmGame
{
    // All changes to a player's progression should go through this service.
    public sealed class ProgressionService
    {
        private readonly GameConfig config;
        private PlaySession active;
        public PlayerData Player { get; private set; }

        public ProgressionService(GameConfig config, PlayerData player)
        {
            if (config == null || player == null) throw new ArgumentNullException();
            config.Validate();
            player.Validate(config);
            this.config = config;
            Player = player;
        }

        private void RequireIdle()
        {
            if (active != null) throw new InvalidOperationException("Finish or abort the current song first.");
        }

        public bool Purchase(string characterId)
        {
            RequireIdle();
            CharacterConfig c = config.Character(characterId);
            if (Player.FindCharacter(characterId) != null || Player.coins < c.purchasePrice) return false;
            Player.coins -= c.purchasePrice;
            Player.characters.Add(new OwnedCharacter { characterId = characterId, level = 1 });
            return true;
        }

        public bool Upgrade(string characterId)
        {
            RequireIdle();
            CharacterConfig c = config.Character(characterId);
            OwnedCharacter owned = Player.FindCharacter(characterId);
            if (owned == null || owned.level >= c.maxLevel) return false;
            int price = config.economy.UpgradeCost(owned.level);
            if (Player.coins < price) return false;
            Player.coins -= price;
            owned.level++;
            return true;
        }

        public bool Equip(string characterId)
        {
            RequireIdle();
            config.Character(characterId);
            if (Player.FindCharacter(characterId) == null) return false;
            Player.equippedCharacterId = characterId;
            return true;
        }

        // Presentation settings may be changed during a song without affecting scoring.
        public bool SetLanguage(GameLanguage language)
        {
            PlayerSettings.ValidateLanguage(language);
            if (Player.settings.language == language) return false;
            Player.settings.language = language;
            return true;
        }

        public bool SetVolumes(float master, float music, float soundEffects)
        {
            // Validate the entire update before mutating any channel.
            PlayerSettings.ValidateVolume(master);
            PlayerSettings.ValidateVolume(music);
            PlayerSettings.ValidateVolume(soundEffects);
            PlayerSettings settings = Player.settings;
            if (settings.masterVolume == master && settings.musicVolume == music && settings.soundEffectsVolume == soundEffects)
                return false;
            settings.masterVolume = master;
            settings.musicVolume = music;
            settings.soundEffectsVolume = soundEffects;
            return true;
        }

        public void SetInputOffset(double milliseconds)
        {
            RequireIdle();
            if (!GameConfig.IsFinite(milliseconds) || Math.Abs(milliseconds) > config.rules.maxCalibrationMs)
                throw new ArgumentOutOfRangeException("milliseconds");
            Player.inputOffsetMs = milliseconds;
        }

        // Mini-games pay drumsticks straight into the one shared balance (plan 2-1).
        public bool AddDrumsticks(long amount)
        {
            RequireIdle();
            if (amount < 0) throw new ArgumentOutOfRangeException("amount");
            if (amount == 0) return false;
            Player.coins = checked(Player.coins + amount);
            return true;
        }

        // Settles one endless mini-game run: pays its drumsticks and keeps the best stage / longest time.
        // earnsDrumsticks: false for touch play (client reply v6 §2-3: only play on the drum pays).
        // earned, when set, is the amount for accurate play. A miss does not add to it.
        public MiniGameResult RecordMiniGame(string gameId, double seconds, int stageReached, bool cleared, bool earnsDrumsticks = true, long? earned = null)
        {
            RequireIdle();
            if (string.IsNullOrEmpty(gameId) || !GameConfig.IsFinite(seconds)) throw new ArgumentException("Invalid mini-game result.");
            if (earned.HasValue && earned.Value < 0) throw new ArgumentOutOfRangeException("earned");
            long drumsticks = !earnsDrumsticks ? 0 : earned ?? DrumstickReward.For(seconds, stageReached, cleared);
            MiniGameRecord record = Player.FindMiniGame(gameId);
            if (record == null) Player.miniGames.Add(record = new MiniGameRecord { gameId = gameId, bestStage = 1 });
            var result = new MiniGameResult { Seconds = seconds, Stage = stageReached, Cleared = cleared, Drumsticks = drumsticks,
                NewBestStage = stageReached > record.bestStage || (record.plays == 0), NewBestTime = seconds > record.bestSeconds };
            record.plays++;
            record.bestStage = Math.Max(record.bestStage, stageReached);
            record.bestSeconds = Math.Max(record.bestSeconds, seconds);
            record.totalDrumsticks = checked(record.totalDrumsticks + drumsticks);
            Player.coins = checked(Player.coins + drumsticks);
            return result;
        }

        public bool SetBluetoothCompensation(bool enabled, double milliseconds)
        {
            PlayerSettings.ValidateBluetoothDelay(milliseconds);
            if (Player.settings.bluetoothCompensationEnabled == enabled && Player.settings.bluetoothDelayMs == milliseconds) return false;
            Player.settings.bluetoothCompensationEnabled = enabled;
            Player.settings.bluetoothDelayMs = milliseconds;
            return true;
        }

        public bool IsChartUnlocked(string chartId)
        {
            ChartConfig chart = config.Chart(chartId);
            if (string.IsNullOrEmpty(chart.requiredClearChartId)) return true;
            ChartRecord prerequisite = Player.FindRecord(chart.requiredClearChartId);
            return prerequisite != null && prerequisite.cleared;
        }

        public PlaySession BeginPlay(string chartId)
        {
            RequireIdle();
            if (!IsChartUnlocked(chartId)) throw new InvalidOperationException("Chart is locked.");
            Player.Validate(config);
            active = new PlaySession(config, config.Chart(chartId), Player.FindCharacter(Player.equippedCharacterId), Player.inputOffsetMs);
            return active;
        }

        public void Abort(PlaySession session)
        {
            RequireActive(session);
            session.closed = true;
            active = null;
        }

        private void RequireActive(PlaySession session)
        {
            if (session == null || session != active || session.closed)
                throw new InvalidOperationException("Session is not active or was already settled.");
        }

        public PlayResult Complete(PlaySession session, bool earnsCoins = true)
        {
            RequireActive(session);
            PlayResult result = session.BuildResult();
            ChartConfig chart = config.Chart(result.ChartId);
            ChartRecord record = Player.FindRecord(result.ChartId);
            EconomyConfig e = config.economy;
            if (result.Cleared && earnsCoins)
            {
                double ratio = e.minimumClearRewardRatio + (1 - e.minimumClearRewardRatio) * result.Accuracy;
                result.BaseCoins = (long)Math.Floor((e.clearBaseCoins + (double)e.coinsPerDifficultyLevel * chart.difficultyLevel) * ratio);
                if (record == null || !record.cleared) result.FirstClearCoins = e.firstClearCoins;
                if (result.FullCombo && (record == null || !record.fullCombo)) result.FirstFullComboCoins = e.firstFullComboCoins;
            }
            long nextCoins = checked(Player.coins + result.TotalCoins);
            int nextPlayCount = checked((record == null ? 0 : record.playCount) + 1);
            if (record == null)
            {
                record = new ChartRecord { chartId = result.ChartId };
                Player.records.Add(record);
            }
            record.bestScore = Math.Max(record.bestScore, result.Score);
            record.bestAccuracy = Math.Max(record.bestAccuracy, result.Accuracy);
            record.maxCombo = Math.Max(record.maxCombo, result.MaxCombo);
            record.playCount = nextPlayCount;
            record.cleared |= result.Cleared;
            record.fullCombo |= result.FullCombo;
            record.allPerfect |= result.AllPerfect;
            Player.coins = nextCoins;
            session.closed = true;
            active = null;
            return result;
        }
    }
}
