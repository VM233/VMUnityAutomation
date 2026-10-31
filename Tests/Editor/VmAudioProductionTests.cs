using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor.Tests
{
    public sealed class VmAudioProductionTests
    {
        private readonly string mixerPath = "Assets/VM Audio Production " + Guid.NewGuid().ToString("N") + ".mixer";
        private bool ownsMixer;
        [TearDown]
        public void RetireAssets() { if (ownsMixer) AssetDatabase.DeleteAsset(mixerPath); }

        [Test]
        public void NativeMixerIsSavedAndRejectsOverwrite()
        {
            Assert.That(File.Exists(mixerPath), Is.False, "The fixture requires its own new asset path.");
            var tool = new VmAudioMixerCreateTool();
            ownsMixer = true;
            var created = tool.Execute(new VmAudioMixerCreateRequest { AssetPath = mixerPath });
            Assert.That(created.SavedAndReadBack, Is.True);
            Assert.That(created.MasterGroup, Is.EqualTo("Master"));
            Assert.That(created.GroupCount, Is.EqualTo(1));
            string guid = AssetDatabase.AssetPathToGUID(mixerPath);
            Assert.Throws<VmProjectToolException>(() => tool.Execute(new VmAudioMixerCreateRequest { AssetPath = mixerPath }));
            Assert.That(AssetDatabase.AssetPathToGUID(mixerPath), Is.EqualTo(guid));
        }
        [TestCase("Assets/../Outside.mixer")]
        [TestCase("Assets/Invalid.asset")]
        public void MixerRejectsInvalidProductPath(string path) =>
            Assert.Throws<VmProjectToolException>(() => new VmAudioMixerCreateTool().Execute(new VmAudioMixerCreateRequest { AssetPath = path }));

        [Test]
        public void PassiveFilterPreservesChannelsSamplesAndInput()
        {
            var node = new GameObject("Passive Audio Filter Fixture");
            try
            {
                var tap = node.AddComponent<VmAudioRecordingTap>();
                tap.Begin(4, 2);
                var input = new[] { .25f, -.5f, .75f, 0f, .1f, .2f };
                var original = (float[])input.Clone();
                typeof(VmAudioRecordingTap).GetMethod("OnAudioFilterRead", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(tap, new object[] { input, 2 });
                CollectionAssert.AreEqual(original, input);
                Assert.That(tap.Complete, Is.True);
                var pcm = tap.Retire(out int count, out string error);
                Assert.That(error, Is.Null); Assert.That(count, Is.EqualTo(4));
                CollectionAssert.AreEqual(new[] { .25f, -.5f, .75f, 0f }, pcm);
            }
            finally { UnityEngine.Object.DestroyImmediate(node); }
        }
        [Test]
        public void FloatWavePreservesExactPcmAndRejectsOverwrite()
        {
            string path = Path.Combine(Path.GetTempPath(), "vm-audio-" + Guid.NewGuid().ToString("N") + ".wav");
            try
            {
                var samples = new[] { .25f, -.5f, .75f, 0f };
                VmAudioRecording.WriteWave(path, samples, 4, 48000, 2);
                using (var stream = File.OpenRead(path))
                using (var reader = new BinaryReader(stream))
                {
                    Assert.That(new string(reader.ReadChars(4)), Is.EqualTo("RIFF"));
                    Assert.That(reader.ReadInt32(), Is.EqualTo(52));
                    stream.Position = 20;
                    Assert.That(reader.ReadInt16(), Is.EqualTo(3));
                    Assert.That(reader.ReadInt16(), Is.EqualTo(2));
                    Assert.That(reader.ReadInt32(), Is.EqualTo(48000));
                    stream.Position = 40; Assert.That(reader.ReadInt32(), Is.EqualTo(16));
                    foreach (float sample in samples) Assert.That(reader.ReadSingle(), Is.EqualTo(sample));
                }
                Assert.Throws<IOException>(() => VmAudioRecording.WriteWave(path, samples, 4, 48000, 2));
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }
    }
}
