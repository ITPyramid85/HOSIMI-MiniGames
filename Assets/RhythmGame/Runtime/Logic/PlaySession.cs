using System;
using System.Collections.Generic;

namespace RhythmGame
{
    public sealed class HitResult
    {
        public int NoteIndex { get; internal set; }
        public Judgement Judgement { get; internal set; }
        public string ColorHtml { get; internal set; }
        public int Combo { get; internal set; }
        public int Health { get; internal set; }
        public int HealthDelta { get; internal set; }
        public bool HasFailed { get; internal set; }
        public DialogueCue Dialogue { get; internal set; }
        public double RawScore { get; internal set; }
        public long TotalScore { get; internal set; }
    }

    public sealed class PlayResult
    {
        public string ChartId { get; internal set; }
        public string CharacterId { get; internal set; }
        public int CharacterLevel { get; internal set; }
        public long Score { get; internal set; }
        public double Accuracy { get; internal set; }
        public string Grade { get; internal set; }
        public int Perfect { get; internal set; }
        public int Great { get; internal set; }
        public int Good { get; internal set; }
        public int Miss { get; internal set; }
        public int MaxCombo { get; internal set; }
        public bool Cleared { get; internal set; }
        public bool FullCombo { get; internal set; }
        public bool AllPerfect { get; internal set; }
        public bool FailedByHealth { get; internal set; }
        public int RemainingHealth { get; internal set; }
        public int UnplayedNotes { get; internal set; }
        public long BaseCoins { get; internal set; }
        public long FirstClearCoins { get; internal set; }
        public long FirstFullComboCoins { get; internal set; }
        public long TotalCoins { get { return BaseCoins + FirstClearCoins + FirstFullComboCoins; } }
    }

    public sealed class PlaySession
    {
        private readonly RulesConfig rules;
        private readonly CharacterConfig character;
        private readonly bool[] judged;
        private readonly int[] counts = new int[4];
        private readonly double baseScore;
        private readonly double calibrationMs;
        private readonly HashSet<int> spokenComboThresholds = new HashSet<int>();
        private int lastMissDialogueCount;
        private double rawScore;
        private double accuracySum;
        internal bool closed;
        public string ChartId { get; private set; }
        public bool IsCasualMode { get { return ChartId == CasualMode.ChartId; } }
        public string CharacterId { get { return character.id; } }
        public int CharacterLevel { get; private set; }
        public int NoteCount { get { return judged.Length; } }
        public int JudgedCount { get; private set; }
        public int Combo { get; private set; }
        public int MaxCombo { get; private set; }
        public int Health { get; private set; }
        public int MaxHealth { get { return rules.maxHealth; } }
        public bool HasFailed { get { return Health == 0; } }
        public long Score { get { return (long)Math.Round(rawScore, MidpointRounding.AwayFromZero); } }
        public bool IsComplete { get { return JudgedCount == NoteCount; } }
        public bool IsFinished { get { return IsComplete || HasFailed; } }

        internal PlaySession(GameConfig config, ChartConfig chart, OwnedCharacter owned, double offsetMs)
        {
            rules = config.rules;
            character = config.Character(owned.characterId);
            CharacterLevel = owned.level;
            baseScore = character.BaseScore(owned.level);
            ChartId = chart.id;
            if (IsCasualMode) baseScore = 100;
            judged = new bool[chart.noteCount];
            calibrationMs = offsetMs;
            Health = rules.maxHealth;
        }

        // noteIndex is the stable index in the chart. Each note can be judged only once.
        public HitResult SubmitTiming(int noteIndex, double rawOffsetMs)
        {
            return Submit(noteIndex, rules.Judge(rawOffsetMs, calibrationMs));
        }

        public HitResult SubmitMiss(int noteIndex) { return Submit(noteIndex, Judgement.Miss); }

        // Input modes choose a judgement; scoring and health always use this shared path.
        internal HitResult Submit(int noteIndex, Judgement judgement)
        {
            if (IsCasualMode && judgement == Judgement.Great) judgement = Judgement.Good;
            if (closed || HasFailed) throw new InvalidOperationException("Session is closed or health reached zero.");
            if (noteIndex < 0 || noteIndex >= NoteCount) throw new ArgumentOutOfRangeException("noteIndex");
            if (judged[noteIndex]) throw new InvalidOperationException("This note was already judged.");
            JudgementConfig row = rules.GetJudgement(judgement);
            judged[noteIndex] = true;
            JudgedCount++;
            counts[(int)judgement]++;
            int healthBefore = Health;
            if (judgement == Judgement.Miss) Health = Math.Max(0, Health - rules.missDamage);
            Combo = (IsCasualMode ? judgement != Judgement.Miss : row.keepsCombo) ? Combo + 1 : 0;
            MaxCombo = Math.Max(MaxCombo, Combo);
            double comboBonus = Math.Min(rules.maxComboBonus, (Combo / rules.comboStep) * rules.comboBonusPerStep);
            double weight = IsCasualMode && judgement == Judgement.Good ? .7 : row.scoreWeight;
            double points = baseScore * weight * (1 + comboBonus);
            rawScore += points;
            accuracySum += IsCasualMode ? weight : row.accuracyWeight;
            return new HitResult { NoteIndex = noteIndex, Judgement = judgement, ColorHtml = row.colorHtml, Combo = Combo,
                RawScore = points, TotalScore = Score, Health = Health, HealthDelta = Health - healthBefore,
                HasFailed = HasFailed, Dialogue = BuildDialogue(judgement) };
        }

        private DialogueCue BuildDialogue(Judgement judgement)
        {
            if (judgement == Judgement.Miss && (lastMissDialogueCount == 0 ||
                JudgedCount - lastMissDialogueCount >= rules.missDialogueCooldownNotes))
            {
                lastMissDialogueCount = JudgedCount;
                return new DialogueCue { Reason = DialogueReason.Miss, Combo = Combo, Line = character.MissLine };
            }
            foreach (int threshold in rules.comboDialogueThresholds)
                if (Combo == threshold && spokenComboThresholds.Add(threshold))
                    return new DialogueCue { Reason = DialogueReason.ComboMilestone, Combo = Combo, Line = character.ComboLine };
            return null;
        }

        internal PlayResult BuildResult()
        {
            if (closed || !IsFinished) throw new InvalidOperationException("Finish the chart or reach zero health before settlement.");
            // Remove floating point accumulation noise at clear/grade thresholds.
            double accuracy = Math.Max(0, Math.Min(1, Math.Round(accuracySum / NoteCount, 12)));
            bool cleared = !HasFailed && IsComplete && accuracy >= rules.clearAccuracy;
            bool fullCombo = cleared && (IsCasualMode || counts[(int)Judgement.Good] == 0) && counts[(int)Judgement.Miss] == 0;
            return new PlayResult { ChartId = ChartId, CharacterId = CharacterId, CharacterLevel = CharacterLevel,
                Score = Score, Accuracy = accuracy, Grade = rules.Grade(accuracy), MaxCombo = MaxCombo,
                Perfect = counts[0], Great = counts[1], Good = counts[2], Miss = counts[3],
                Cleared = cleared, FullCombo = fullCombo, AllPerfect = fullCombo && counts[0] == NoteCount,
                FailedByHealth = HasFailed, RemainingHealth = Health, UnplayedNotes = NoteCount - JudgedCount };
        }
    }
}
