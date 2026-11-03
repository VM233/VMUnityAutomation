using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmProfilerMemoryTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void MeshDetailsUseNativeTopologyMetadataAfterCpuDataRetirement(bool retireCpuData)
        {
            var mesh = new Mesh { name = "Memory detail topology witness" };
            try
            {
                mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up, Vector3.one };
                mesh.subMeshCount = 4;
                mesh.SetIndices(new[] { 0, 1, 2 }, MeshTopology.Triangles, 0);
                mesh.SetIndices(new[] { 0, 1, 2, 1, 2, 3 }, MeshTopology.Triangles, 1);
                mesh.SetIndices(new[] { 0, 1, 3, 2 }, MeshTopology.Quads, 2);
                mesh.SetIndices(new[] { 0, 1 }, MeshTopology.Lines, 3);
                if (retireCpuData) mesh.UploadMeshData(true);

                Assert.That(mesh.isReadable, Is.EqualTo(!retireCpuData));
                Assert.That(VmAutomationMemoryProfilerCommands.DescribeMesh(mesh),
                    Is.EqualTo("4 verts, 5 tris"));
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void EmptyMeshDetailsHaveNoTriangles()
        {
            var mesh = new Mesh();
            try
            {
                Assert.That(VmAutomationMemoryProfilerCommands.DescribeMesh(mesh),
                    Is.EqualTo("0 verts, 0 tris"));
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

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
