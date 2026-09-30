using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
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
        public void RenderingContractsPublishNativeAndConvertedTimingUnits()
        {
            Assert.That(VmAutomationCatalog.TryGetTool("profiler/stats", true, out var stats), Is.True);
            var statsOutput = (Dictionary<string, object>)stats["outputSchema"];
            var properties = (Dictionary<string, object>)statsOutput["properties"];
            foreach (string name in new[] { "frameTime", "renderTime" })
            {
                var field = (Dictionary<string, object>)properties[name];
                Assert.That((string)field["description"], Does.Contain("seconds"));
            }
            Assert.That(VmAutomationCatalog.TryGetTool("profiler/analyze", true, out var analysis), Is.True);
            var analysisOutput = (Dictionary<string, object>)analysis["outputSchema"];
            var analysisProperties = (Dictionary<string, object>)analysisOutput["properties"];
            var rendering = (Dictionary<string, object>)analysisProperties["rendering"];
            Assert.That((string)rendering["description"], Does.Contain("frameTimeSeconds"));
            Assert.That((string)rendering["description"], Does.Contain("frameTimeMs"));
            Assert.That((string)rendering["description"], Does.Contain("estimatedFps"));
        }

        [Test]
        public void RenderingStatisticsFollowTheNativeCounterVersion()
        {
            var result = (Dictionary<string, object>)VmAutomationProfilerCommands.GetRenderingStats(new());
            Assert.That(result["drawCalls"], Is.EqualTo(UnityStats.drawCalls));
            Assert.That(result["setPassCalls"], Is.EqualTo(UnityStats.setPassCalls));
            Assert.That(result["frameTime"], Is.TypeOf<float>());
            Assert.That(result["renderTime"], Is.TypeOf<float>());
            Assert.That(result.ContainsKey("indirectDrawCalls"), Is.False);
#if UNITY_6000_4_OR_NEWER
            Assert.That(result.ContainsKey("batches"), Is.False);
            Assert.That(result["totalIndirectDrawCalls"], Is.EqualTo(UnityStats.totalIndirectDrawCalls));
#else
            Assert.That(result["batches"], Is.EqualTo(UnityStats.batches));
            Assert.That(result.ContainsKey("totalIndirectDrawCalls"), Is.False);
#endif
            Assert.That(VmAutomationCatalog.TryGetTool("profiler/stats", true, out var stats), Is.True);
            var output = (Dictionary<string, object>)stats["outputSchema"];
            foreach (string name in (System.Collections.IEnumerable)output["required"])
                Assert.That(result.ContainsKey(name), Is.True, name);
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
