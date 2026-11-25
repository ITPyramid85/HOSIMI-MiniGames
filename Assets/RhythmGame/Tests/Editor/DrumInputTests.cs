using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace RhythmGame.Tests
{
    public sealed class DrumInputTests
    {
        private GameObject host;
        private DrumInput drum;
        private string directory;

        [SetUp]
        public void SetUp()
        {
            host = new GameObject("DrumInputTest");
            drum = host.AddComponent<DrumInput>();
            directory = Path.Combine(Path.GetTempPath(), "RhythmDrumTests-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(host);
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [Test]
        public void G3003NotesMapToTheManualsPadNumbers()
        {
            // Manual table: PAD1 crash 49 ... PAD8 tom3 43, pedals 44 / 36.
            int[] notes = { 49, 51, 46, 47, 45, 38, 37, 43, 44, 36 };
            for (int input = 0; input < notes.Length; input++)
            {
                Assert.AreEqual(input, DrumKit.FromMidiNote(notes[input]), "note " + notes[input]);
                Assert.AreEqual(notes[input], DrumKit.Notes[input]);
            }
            Assert.AreEqual(2, DrumKit.FromMidiNote(42), "Closed hi-hat is the same pad.");
            Assert.AreEqual(-1, DrumKit.FromMidiNote(60));
            Assert.AreEqual(7, DrumKit.FromMidiNote(43), "PAD8 tom 3 is the mochi game's pad (index 7).");
            Assert.IsTrue(DrumKit.IsPedal(DrumKit.HiHatPedal) && DrumKit.IsPedal(DrumKit.KickPedal) && !DrumKit.IsPedal(7));
            for (int input = 0; input < DrumKit.InputCount; input++)
                Assert.IsNotEmpty(Strings.Get(DrumKit.NameKey(input), GameLanguage.Korean));
        }

        [Test]
        public void HitsAreQueuedAndHandedOutInOrderOnDispatch()
        {
            var hits = new List<DrumHit>();
            drum.Hit += hits.Add;
            drum.Inject(43, 96);
            drum.Inject(42, 30);
            drum.Inject(36, 5);      // pedal: no velocity on the drum, reported as full
            drum.Inject(60, 100);    // not a G3003 note
            drum.Inject(38, 0);      // note-on with velocity 0 is a release
            Assert.AreEqual(0, hits.Count, "Nothing is delivered off the main thread's dispatch.");
            drum.Dispatch();
            Assert.AreEqual(3, hits.Count);
            Assert.AreEqual(7, hits[0].Input); Assert.AreEqual(96, hits[0].Velocity); Assert.AreEqual(43, hits[0].Note);
            Assert.AreEqual(2, hits[1].Input); Assert.AreEqual(30, hits[1].Velocity);
            Assert.AreEqual(DrumKit.KickPedal, hits[2].Input); Assert.AreEqual(DrumKit.PedalVelocity, hits[2].Velocity);
            drum.Dispatch();
            Assert.AreEqual(3, hits.Count, "Each hit once.");
        }

        [Test]
        public void ConnectionChangesAreReportedOnce()
        {
            int changes = 0;
            drum.StatusChanged += () => changes++;
            Assert.IsFalse(drum.IsConnected);
            drum.SetStatus(DrumConnection.Bluetooth, "BLE_MIDI");
            drum.Dispatch();
            drum.Dispatch();
            Assert.AreEqual(1, changes);
            Assert.IsTrue(drum.IsConnected);
            Assert.AreEqual(DrumConnection.Bluetooth, drum.Connection);
            Assert.AreEqual("BLE_MIDI", drum.DeviceName);
            drum.SetStatus(DrumConnection.Bluetooth, "BLE_MIDI");
            drum.Dispatch();
            Assert.AreEqual(1, changes, "Same state again is not a change.");
            drum.SetStatus(DrumConnection.None, "", scanning: true);
            drum.Dispatch();
            Assert.AreEqual(2, changes);
            Assert.IsTrue(drum.Scanning);
            Assert.IsFalse(drum.Supported, "No MIDI source in the editor.");
        }

        [Test]
        public void PadOffsetIsStoredPerAudioRouteAndSaved()
        {
            string path = Path.Combine(directory, "player.json");
            RhythmGameData data = RhythmGameData.Open(path);
            Assert.AreEqual(0, data.Settings.padOffsetMs);
            Assert.IsTrue(data.SetPadOffset(false, 41.6));
            Assert.IsTrue(data.SetPadOffset(true, 212));
            Assert.IsFalse(data.SetPadOffset(true, 212));
            RhythmGameData reopened = RhythmGameData.Open(path);
            Assert.AreEqual(42, reopened.Settings.PadOffsetMs(false), "Rounded to whole ms.");
            Assert.AreEqual(212, reopened.Settings.PadOffsetMs(true));
            Assert.Throws<ArgumentOutOfRangeException>(() => data.SetPadOffset(false, 5000));
            Assert.Throws<ArgumentOutOfRangeException>(() => data.SetPadOffset(false, double.NaN));
        }
    }
}
