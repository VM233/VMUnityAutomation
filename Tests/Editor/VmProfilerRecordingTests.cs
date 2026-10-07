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
            int capacity = VmAutomationProfilerFrameHistory.FrameCount;
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
                Assert.That(previous["frameHistoryLength"], Is.EqualTo(capacity));
                Assert.That(result["frameHistoryLength"], Is.EqualTo(capacity));
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
                VmAutomationProfilerFrameHistory.FrameCount = capacity;
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
            var history = (Dictionary<string, object>)inputProperties["frameHistoryLength"];
            Assert.That(history["minimum"], Is.EqualTo(1));
            Assert.That(history["maximum"], Is.EqualTo(VmAutomationProfilerFrameHistory.MaximumFrames));
            var output = (Dictionary<string, object>)tool["outputSchema"];
            var outputProperties = (Dictionary<string, object>)output["properties"];
            Assert.That(outputProperties.ContainsKey("profileEditor"), Is.True);
            Assert.That(outputProperties.ContainsKey("previous"), Is.True);
            Assert.That(outputProperties.ContainsKey("framesCleared"), Is.True);
            Assert.That(outputProperties.ContainsKey("previousFirstFrame"), Is.True);
            Assert.That(outputProperties.ContainsKey("previousLastFrame"), Is.True);
            Assert.That(outputProperties.ContainsKey("frameHistoryLength"), Is.True);
        }

        [Test]
        public void MethodAddressesRetainTheFullUnsigned64BitDomain()
        {
            var schema = VmAutomationToolInputSchemaCatalog.Get("profiler/frame-data");
            var request = new Dictionary<string, object>
            {
                { "methodAddresses", new List<object> { "0x0000000000000000", "0xffffffffffffffff" } }
            };
            Assert.That(VmAutomationInputValidator.TryValidate(request, schema,
                out _, out string error, out _), Is.True, error);
        }

        [TestCase("0x0")]
        [TestCase("0x10000000000000000")]
        [TestCase("0x000000000000000g")]
        [TestCase("0x000000000000000A")]
        [TestCase("000000000000000000")]
        public void MalformedMethodAddressesAreRejectedBeforeNativeAdmission(string address)
        {
            var schema = VmAutomationToolInputSchemaCatalog.Get("profiler/frame-data");
            Assert.That(VmAutomationInputValidator.TryValidate(new Dictionary<string, object>
            {
                { "methodAddresses", new List<object> { address } }
            }, schema, out string code, out _, out _), Is.False);
            Assert.That(code, Is.EqualTo("invalid_arguments"));
        }

        [Test]
        public void NumericDuplicateEmptyAndOversizedMethodRequestsAreRejected()
        {
            var schema = VmAutomationToolInputSchemaCatalog.Get("profiler/frame-data");
            var oversized = new List<object>();
            for (int index = 0; index < 17; index++) oversized.Add("0x" + index.ToString("x16"));
            foreach (var addresses in new[]
            {
                new List<object>(), new List<object> { 9007199254740992d },
                new List<object> { "0x0000000000000001", "0x0000000000000001" }, oversized
            })
            {
                Assert.That(VmAutomationInputValidator.TryValidate(new Dictionary<string, object>
                {
                    { "methodAddresses", addresses }
                }, schema, out _, out _, out _), Is.False);
            }
            Assert.That(VmAutomationInputValidator.TryValidate(new Dictionary<string, object>(),
                schema, out _, out _, out _), Is.True);
        }

        [TestCase(1)]
        [TestCase(128)]
        public void FrameHistoryCapacityPublishesNativePreviousStateAndRestores(int frames)
        {
            bool enabled = ProfilerDriver.enabled;
            int capacity = VmAutomationProfilerFrameHistory.FrameCount;
            try
            {
                var result = (Dictionary<string, object>)VmAutomationProfilerCommands.EnableProfiler(new()
                {
                    { "enabled", false }, { "frameHistoryLength", frames }
                });
                var previous = (Dictionary<string, object>)result["previous"];
                Assert.That(previous["frameHistoryLength"], Is.EqualTo(capacity));
                Assert.That(result["frameHistoryLength"], Is.EqualTo(frames));
                Assert.That(VmAutomationProfilerFrameHistory.FrameCount, Is.EqualTo(frames));
                VmAutomationProfilerCommands.EnableProfiler(previous);
                Assert.That(VmAutomationProfilerFrameHistory.FrameCount, Is.EqualTo(capacity));
                Assert.That(ProfilerDriver.enabled, Is.EqualTo(enabled));
            }
            finally
            {
                VmAutomationProfilerFrameHistory.FrameCount = capacity;
                ProfilerDriver.enabled = enabled;
            }
        }

        [TestCase(0)]
        public void InvalidFrameHistoryCapacityFailsBeforeMutatingNativeSettings(int frames)
        {
            int capacity = VmAutomationProfilerFrameHistory.FrameCount;
            bool enabled = ProfilerDriver.enabled;
            bool deep = ProfilerDriver.deepProfiling;
            bool editor = ProfilerDriver.profileEditor;
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                VmAutomationProfilerCommands.EnableProfiler(new()
                {
                    { "frameHistoryLength", frames }, { "enabled", !enabled },
                    { "deepProfiling", !deep }, { "profileEditor", !editor }
                }));
            Assert.That(VmAutomationProfilerFrameHistory.FrameCount, Is.EqualTo(capacity));
            Assert.That(ProfilerDriver.enabled, Is.EqualTo(enabled));
            Assert.That(ProfilerDriver.deepProfiling, Is.EqualTo(deep));
            Assert.That(ProfilerDriver.profileEditor, Is.EqualTo(editor));
        }

        [Test]
        public void MaximumFrameHistoryCapacityUsesTheNativeLimit()
        {
            FrameHistoryCapacityPublishesNativePreviousStateAndRestores(
                VmAutomationProfilerFrameHistory.MaximumFrames);
        }

        [Test]
        public void FrameHistoryCapacityAboveNativeLimitFailsBeforeMutation()
        {
            InvalidFrameHistoryCapacityFailsBeforeMutatingNativeSettings(
                VmAutomationProfilerFrameHistory.MaximumFrames + 1);
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

        [Test]
        public void MemoryBreakdownPublishesCategoryObjectsAndBooleanPackagePresence()
        {
            Assert.That(VmAutomationCatalog.TryGetTool("profiler/memory-breakdown", true, out var tool), Is.True);
            var schema = (Dictionary<string, object>)tool["outputSchema"];
            var fields = (Dictionary<string, object>)schema["properties"];
            var installed = (Dictionary<string, object>)fields["memoryProfilerPackageInstalled"];
            Assert.That(installed["type"], Is.EqualTo("boolean"));
            var categories = (Dictionary<string, object>)fields["categories"];
            Assert.That(categories["type"], Is.EqualTo("object"));
            Assert.That(categories["additionalProperties"], Is.False);
            var categoryFields = (Dictionary<string, object>)categories["properties"];
            Assert.That(categoryFields.Count, Is.EqualTo(9));
            foreach (string name in new[] { "textures", "renderTextures", "meshes", "materials", "shaders",
                         "audioClips", "animationClips", "fonts", "scriptableObjects" })
            {
                var category = (Dictionary<string, object>)categoryFields[name];
                var values = (Dictionary<string, object>)category["properties"];
                Assert.That(category["additionalProperties"], Is.False);
                Assert.That(((Dictionary<string, object>)values["count"])["type"], Is.EqualTo("integer"));
                Assert.That(((Dictionary<string, object>)values["totalMB"])["type"], Is.EqualTo("number"));
                Assert.That(((Dictionary<string, object>)values["totalBytes"])["type"], Is.EqualTo("integer"));
                var assets = (Dictionary<string, object>)values["topAssets"];
                Assert.That(assets["type"], Is.EqualTo("array"));
                var item = (Dictionary<string, object>)assets["items"];
                Assert.That(item["additionalProperties"], Is.False);
                var itemFields = (Dictionary<string, object>)item["properties"];
                Assert.That(itemFields.Keys, Is.EquivalentTo(new[] { "name", "sizeMB", "sizeBytes", "detail", "assetPath" }));
                Assert.That(item["required"], Is.EquivalentTo(new[] { "name", "sizeMB", "sizeBytes" }));
                Assert.That(category["required"], Is.EquivalentTo(new[] { "count", "totalMB", "totalBytes" }));
            }
        }
    }
}
