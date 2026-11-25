using System;

namespace RhythmGame
{
    // One difficulty stage. Charts read what they need; judging reads the windows.
    public sealed class Stage
    {
        public int Number;            // 1..10
        public double StartSeconds;   // elapsed play time at which it begins
        public double PerfectMs, GoodMs;
    }

    // Client reply v5 §1: no time limit. Difficulty climbs through stages 1..10 with play time:
    // gentle for the first 2-3 minutes, rising from 3-4, stage 7 around 5 minutes, and clearing
    // stage 10 (surviving to ClearSeconds) is the very hard goal.
    public static class StageTable
    {
        public const int Count = 10;
        public const double ClearSeconds = 480;
        private static readonly double[] Starts = { 0, 45, 85, 125, 170, 220, 300, 345, 385, 420 };
        private static readonly double[] Perfect = { 80, 75, 70, 65, 60, 55, 50, 45, 42, 40 };
        private static readonly double[] Good = { 170, 160, 150, 140, 130, 120, 110, 100, 95, 90 };

        public static Stage Get(int number)
        {
            if (number < 1 || number > Count) throw new ArgumentOutOfRangeException("number");
            int i = number - 1;
            return new Stage { Number = number, StartSeconds = Starts[i], PerfectMs = Perfect[i], GoodMs = Good[i] };
        }

        public static Stage At(double seconds)
        {
            int number = 1;
            while (number < Count && seconds >= Starts[number]) number++;
            return Get(number);
        }

        // 0..1 through the current stage (the last stage runs to ClearSeconds); drives the HUD bar.
        public static double ProgressIn(double seconds)
        {
            Stage stage = At(seconds);
            double end = stage.Number < Count ? Starts[stage.Number] : ClearSeconds;
            return Math.Max(0, Math.Min(1, (seconds - stage.StartSeconds) / (end - stage.StartSeconds)));
        }
    }

    // Client reply v5 §1/§6: no medals; drumsticks grow with time played and stage reached, plus a
    // clear bonus for stage 10. Rates are the developer's proposal for the client to confirm.
    public static class DrumstickReward
    {
        public const double SecondsPerStick = 4;   // 15 per minute played
        public const int ClearBonus = 100;
        // Paid on reaching stages 2..10.
        private static readonly int[] StageBonus = { 0, 2, 3, 4, 6, 8, 12, 16, 20, 25 };

        public static long For(double seconds, int stageReached, bool cleared)
        {
            if (seconds < 0 || stageReached < 1 || stageReached > StageTable.Count) throw new ArgumentOutOfRangeException();
            long total = (long)Math.Floor(Math.Min(seconds, StageTable.ClearSeconds) / SecondsPerStick);
            for (int i = 1; i < stageReached; i++) total += StageBonus[i];
            return total + (cleared ? ClearBonus : 0);
        }
    }

    // What a finished run paid and whether it set a record.
    public sealed class MiniGameResult
    {
        public double Seconds;
        public int Stage;
        public bool Cleared;
        public long Drumsticks;
        public bool NewBestStage, NewBestTime;
    }

    // Score, combo and hearts for one endless mini-game run. A run ends when the hearts are gone.
    public sealed class MiniGameScore
    {
        public const int PerfectPoints = 100, GoodPoints = 50, BonusFactor = 3, ForbiddenPenalty = 150;
        public const int MaxLives = 5, HalvesPerHeart = 2;
        public const int MaxHalves = MaxLives * HalvesPerHeart;
        public int Score { get; private set; }
        public int Combo { get; private set; }
        public int MaxCombo { get; private set; }
        public int Perfect { get; private set; }
        public int Good { get; private set; }
        public int Miss { get; private set; }          // missed cues, forbidden hits and empty hits
        public int Halves { get; private set; }        // 0..10. Two halves fill one heart.
        // Hearts that still show any fill, so a half heart still counts until it is empty.
        public int Lives { get { return (Halves + HalvesPerHeart - 1) / HalvesPerHeart; } }
        public bool IsOver { get { return Halves == 0; } }

        public MiniGameScore() { Halves = MaxHalves; }

        // Missing a cue or striking a forbidden one costs half a heart. An empty strike only breaks the combo.
        // Hits never give a heart back.
        public void Add(CueResult result)
        {
            switch (result.Outcome)
            {
                case CueOutcome.Hit:
                    int points = result.Grade == HitGrade.Perfect ? PerfectPoints : GoodPoints;
                    if (result.Cue.Kind == CueKind.Bonus) points *= BonusFactor;
                    Score += points;
                    if (result.Grade == HitGrade.Perfect) Perfect++; else Good++;
                    Combo++;
                    MaxCombo = Math.Max(MaxCombo, Combo);
                    break;
                case CueOutcome.Missed:
                    Miss++; Combo = 0; LoseHalf();
                    break;
                case CueOutcome.ForbiddenHit:
                    Score = Math.Max(0, Score - ForbiddenPenalty); Miss++; Combo = 0; LoseHalf();
                    break;
                case CueOutcome.EmptyHit:
                    Miss++; Combo = 0;
                    break;
                case CueOutcome.ForbiddenAvoided:
                    break;
            }
        }

        // Also for a penalty that is not a judged cue, such as falling through empty ice.
        public void LoseHalf() { if (Halves > 0) Halves--; }

        // Ends the run outright, whatever hearts are left.
        public void Defeat() { Halves = 0; }
    }
}
