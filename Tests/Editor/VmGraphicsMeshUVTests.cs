using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmGraphicsMeshUVTests
    {
        [Test]
        public void MeshMetadataPublishesTheSameShapeLimitsAsItsNativeOwner()
        {
            Assert.That(VmAutomationGeneratedRouteContracts.TryGetOutput("graphics/mesh-info", out var schema), Is.True);
            var properties = (Dictionary<string, object>)schema["properties"];
            var names = (Dictionary<string, object>)properties["blendShapeNames"];
            Assert.That(names["maxItems"], Is.EqualTo(512));
            var item = (Dictionary<string, object>)names["items"];
            Assert.That(item["maxLength"], Is.EqualTo(512));
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void MeshMetadataUsesTheSameNativeBindingAndShapesForEverySelector(
            bool skinned, bool releaseCpuData)
        {
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Native Mesh Binding Review.asset");
            var mesh = new Mesh { name = "Native Mesh Binding Review" };
            var host = new GameObject("Native Mesh Binding Review " + Guid.NewGuid().ToString("N"));
            try
            {
                mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
                mesh.normals = new[] { Vector3.forward, Vector3.forward, Vector3.forward };
                mesh.colors = new[] { Color.red, Color.green, Color.blue };
                mesh.subMeshCount = 2;
                mesh.SetIndices(new[] { 0, 1, 2 }, MeshTopology.Triangles, 0);
                mesh.SetIndices(new[] { 0, 1 }, MeshTopology.Lines, 1);
                var deltas = new[] { Vector3.zero, Vector3.up, Vector3.zero };
                mesh.AddBlendShapeFrame("Face.Round", 100, deltas, null, null);
                mesh.AddBlendShapeFrame("Hair Length", 100, deltas, null, null);
                if (skinned)
                {
                    mesh.bindposes = new[] { Matrix4x4.identity, Matrix4x4.identity };
                    mesh.boneWeights = new[]
                    {
                        new BoneWeight { boneIndex0 = 0, weight0 = 1 },
                        new BoneWeight { boneIndex0 = 1, weight0 = 1 },
                        new BoneWeight { boneIndex0 = 0, weight0 = 1 }
                    };
                    var renderer = host.AddComponent<SkinnedMeshRenderer>();
                    renderer.sharedMesh = mesh;
                    renderer.bones = new[] { host.transform, host.transform };
                }
                else host.AddComponent<MeshFilter>().sharedMesh = mesh;
                AssetDatabase.CreateAsset(mesh, path);
                if (releaseCpuData) mesh.UploadMeshData(true);
                var asset = (Dictionary<string, object>)VmAutomationGraphicsCommands.GetMeshInfo(new()
                    { ["assetPath"] = path });
                var scene = (Dictionary<string, object>)VmAutomationGraphicsCommands.GetMeshInfo(new()
                    { ["gameObjectPath"] = host.name });
                foreach (var result in new[] { asset, scene })
                {
                    Assert.That(result["isSkinned"], Is.EqualTo(skinned));
                    Assert.That(result["boneCount"], Is.EqualTo(skinned ? 2 : 0));
                    Assert.That(result["blendShapeNames"], Is.EqualTo(new[] { "Face.Round", "Hair Length" }));
                    Assert.That(result["blendShapeCount"], Is.EqualTo(2));
                    Assert.That(Convert.ToInt64(result["triangleCount"]), Is.EqualTo(1));
                    Assert.That(result["hasNormals"], Is.True);
                    Assert.That(result["hasColors"], Is.True);
                    Assert.That(result["hasTangents"], Is.False);
                    Assert.That(result["isReadable"], Is.EqualTo(!releaseCpuData));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
                if (EditorUtility.IsPersistent(mesh)) AssetDatabase.DeleteAsset(path);
                else UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        [TestCase("shape-count")]
        [TestCase("shape-name")]
        [TestCase("submesh-count")]
        public void MeshMetadataRejectsAnIncompleteOverBudgetReport(string limit)
        {
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Native Mesh Limit Review.asset");
            var mesh = new Mesh { name = "Native Mesh Limit Review" };
            try
            {
                mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
                if (limit == "shape-count")
                    for (int shape = 0; shape < 513; shape++)
                        mesh.AddBlendShapeFrame("Shape" + shape, 100, new Vector3[3], null, null);
                else if (limit == "shape-name")
                    mesh.AddBlendShapeFrame(new string('a', 513), 100, new Vector3[3], null, null);
                else mesh.subMeshCount = 4097;
                AssetDatabase.CreateAsset(mesh, path);
                var result = (Dictionary<string, object>)VmAutomationGraphicsCommands.GetMeshInfo(new()
                    { ["assetPath"] = path });
                Assert.That(result["success"], Is.False);
                Assert.That(result["errorCode"], Is.EqualTo("mesh_metadata_limit"));
                Assert.That(result.ContainsKey("vertexCount"), Is.False);
            }
            finally
            {
                if (EditorUtility.IsPersistent(mesh)) AssetDatabase.DeleteAsset(path);
                else UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        [TestCase(0, 0)]
        [TestCase(1, 1)]
        [TestCase(1 << 5, 1)]
        [TestCase(1 << 7, 1)]
        [TestCase((1 << 0) | (1 << 5) | (1 << 7), 3)]
        [TestCase(255, 8)]
        public void MeshInfoCountsTheActualNativeUVLayout(int channels, int expected)
        {
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Native Mesh UV Review.asset");
            var mesh = new Mesh { name = "Native Mesh UV Review" };
            try
            {
                mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
                mesh.triangles = new[] { 0, 1, 2 };
                var uv = new[] { Vector2.zero, Vector2.right, Vector2.up };
                for (int channel = 0; channel < 8; channel++)
                    if ((channels & (1 << channel)) != 0) mesh.SetUVs(channel, uv);
                AssetDatabase.CreateAsset(mesh, path);
                var result = (Dictionary<string, object>)VmAutomationGraphicsCommands.GetMeshInfo(new()
                    { ["assetPath"] = path });
                Assert.That(result["name"], Is.EqualTo(mesh.name));
                Assert.That(Convert.ToInt32(result["uvChannelCount"]), Is.EqualTo(expected));
                Assert.That(Convert.ToInt32(result["vertexCount"]), Is.EqualTo(3));
                Assert.That(Convert.ToInt32(result["triangleCount"]), Is.EqualTo(1));
            }
            finally
            {
                if (EditorUtility.IsPersistent(mesh)) AssetDatabase.DeleteAsset(path);
                else UnityEngine.Object.DestroyImmediate(mesh);
            }
        }
    }
}
