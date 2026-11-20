using System;
using System.Collections.Generic;

namespace RhythmGame
{
    // Game 10. Thin walls are one S, medium walls one D, thick walls two or three C beats, a chest is Q.
    // The water creeps one cell forward every four beats on its own. Each broken wall wins a cell back,
    // a miss gives one away, and a chest broken with the crash buys extra room.
    public static class WallChart
    {
        public const double Bpm = 100;
        public const double BeatSeconds = 60.0 / Bpm;
        public const double CruiseGap = BeatSeconds * 2;
        public const double FirstCue = 2;
        // Slow enough that a three-beat thick wall still breaks even, so only misses lose ground.
        public const double CreepSeconds = BeatSeconds * 4;
        public const int StartGap = 5;
        public const int TreasureAir = 3;
        public const int ThinPad = 3, MidPad = 4, ThickPad = 7, ChestPad = 0;
        // The on-screen DrumQ is hidden, but the crash pad (Q) still breaks chests.
        public static readonly int[] Pads = { ThinPad, MidPad, ThickPad, ChestPad };

        // Water creeps that have happened by this song time.
        public static int CreepsBy(double songTime)
        {
            if (songTime < FirstCue + CreepSeconds) return 0;
            return (int)Math.Floor((songTime - FirstCue) / CreepSeconds + 1e-9);
        }

        // How many of the other games need a recorded time before Wall Breaker opens.
        public static int TimesToOpen
        {
            get
            {
                int count = 0;
                MiniGamesMenu.Entry[] games = MiniGamesMenu.Games;
                for (int i = 0; i < games.Length; i++) if (games[i].Id != "wall") count++;
                return count;
            }
        }

        // Games that already have a longest time. A stage with no time does not count, and Wall Breaker does not count itself.
        public static int RecordedTimes(PlayerData player)
        {
            if (player == null || player.miniGames == null) return 0;
            int count = 0;
            MiniGamesMenu.Entry[] games = MiniGamesMenu.Games;
            for (int i = 0; i < games.Length; i++)
            {
                if (games[i].Id == "wall") continue;
                MiniGameRecord record = player.FindMiniGame(games[i].Id);
                if (record != null && record.bestSeconds > 0) count++;
            }
            return count;
        }

        public static bool Unlocked(PlayerData player) { return RecordedTimes(player) >= TimesToOpen; }

        public static List<BeatCue> Generate(int seed)
        {
            var random = new Random(seed);
            var cues = new List<BeatCue>();
            double time = FirstCue;
            int serial = 0;
            while (time < StageTable.ClearSeconds - BeatSeconds)
            {
                int roll = random.Next(10);
                int pad = MidPad, need = 1;
                CueKind kind = CueKind.Normal;
                if (roll == 0 && serial >= 2) { pad = ChestPad; kind = CueKind.Bonus; }
                else if (roll < 4) pad = ThinPad;
                else if (roll < 8) pad = MidPad;
                else { pad = ThickPad; need = 2 + random.Next(2); }
                if (time + (need - 1) * BeatSeconds >= StageTable.ClearSeconds) break;
                serial++;
                int variant = serial * 10 + need;
                for (int hit = 0; hit < need; hit++)
                {
                    double at = time + hit * BeatSeconds;
                    Stage stage = StageTable.At(at);
                    cues.Add(new BeatCue
                    {
                        Index = cues.Count, Time = at, Pad = pad, Kind = kind, Variant = variant,
                        PerfectMs = stage.PerfectMs, GoodMs = stage.GoodMs
                    });
                }
                time += (need - 1) * BeatSeconds + CruiseGap;
            }
            return cues;
        }

        // Meters advance only when a wall breaks. Gap is how many cells the water is behind the runner.
        public sealed class Flood
        {
            public int Meters { get; private set; }
            public int Gap { get; private set; }
            public Flood() { Gap = StartGap; }
            public bool Submerged { get { return Gap <= 0; } }

            public void Creep()
            {
                if (!Submerged) Gap--;
            }

            public void Resolve(bool broke, bool treasure)
            {
                if (Submerged) return;
                if (!broke) { Gap--; return; }
                Meters++;
                int room = treasure ? StartGap + TreasureAir : StartGap;
                if (Gap < room) Gap = Math.Min(room, Gap + (treasure ? 2 : 1));
            }
        }
    }
}
