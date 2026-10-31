using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using VMUnityAutomation.Runtime;

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

        [TestCase(true)]
        [TestCase(false)]
        public void ExposurePreservesNativeParameterIdentityAndLifecycle(bool groupVolume)
        {
            ownsMixer = true;
            new VmAudioMixerCreateTool().Execute(new VmAudioMixerCreateRequest { AssetPath = mixerPath });
            var assets = AssetDatabase.LoadAllAssetsAtPath(mixerPath);
            var group = assets.Single(asset => asset.GetType().Name == "AudioMixerGroupController");
            var controller = assets.Single(asset => asset.GetType().Name == "AudioMixerController");
            if (!groupVolume)
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(group, out string _, out long groupID);
                Transaction(new Dictionary<string, object> { { "action", "add-effect" },
                    { "groupLocalId", groupID.ToString() }, { "effectName", "Lowpass" } });
                assets = AssetDatabase.LoadAllAssetsAtPath(mixerPath);
            }
            var effect = assets.Single(asset => asset.GetType().Name == "AudioMixerEffectController" &&
                asset.GetType().GetProperty("effectName").GetValue(asset).ToString() == (groupVolume ? "Attenuation" : "Lowpass"));
            var info = (Dictionary<string, object>)VmAutomationAudioMixerCommands.Info(new Dictionary<string, object> { { "assetPath", mixerPath } });
            var effects = (List<Dictionary<string, object>>)info["effects"];
            var definition = (List<Dictionary<string, object>>)effects.Single(value => value["effectName"].ToString() == (groupVolume ? "Attenuation" : "Lowpass"))["parameters"];
            string parameter = groupVolume ? "Volume" : definition[0]["name"].ToString();
            float requested = groupVolume ? -12 : Convert.ToSingle(definition[0]["minimum"]) * .75f + Convert.ToSingle(definition[0]["maximum"]) * .25f;
            float overridden = groupVolume ? -6 : Convert.ToSingle(definition[0]["minimum"]) * .5f + Convert.ToSingle(definition[0]["maximum"]) * .5f;
            string expectedGuid = groupVolume ? group.GetType().GetMethod("GetGUIDForVolume").Invoke(group, null).ToString() :
                effect.GetType().GetMethod("GetGUIDForParameter").Invoke(effect, new object[] { parameter }).ToString();
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(effect, out string _, out long effectID);
            Transaction(new Dictionary<string, object> { { "action", "expose-effect-parameter" },
                { "effectLocalId", effectID.ToString() }, { "parameter", parameter }, { "exposedName", "FixtureGain" } });
            var serialized = new SerializedObject(controller);
            var exposed = serialized.FindProperty("m_ExposedParameters");
            Assert.That(exposed.arraySize, Is.EqualTo(1));
            object native = ((Array)controller.GetType().GetProperty("exposedParameters").GetValue(controller)).GetValue(0);
            Assert.That(native.GetType().GetField("guid").GetValue(native).ToString(), Is.EqualTo(expectedGuid));
            Transaction(new Dictionary<string, object> { { "action", "set-snapshot-parameter" },
                { "effectLocalId", effectID.ToString() }, { "parameter", parameter }, { "value", requested } });
            var mixer = (UnityEngine.Audio.AudioMixer)controller;
            var snapshot = assets.Single(asset => asset.GetType().Name == "AudioMixerSnapshotController");
            float snapshotValue = Convert.ToSingle(groupVolume ?
                group.GetType().GetMethod("GetValueForVolume").Invoke(group, new object[] { controller, snapshot }) :
                effect.GetType().GetMethod("GetValueForParameter").Invoke(effect, new object[] { controller, snapshot, parameter }));
            Assert.That(snapshotValue, Is.EqualTo(requested).Within(.001f));
            Transaction(new Dictionary<string, object> { { "action", "set-exposed-parameter" },
                { "parameter", "FixtureGain" }, { "value", overridden } });
            Assert.That(mixer.GetFloat("FixtureGain", out float runtimeValue), Is.True);
            Assert.That(runtimeValue, Is.EqualTo(overridden).Within(.001f));
            Transaction(new Dictionary<string, object> { { "action", "clear-exposed-parameter" }, { "parameter", "FixtureGain" } });
            Transaction(new Dictionary<string, object> { { "action", "unexpose-parameter" }, { "exposedName", "FixtureGain" } });
            Assert.That(mixer.GetFloat("FixtureGain", out _), Is.False);
            Assert.That(File.ReadAllText(mixerPath), Does.Not.Contain("FixtureGain"));
        }
        private void Transaction(Dictionary<string, object> operation)
        {
            object response = VmAutomationAudioMixerCommands.Transaction(new Dictionary<string, object> {
                { "assetPath", mixerPath }, { "operations", new List<object> { operation } } });
            var result = (Dictionary<string, object>)response;
            Assert.That(result["success"], Is.EqualTo(true), Newtonsoft.Json.JsonConvert.SerializeObject(response));
        }

        [Test]
        public void AttenuationRejectsUnusableWetMixWithoutPublishingIt()
        {
            ownsMixer = true;
            new VmAudioMixerCreateTool().Execute(new VmAudioMixerCreateRequest { AssetPath = mixerPath });
            var effect = AssetDatabase.LoadAllAssetsAtPath(mixerPath).Single(asset => asset.GetType().Name == "AudioMixerEffectController");
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(effect, out string _, out long effectID);
            var result = (Dictionary<string, object>)VmAutomationAudioMixerCommands.Transaction(new Dictionary<string, object> {
                { "assetPath", mixerPath }, { "operations", new List<object> { new Dictionary<string, object> {
                    { "action", "expose-effect-parameter" }, { "effectLocalId", effectID.ToString() },
                    { "parameter", "MixLevel" }, { "exposedName", "UnusableGain" } } } } });
            Assert.That(result["success"], Is.EqualTo(false));
            Assert.That(result["errorCode"], Is.EqualTo("audio_mixer_transaction_invalid"));
            Assert.That(File.ReadAllText(mixerPath), Does.Not.Contain("UnusableGain"));
        }

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
