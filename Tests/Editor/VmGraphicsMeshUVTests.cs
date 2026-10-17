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
