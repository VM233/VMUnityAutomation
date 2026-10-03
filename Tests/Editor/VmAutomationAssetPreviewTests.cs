using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmAutomationAssetPreviewTests
    {
        [UnityTest]
        public IEnumerator ColdPreviewWaitsForNativeGeneration()
        {
            string path = CreateMaterial();
            Texture2D decoded = null;
            try
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                Assert.That(AssetPreview.GetAssetPreview(material), Is.Null,
                    "The unique fixture must begin without a cached preview.");
                object result = null;
                VmAutomationAssetPreviewRequest.Capture(Arguments(path, 256, 256),
                    value => result = value, null);
                Assert.That(result, Is.Null,
                    "A pending native image must not be replaced with an asset icon.");
                for (int frame = 0; frame < 5000 && result == null; frame++)
                    yield return null;
                decoded = Decode(result, 256, 256);
                Assert.That(AssetPreview.GetAssetPreview(material), Is.Not.Null);
                Assert.That(decoded.GetPixel(128, 128).a, Is.GreaterThan(0.9f));
            }
            finally
            {
                if (decoded != null) UnityEngine.Object.DestroyImmediate(decoded);
                AssetDatabase.DeleteAsset(path);
            }
        }

        [UnityTest]
        public IEnumerator PortraitCanvasPreservesAspectAndActiveTarget()
        {
            string path = CreateMaterial();
            Texture2D decoded = null;
            RenderTexture active = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                object first = null;
                VmAutomationAssetPreviewRequest.Capture(Arguments(path, 256, 256),
                    value => first = value, null);
                for (int frame = 0; frame < 5000 && first == null; frame++)
                    yield return null;
                AssertSuccess(first);
                active = new RenderTexture(8, 8, 0);
                active.Create();
                RenderTexture.active = active;
                object result = null;
                VmAutomationAssetPreviewRequest.Capture(Arguments(path, 128, 256),
                    value => result = value, null);
                Assert.That(RenderTexture.active, Is.SameAs(active));
                decoded = Decode(result, 128, 256);
                Assert.That(decoded.GetPixel(64, 8).a, Is.Zero);
                Assert.That(decoded.GetPixel(64, 247).a, Is.Zero);
                Assert.That(decoded.GetPixel(64, 128).a, Is.GreaterThan(0.9f));
            }
            finally
            {
                RenderTexture.active = previous;
                if (decoded != null) UnityEngine.Object.DestroyImmediate(decoded);
                if (active != null) UnityEngine.Object.DestroyImmediate(active);
                AssetDatabase.DeleteAsset(path);
            }
        }

        [UnityTest]
        public IEnumerator MaterialInformationUsesNativePreviewOwner()
        {
            string path = CreateMaterial();
            Texture2D decoded = null;
            try
            {
                object result = null;
                VmAutomationGraphicsCommands.GetMaterialInfoDeferred(
                    new Dictionary<string, object> { { "assetPath", path } },
                    value => result = value, null);
                for (int frame = 0; frame < 5000 && result == null; frame++)
                    yield return null;
                var data = AssertSuccess(result);
                Assert.That(data["shaderName"], Is.EqualTo("Sprites/Default"));
                Assert.That(AssetPreview.GetAssetPreview(
                    AssetDatabase.LoadAssetAtPath<Material>(path)), Is.Not.Null);
                decoded = new Texture2D(2, 2);
                Assert.That(decoded.LoadImage(Convert.FromBase64String(
                    (string)data["base64"])), Is.True);
                Assert.That(decoded.width, Is.EqualTo(256));
                Assert.That(decoded.height, Is.EqualTo(256));
            }
            finally
            {
                if (decoded != null) UnityEngine.Object.DestroyImmediate(decoded);
                AssetDatabase.DeleteAsset(path);
            }
        }

        [Test]
        public void MissingAssetPublishesExactRejection()
        {
            object result = null;
            VmAutomationAssetPreviewRequest.Capture(
                Arguments("Assets/Missing Native Preview.asset", 256, 256),
                value => result = value, null);
            Assert.That(VmAutomationResponse.TryGetError(result,
                out _, out string code, out _), Is.True);
            Assert.That(code, Is.EqualTo("asset_not_found"));
        }

        private static string CreateMaterial()
        {
            Shader shader = Shader.Find("Sprites/Default");
            Assert.That(shader, Is.Not.Null);
            string path = "Assets/Native Preview " + Guid.NewGuid().ToString("N") + ".mat";
            var material = new Material(shader) { color = Color.yellow };
            AssetDatabase.CreateAsset(material, path);
            return path;
        }

        private static Dictionary<string, object> Arguments(string path, int width, int height)
        {
            return new Dictionary<string, object>
            {
                { "assetPath", path }, { "width", width }, { "height", height },
            };
        }

        private static Dictionary<string, object> AssertSuccess(object result)
        {
            Assert.That(result, Is.Not.Null, "Preview did not complete within the test frame budget.");
            Assert.That(VmAutomationResponse.TryGetError(result,
                out string message, out _, out _), Is.False, message);
            return (Dictionary<string, object>)result;
        }

        private static Texture2D Decode(object result, int width, int height)
        {
            var data = AssertSuccess(result);
            Assert.That(data["width"], Is.EqualTo(width));
            Assert.That(data["height"], Is.EqualTo(height));
            var decoded = new Texture2D(2, 2);
            Assert.That(decoded.LoadImage(Convert.FromBase64String(
                (string)data["base64"])), Is.True);
            Assert.That(decoded.width, Is.EqualTo(width));
            Assert.That(decoded.height, Is.EqualTo(height));
            return decoded;
        }
    }
}
