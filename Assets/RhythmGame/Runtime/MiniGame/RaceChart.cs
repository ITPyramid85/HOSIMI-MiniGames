using System.Collections.Generic;

namespace RhythmGame
{
    // Game 9. S and D alternate on the beat and walk the turtle up to the sprint line.
    // Past that line only Z moves it: 3 seconds of snare at double speed while the rabbit chases.
    public static class RaceChart
    {
        public const double Bpm = 100;
        public const double FirstCue = 2;
        public const double NapAt = 7;
        // The rabbit wakes on its own here if the turtle has not reached the sprint line.
        public const double WakeAt = 30;
        public const double SprintSeconds = 3;
        // The rabbit needs a little longer than the sprint, so an all-good Z roll still wins.
        public const double ChaseSeconds = 3.4;
        public const int FinishSteps = 30;
        public const int SprintLine = 20;
        public const int NapSteps = 16;
        public const int CloseSteps = 24;
        public const int LeftPad = 3;     // S, display pad 4
        public const int RightPad = 4;    // D, display pad 5
        public const int SprintPad = 5;   // Z, display pad 6

        public static double BeatSeconds { get { return 60.0 / Bpm; } }
        // The shared countdown shows GO on beat 3. The rabbit stays on the line until then.
        public static double GoAt { get { return BeatSeconds * 3; } }
        // Two beats while walking. The sprint uses one beat, so the drum is twice as fast.
        public static double CruiseGap { get { return BeatSeconds * 2; } }
        public static double SprintGap { get { return BeatSeconds; } }
        public static int SprintCues { get { return (int)System.Math.Round(SprintSeconds / SprintGap); } }

        public static int[] Pads { get { return new[] { LeftPad, RightPad, SprintPad }; } }

        // S D S D ... on race time, until the rabbit would wake by itself.
        public static List<BeatCue> Generate()
        {
            var cues = new List<BeatCue>();
            double time = FirstCue;
            bool left = true;
            while (time < WakeAt - 0.0001)
            {
                cues.Add(Make(cues.Count, time, left ? LeftPad : RightPad));
                time += CruiseGap;
                left = !left;
            }
            return cues;
        }

        // The same walk, moved so its GO falls a beat after songNow. Stage 2 starts when the turtle finishes.
        public static List<BeatCue> FromNow(double songNow)
        {
            double origin = songNow - GoAt + BeatSeconds;
            List<BeatCue> cues = Generate();
            for (int i = 0; i < cues.Count; i++) cues[i] = Make(i, cues[i].Time + origin, cues[i].Pad);
            return cues;
        }

        // Z only, one beat apart, for the 3 seconds after songNow.
        public static List<BeatCue> Sprint(double songNow)
        {
            var cues = new List<BeatCue>();
            for (int k = 1; k <= SprintCues; k++) cues.Add(Make(k - 1, songNow + SprintGap * k, SprintPad));
            return cues;
        }

        // A miss stays put. A Z hit in the sprint doubles a clean step.
        public static int Steps(HitGrade grade, bool sprint)
        {
            int step = grade == HitGrade.Perfect ? 2 : grade == HitGrade.Good ? 1 : 0;
            return sprint ? step * 2 : step;
        }

        // Ahead, then asleep on 16, then chasing to the line from the moment the sprint starts.
        // sprintAt is race time; pass double.PositiveInfinity while nobody has started it.
        public static double RabbitSteps(double time, double sprintAt)
        {
            if (time <= GoAt) return 0;
            if (time < NapAt) return NapSteps * ((time - GoAt) / (NapAt - GoAt));
            if (time < sprintAt) return NapSteps;
            double along = (time - sprintAt) / ChaseSeconds;
            if (along > 1) along = 1;
            return NapSteps + (FinishSteps - NapSteps) * along;
        }

        // 0 meadow, 1 mountain, 2 sea, 3 or more space. The shell uses the same count.
        public static int Course(int wins)
        {
            if (wins <= 0) return 0;
            if (wins >= 3) return 3;
            return wins;
        }

        public static int Shell(int wins) { return Course(wins); }

        // Close enough that the rabbit bothers to tease. A win is not a tease.
        public static bool CloseLoss(int turtleSteps)
        {
            return turtleSteps >= CloseSteps && turtleSteps < FinishSteps;
        }

        private static BeatCue Make(int index, double time, int pad)
        {
            Stage stage = StageTable.At(time);
            return new BeatCue
            {
                Index = index,
                Time = time,
                Pad = pad,
                PerfectMs = stage.PerfectMs,
                GoodMs = stage.GoodMs
            };
        }
    }
}
