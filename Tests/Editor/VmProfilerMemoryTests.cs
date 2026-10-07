using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmProfilerMemoryTests
    {
        [Test]
        public void NativeMemoryProductPublishesUnusedCapacityWithItsRawCounters()
        {
            var memory = (Dictionary<string, object>)VmAutomationProfilerCommands.GetMemoryInfo(new());
            long used = (long)memory["monoUsedBytes"];
            long heap = (long)memory["monoHeapBytes"];

            Assert.That(heap, Is.GreaterThan(0));
            Assert.That(memory["monoUnusedHeapPercent"],
                Is.EqualTo(Math.Round(100.0 * (heap - used) / heap, 1)));
            Assert.That(memory.ContainsKey("monoFragmentationPercent"), Is.False);
            Assert.That(memory.Count, Is.EqualTo(13));
        }

        [Test]
        public void MemoryContractNamesCapacityWithoutClaimingFragmentation()
        {
            Assert.That(VmAutomationCatalog.TryGetTool("profiler/memory", true, out var tool), Is.True);
            var output = (Dictionary<string, object>)tool["outputSchema"];
            var properties = (Dictionary<string, object>)output["properties"];

            Assert.That(properties.ContainsKey("monoUnusedHeapPercent"), Is.True);
            Assert.That(properties.ContainsKey("monoFragmentationPercent"), Is.False);
            Assert.That(output["required"], Does.Contain("monoUnusedHeapPercent"));
            Assert.That(output["additionalProperties"], Is.False);
        }
    }
}
