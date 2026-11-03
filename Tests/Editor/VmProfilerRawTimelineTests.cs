using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine.TestTools;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmProfilerRawTimelineTests
    {
        [TestCase(true, true, 0, 1, null)]
        [TestCase(false, false, 0, 1, null)]
        [TestCase(false, true, -1, 1, null)]
        [TestCase(false, true, 0, 0, null)]
        [TestCase(false, true, 0, 1025, null)]
        [TestCase(false, true, 1, 1, null)]
        public void InvalidAdmissionFailsBeforeReadingNativeSamples(bool recording, bool explicitFrame, int offset, int limit, string identity)
        {
            Assert.That(() => VmProfilerRawTimeline.ValidateRequest(recording, explicitFrame, offset, limit, identity), Throws.Exception);
        }

        [Test]
        public void PagesPreserveTheFullDomainAndRejectChangedCaptures()
        {
            VmProfilerRawTimeline.ValidateRequest(false, true, 0, 1024, null);
            VmProfilerRawTimeline.ValidateRequest(false, true, 1024, 1024, "capture");
            Assert.That(VmProfilerRawTimeline.PageCount(3001, 0, 1024, "capture", null), Is.EqualTo(1024));
            Assert.That(VmProfilerRawTimeline.PageCount(3001, 2048, 1024, "capture", "capture"), Is.EqualTo(953));
            Assert.That(VmProfilerRawTimeline.PageCount(3001, 3001, 1024, "capture", "capture"), Is.Zero);
            Assert.That(VmProfilerRawTimeline.PageCount(0, 0, 1, "capture", null), Is.Zero);
            Assert.That(() => VmProfilerRawTimeline.PageCount(3001, 3002, 1, "capture", "capture"), Throws.Exception);
            Assert.That(() => VmProfilerRawTimeline.PageCount(3001, 1, 1, "new", "capture"), Throws.Exception);
        }

        [TestCase(-1, 1)]
        [TestCase(0, 0)]
        [TestCase(0, 1025)]
        public void CatalogRejectsInvalidPageBounds(int offset, int limit)
        {
            var schema = VmAutomationToolInputSchemaCatalog.Get("profiler/frame-data");
            Assert.That(VmAutomationInputValidator.TryValidate(new Dictionary<string, object>
            {
                { "frameIndex", 1 }, { "rawSamples", new Dictionary<string, object> { { "offset", offset }, { "limit", limit } } },
            }, schema, out _, out _, out _), Is.False);
        }

        [UnityTest]
        public IEnumerator NativePagesRetainNanosecondsAndUnmergedSampleOrder()
        {
            bool enabled = ProfilerDriver.enabled, editor = ProfilerDriver.profileEditor;
            try
            {
                int previous = ProfilerDriver.lastFrameIndex, frame = -1;
                ProfilerDriver.profileEditor = true;
                ProfilerDriver.enabled = true;
                double deadline = EditorApplication.timeSinceStartup + 10;
                for (int update = 0; update < 120 && EditorApplication.timeSinceStartup < deadline; update++)
                {
                    EditorApplication.QueuePlayerLoopUpdate();
                    yield return null;
                    int latest = ProfilerDriver.lastFrameIndex;
                    if (latest <= previous || latest < 0) continue;
                    using (var published = ProfilerDriver.GetRawFrameDataView(latest, 0))
                    {
                        if (!published.valid) continue;
                        frame = latest;
                        break;
                    }
                }
                Assert.That(frame, Is.GreaterThanOrEqualTo(0), $"Native recording published no frame; prior={previous}, last={ProfilerDriver.lastFrameIndex}, enabled={ProfilerDriver.enabled}, editor={ProfilerDriver.profileEditor}.");
                ProfilerDriver.enabled = false;
                using (var native = ProfilerDriver.GetRawFrameDataView(frame, 0))
                {
                    Assert.That(native.valid, Is.True);
                    Assert.That(native.sampleCount, Is.GreaterThan(2));
                    var args = new Dictionary<string, object>
                    {
                        { "frameIndex", frame }, { "threadIndex", 0 }, { "maxItems", 1 }, { "maxDepth", 0 },
                        { "rawSamples", new Dictionary<string, object> { { "offset", 0 }, { "limit", 2 } } },
                    };
                    var response = (Dictionary<string, object>)VmAutomationProfilerCommands.GetFrameData(args);
                    var page = (Dictionary<string, object>)response["rawTimeline"];
                    Assert.That(page["sampleCount"], Is.EqualTo(native.sampleCount));
                    Assert.That(page["frameStartTimeNs"], Is.EqualTo(native.frameStartTimeNs.ToString(CultureInfo.InvariantCulture)));
                    Assert.That(page["frameDurationNs"], Is.EqualTo(native.frameTimeNs.ToString(CultureInfo.InvariantCulture)));
                    Assert.That(page["nextOffset"], Is.EqualTo(2));
                    CheckSamples(page);
                    args["rawSamples"] = new Dictionary<string, object>
                    {
                        { "offset", native.sampleCount - 1 }, { "limit", 2 }, { "expectedIdentity", page["identity"] },
                    };
                    response = (Dictionary<string, object>)VmAutomationProfilerCommands.GetFrameData(args);
                    page = (Dictionary<string, object>)response["rawTimeline"];
                    Assert.That(page["count"], Is.EqualTo(1));
                    Assert.That(page["nextOffset"], Is.Null);
                    CheckSamples(page);
                    args.Remove("rawSamples");
                    response = (Dictionary<string, object>)VmAutomationProfilerCommands.GetFrameData(args);
                    Assert.That(response["rawTimeline"], Is.Null);

                    void CheckSamples(Dictionary<string, object> result)
                    {
                        foreach (var sample in (List<Dictionary<string, object>>)result["samples"])
                        {
                            int index = (int)sample["index"];
                            Assert.That(sample["name"], Is.EqualTo(native.GetSampleName(index)));
                            Assert.That(sample["markerId"], Is.EqualTo(native.GetSampleMarkerId(index)));
                            Assert.That(sample["startTimeNs"], Is.EqualTo(native.GetSampleStartTimeNs(index).ToString(CultureInfo.InvariantCulture)));
                            Assert.That(sample["durationNs"], Is.EqualTo(native.GetSampleTimeNs(index).ToString(CultureInfo.InvariantCulture)));
                            Assert.That(sample["childrenCountRecursive"], Is.EqualTo(native.GetSampleChildrenCountRecursive(index)));
                            Assert.That(sample["flags"], Is.EqualTo((int)native.GetSampleFlags(index)));
                        }
                    }
                }
            }
            finally
            {
                ProfilerDriver.enabled = enabled;
                ProfilerDriver.profileEditor = editor;
            }
        }

        [Test]
        public void NativeNullNamesRemainValidWithoutRelaxingTheNameBound()
        {
            Assert.That(() => VmProfilerRawTimeline.ValidateSampleName(null), Throws.Nothing);
            Assert.That(() => VmProfilerRawTimeline.ValidateSampleName(new string('a', 512)), Throws.Nothing);
            Assert.That(() => VmProfilerRawTimeline.ValidateSampleName(new string('a', 513)), Throws.TypeOf<InvalidOperationException>());
        }
    }
}
