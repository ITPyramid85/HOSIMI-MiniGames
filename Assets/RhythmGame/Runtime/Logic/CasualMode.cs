namespace RhythmGame
{
    // Runtime policy only. Existing config fields, enum values and saves stay compatible.
    public static class CasualMode
    {
        public const string ChartId = "training_casual_01";
        public static int Stage(double time) { return time < 20 ? 0 : time < 40 ? 1 : 2; }
        public static double GoodWindowMs(double time) { return Stage(time) == 0 ? 900 : Stage(time) == 1 ? 700 : 500; }
        public static double PerfectWindowMs(double time) { return GoodWindowMs(time) / 2; }
    }
}
