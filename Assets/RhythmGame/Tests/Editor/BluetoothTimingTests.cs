using System;
using NUnit.Framework;
using UnityEngine;

namespace RhythmGame.Tests
{
    public sealed class BluetoothTimingTests
    {
        [Test]
        public void OldSettingsLoadDisabledAndNewFieldsRoundTripWithoutChangingCalibration()
        {
            var settings = JsonUtility.FromJson<PlayerSettings>("{\"language\":2,\"masterVolume\":0.5,\"musicVolume\":1,\"soundEffectsVolume\":1}");
            settings.Validate(); Assert.IsFalse(settings.bluetoothCompensationEnabled);
            Assert.AreEqual(0, settings.bluetoothDelayMs);
            var config = RhythmGameData.LoadDefaultConfig();
            var player = PlayerData.Create(config);
            var service = new ProgressionService(config, player);
            service.SetInputOffset(-25); service.SetBluetoothCompensation(true, 300);
            var loaded = JsonUtility.FromJson<PlayerData>(JsonUtility.ToJson(player));
            loaded.Validate(config);
            Assert.AreEqual(-25, loaded.inputOffsetMs);
            Assert.AreEqual(300, loaded.settings.BluetoothCompensationMs(true));
            Assert.AreEqual(0, loaded.settings.BluetoothCompensationMs(false));
            service.SetBluetoothCompensation(false, 300);
            Assert.AreEqual(0, player.settings.BluetoothCompensationMs(true));
            Assert.AreEqual(300, player.settings.bluetoothDelayMs);
        }

        [TestCase(-1)] [TestCase(1001)] [TestCase(double.NaN)] [TestCase(double.PositiveInfinity)]
        public void InvalidDelayCannotPartiallyChangeSavedSetting(double value)
        {
            var player = PlayerData.Create(RhythmGameData.LoadDefaultConfig());
            var service = new ProgressionService(RhythmGameData.LoadDefaultConfig(), player);
            string before = JsonUtility.ToJson(player);
            Assert.Throws<ArgumentOutOfRangeException>(() => service.SetBluetoothCompensation(true, value));
            Assert.AreEqual(before, JsonUtility.ToJson(player));
        }

        [TestCase(0, 300)] [TestCase(300, 0)] [TestCase(-25, 975)]
        public void PausedRouteSwitchPreservesVisibleReactionWindowAndMovesFutureCues(double from, double to)
        {
            var config = RhythmGameData.LoadDefaultConfig();
            var service = new ProgressionService(config, PlayerData.Create(config));
            var session = service.BeginPlay(CasualMode.ChartId);
            var chart = config.Chart(CasualMode.ChartId);
            var points = JsonUtility.FromJson<BeatmapData>(Resources.Load<TextAsset>(chart.chartResource).text).Compile(chart);
            var judge = new PadInputJudge(session, points, from);
            double shown = 2 + from / 1000;
            judge.Present(shown); judge.ReleaseAll(shown + .3);
            double delta = judge.ChangeCalibrationWhilePaused(to);
            Assert.IsTrue(judge.IsLit(5, shown + delta - .000000000001), "DSP re-anchoring roundoff must not hide the resumed cue.");
            Assert.AreEqual((to - from) / 1000, delta, .000001);
            Assert.AreEqual(4 + to / 1000, judge.NextTime(6), .000001);
            Assert.IsTrue(judge.IsLit(5, shown + .3 + delta));
            HitResult hit = null; judge.Judged += value => hit = value;
            judge.Press(5, shown + .5 + delta);
            Assert.AreEqual(Judgement.Good, hit.Judgement, "Switching must not give a fresh Perfect window.");
            Assert.AreEqual(1, session.JudgedCount); Assert.AreEqual(100, session.Health);
        }

        [Test]
        public void AddedDelayDoesNotAcceptDarkEarlyPressOrAutoScoreHeldInput()
        {
            var config = RhythmGameData.LoadDefaultConfig();
            var service = new ProgressionService(config, PlayerData.Create(config));
            var session = service.BeginPlay(CasualMode.ChartId);
            var chart = config.Chart(CasualMode.ChartId);
            var points = JsonUtility.FromJson<BeatmapData>(Resources.Load<TextAsset>(chart.chartResource).text).Compile(chart);
            var judge = new PadInputJudge(session, points, 300);
            judge.Present(2); Assert.IsFalse(judge.IsLit(5, 2));
            Assert.IsFalse(judge.Press(5, 2.1)); judge.Present(2.3);
            Assert.IsFalse(judge.Press(5, 2.4)); Assert.AreEqual(0, session.JudgedCount);
            judge.Release(5, 2.4); Assert.IsTrue(judge.Press(5, 2.4));
            Assert.AreEqual(1, session.JudgedCount);
        }
    }
}
