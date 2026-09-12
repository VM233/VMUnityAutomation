using System.Collections.Generic;
using NUnit.Framework;
using UnityEditorInternal;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmProfilerRecordingTests
    {
        [Test]
        public void EditorSamplingPublishesPreviousStateAndCanBeRestored()
        {
            bool enabled = ProfilerDriver.enabled;
            bool deep = ProfilerDriver.deepProfiling;
            bool editor = ProfilerDriver.profileEditor;
            try
            {
                var result = (Dictionary<string, object>)VmAutomationProfilerCommands.EnableProfiler(new()
                {
                    { "enabled", false }, { "profileEditor", !editor }
                });
                var previous = (Dictionary<string, object>)result["previous"];
                Assert.That(previous["enabled"], Is.EqualTo(enabled));
                Assert.That(previous["deepProfiling"], Is.EqualTo(deep));
                Assert.That(previous["profileEditor"], Is.EqualTo(editor));
                Assert.That(result["profilerEnabled"], Is.False);
                Assert.That(result["profileEditor"], Is.EqualTo(!editor));
                Assert.That(result["deepProfiling"], Is.EqualTo(deep));
                Assert.That(result["framesCleared"], Is.False);
                Assert.That(result["firstFrame"], Is.EqualTo(result["previousFirstFrame"]));
                Assert.That(result["lastFrame"], Is.EqualTo(result["previousLastFrame"]));
                VmAutomationProfilerCommands.EnableProfiler(previous);
                Assert.That(ProfilerDriver.enabled, Is.EqualTo(enabled));
                Assert.That(ProfilerDriver.profileEditor, Is.EqualTo(editor));
            }
            finally
            {
                ProfilerDriver.profileEditor = editor;
                ProfilerDriver.deepProfiling = deep;
                ProfilerDriver.enabled = enabled;
            }
        }

        [Test]
        public void RecordingContractDeclaresEditorSamplingAndPreviousState()
        {
            Assert.That(VmAutomationCatalog.TryGetTool("profiler/enable", true, out var tool), Is.True);
            var input = (Dictionary<string, object>)tool["inputSchema"];
            var inputProperties = (Dictionary<string, object>)input["properties"];
            Assert.That(inputProperties.ContainsKey("profileEditor"), Is.True);
            Assert.That(inputProperties.ContainsKey("clearFrames"), Is.True);
            var output = (Dictionary<string, object>)tool["outputSchema"];
            var outputProperties = (Dictionary<string, object>)output["properties"];
            Assert.That(outputProperties.ContainsKey("profileEditor"), Is.True);
            Assert.That(outputProperties.ContainsKey("previous"), Is.True);
            Assert.That(outputProperties.ContainsKey("framesCleared"), Is.True);
            Assert.That(outputProperties.ContainsKey("previousFirstFrame"), Is.True);
            Assert.That(outputProperties.ContainsKey("previousLastFrame"), Is.True);
        }

        [Test]
        public void ExplicitCaptureRetirementPublishesEmptyFrameHistory()
        {
            bool enabled = ProfilerDriver.enabled;
            try
            {
                var result = (Dictionary<string, object>)VmAutomationProfilerCommands.EnableProfiler(new()
                {
                    { "enabled", false }, { "clearFrames", true }
                });
                Assert.That(result["framesCleared"], Is.True);
                Assert.That(result["profilerEnabled"], Is.False);
                Assert.That(VmAutomationProfilerCommands.HasRecordedFrameData(
                    (int)result["firstFrame"], (int)result["lastFrame"]), Is.False);
            }
            finally { ProfilerDriver.enabled = enabled; }
        }
    }
}
