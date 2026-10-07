using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmAssetImportResizeTests
    {
        private string sourceDirectory;
        private string source;
        private string folder;
        private string destination;

        [SetUp]
        public void SetUp()
        {
            string identity = Guid.NewGuid().ToString("N");
            sourceDirectory = Path.Combine(Path.GetDirectoryName(Application.dataPath),
                "Temp", "VmAssetImportResizeTests-" + identity);
            Directory.CreateDirectory(sourceDirectory);
            source = Path.Combine(sourceDirectory, "Source.png");
            folder = "Assets/VmAssetImportResizeTests-" + identity;
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            destination = folder + "/Result.png";
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(folder);
            foreach (string path in Directory.GetFiles(sourceDirectory)) File.Delete(path);
            Directory.Delete(sourceDirectory);
        }

        [Test]
        public void DirectImportMatchesStandaloneAlphaAndPreservesSourceAndPpu()
        {
            WriteSource(2, 1, new[] { new Color32(255, 0, 0, 255), new Color32(0, 0, 255, 0) });
            byte[] original = File.ReadAllBytes(source);
            string standalone = Path.Combine(sourceDirectory, "Standalone.png");
            var standaloneResult = new VmImageResizeTool().Execute(new VmImageResizeRequest
                { SourcePath = source, OutputPath = standalone, Width = 1 });
            Dictionary<string, object> result = Import(Request());
            Assert.That(result["success"], Is.True);
            var receipt = (Dictionary<string, object>)First(result)["resize"];
            Assert.That(receipt["verified"], Is.True);
            Assert.That(receipt["outputSha256"], Is.EqualTo(standaloneResult.OutputSha256));
            Assert.That(File.ReadAllBytes(Absolute(destination)), Is.EqualTo(File.ReadAllBytes(standalone)));
            Assert.That(File.ReadAllBytes(source), Is.EqualTo(original));
            Assert.That(Directory.GetFiles(sourceDirectory), Has.Length.EqualTo(2));
            Assert.That(Directory.GetFiles(Absolute(folder)), Has.Length.EqualTo(2));
            var importer = (TextureImporter)AssetImporter.GetAtPath(destination);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            Assert.That(settings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect));
            Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(100));
            Assert.That(AssetDatabase.LoadAssetAtPath<Sprite>(destination).rect.size, Is.EqualTo(Vector2.one));
        }

        [Test]
        public void DryRunFingerprintsResizedPixelsWithoutPublishing()
        {
            WriteSolid(8, 4);
            var request = Request();
            request["dryRun"] = true;
            var result = Import(request);
            Assert.That(result["success"], Is.True);
            var receipt = (Dictionary<string, object>)First(result)["resize"];
            Assert.That(receipt["sourceWidth"], Is.EqualTo(8));
            Assert.That(receipt["width"], Is.EqualTo(1));
            Assert.That(receipt["verified"], Is.False);
            Assert.That(receipt["outputSha256"], Has.Length.EqualTo(64));
            Assert.That(File.Exists(Absolute(destination)), Is.False);
            Assert.That(Directory.GetFiles(sourceDirectory), Has.Length.EqualTo(1));
            Assert.That(Directory.GetFiles(Absolute(folder)), Is.Empty);
        }

        [Test]
        public void DuplicateDetectionUsesResizedContentAgainstExistingAsset()
        {
            WriteSolid(2, 1);
            Assert.That(Import(Request())["importedCount"], Is.EqualTo(1));
            WriteSolid(8, 4);
            var request = Request(folder + "/Duplicate.png");
            var defaults = (Dictionary<string, object>)request["defaults"];
            defaults["dedupeMode"] = "decodedPixels";
            defaults["dedupeScope"] = "destinationFolder";
            var result = Import(request);
            Assert.That(result["success"], Is.True);
            Assert.That(result["skippedCount"], Is.EqualTo(1));
            Assert.That(First(result)["duplicateAssetPath"], Is.EqualTo(destination));
            Assert.That(((Dictionary<string, object>)First(result)["resize"])["verified"], Is.False);
            Assert.That(File.Exists(Absolute(folder + "/Duplicate.png")), Is.False);
        }

        [Test]
        public void OverwritePreservesGuidAndSpriteFileId()
        {
            WriteSolid(2, 1);
            Import(Request());
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(AssetDatabase.LoadAssetAtPath<Sprite>(destination),
                out string guid, out long fileId);
            var request = Request();
            var defaults = (Dictionary<string, object>)request["defaults"];
            defaults["overwrite"] = true;
            defaults["resize"] = new Dictionary<string, object> { { "width", 4 }, { "filter", "Nearest" } };
            var result = Import(request);
            Assert.That(result["success"], Is.True);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(AssetDatabase.LoadAssetAtPath<Sprite>(destination),
                out string newGuid, out long newFileId);
            Assert.That(newGuid, Is.EqualTo(guid));
            Assert.That(newFileId, Is.EqualTo(fileId));
            Assert.That(AssetDatabase.LoadAssetAtPath<Sprite>(destination).rect.size, Is.EqualTo(new Vector2(4, 2)));
        }

        [TestCase(2, false)]
        [TestCase(1, true)]
        public void SliceAdmissionUsesFinalDimensions(int frameWidth, bool succeeds)
        {
            WriteSolid(8, 4);
            var request = Request();
            var defaults = (Dictionary<string, object>)request["defaults"];
            defaults["spriteSlice"] = new Dictionary<string, object>
                { { "frameWidth", frameWidth }, { "frameHeight", 1 } };
            var result = Import(request);
            Assert.That(result["success"], Is.EqualTo(succeeds));
            Assert.That(File.Exists(Absolute(destination)), Is.EqualTo(succeeds));
        }

        [Test]
        public void InPlaceResizePreservesIdentitySettingsAndDryRunBytes()
        {
            WriteSolid(8, 4);
            var seed = Request();
            ((Dictionary<string, object>)seed["defaults"])["resize"] =
                new Dictionary<string, object> { { "width", 8 } };
            Import(seed);
            var importer = (TextureImporter)AssetImporter.GetAtPath(destination);
            importer.spritePivot = new Vector2(0.3f, 0.7f);
            importer.SaveAndReimport();
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(AssetDatabase.LoadAssetAtPath<Sprite>(destination),
                out string guid, out long fileId);
            byte[] original = File.ReadAllBytes(Absolute(destination));
            var request = Request();
            request["defaults"] = new Dictionary<string, object>
            {
                { "overwrite", true }, { "dedupeMode", "none" },
                { "resize", new Dictionary<string, object> { { "width", 4 } } }
            };
            ((List<Dictionary<string, object>>)request["imports"])[0]["sourcePath"] = Absolute(destination);
            request["dryRun"] = true;
            Assert.That(Import(request)["success"], Is.True);
            Assert.That(File.ReadAllBytes(Absolute(destination)), Is.EqualTo(original));
            request["dryRun"] = false;
            var result = Import(request);
            Assert.That(result["success"], Is.True);
            var receipt = (Dictionary<string, object>)First(result)["resize"];
            Assert.That(receipt["sourceWidth"], Is.EqualTo(8));
            Assert.That(receipt["width"], Is.EqualTo(4));
            Assert.That(receipt["verified"], Is.True);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(destination);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string newGuid, out long newFileId);
            Assert.That(newGuid, Is.EqualTo(guid));
            Assert.That(newFileId, Is.EqualTo(fileId));
            Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(4, 2)));
            importer = (TextureImporter)AssetImporter.GetAtPath(destination);
            Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(100));
            Assert.That(importer.spritePivot, Is.EqualTo(new Vector2(0.3f, 0.7f)));
            Assert.That(Directory.GetFiles(Absolute(folder)), Has.Length.EqualTo(2));
        }

        [TestCase(false, true)]
        [TestCase(true, false)]
        public void InPlaceResizeRequiresOverwriteAndResize(bool overwrite, bool resize)
        {
            WriteSolid(8, 4);
            Import(Request());
            byte[] original = File.ReadAllBytes(Absolute(destination));
            var request = Request();
            var defaults = (Dictionary<string, object>)request["defaults"];
            defaults["overwrite"] = overwrite;
            if (!resize) defaults.Remove("resize");
            ((List<Dictionary<string, object>>)request["imports"])[0]["sourcePath"] = Absolute(destination);
            Assert.That(Import(request)["success"], Is.False);
            Assert.That(File.ReadAllBytes(Absolute(destination)), Is.EqualTo(original));
        }

        [TestCase("width", 0)]
        [TestCase("width", 1.5)]
        [TestCase("height", null)]
        [TestCase("pixelsPerUnit", 350)]
        public void InvalidResizeFailsBeforeAnyDestinationWrite(string key, object value)
        {
            WriteSolid(2, 1);
            var request = Request();
            ((Dictionary<string, object>)request["defaults"])["resize"] =
                new Dictionary<string, object> { { key, value } };
            Assert.That(Import(request)["success"], Is.False);
            Assert.That(Directory.GetFiles(Absolute(folder)), Is.Empty);
        }

        [Test]
        public void BatchBudgetFailureOccursBeforeDecodeOrPublication()
        {
            WriteSolid(2, 1);
            var exception = Assert.Throws<VmProjectToolException>(() =>
                VmPngResizePreparation.Prepare(new VmImageResizeRequest
                    { SourcePath = source, Width = 1 }, true, 2, 1024));
            Assert.That(exception.ErrorCode, Is.EqualTo("image_size_limit"));
            Assert.That(Directory.GetFiles(Absolute(folder)), Is.Empty);
        }

        [UnityTest]
        public IEnumerator DeferredFailureRestoresPreviousBytesAndGuid() => DeferredRollback(false);

        [UnityTest]
        public IEnumerator DeferredInPlaceFailureRestoresPreviousBytesAndGuid() => DeferredRollback(true);

        private IEnumerator DeferredRollback(bool inPlace)
        {
            WriteSolid(2, 1);
            Import(Request());
            byte[] original = File.ReadAllBytes(Absolute(destination));
            string guid = AssetDatabase.AssetPathToGUID(destination);
            string secondSource = Path.Combine(sourceDirectory, "Second.png");
            File.Copy(source, secondSource);
            var request = Request();
            var defaults = (Dictionary<string, object>)request["defaults"];
            defaults.Remove("resize");
            defaults["overwrite"] = true;
            var entries = (List<Dictionary<string, object>>)request["imports"];
            if (inPlace) entries[0]["sourcePath"] = Absolute(destination);
            entries[0]["resize"] = new Dictionary<string, object> { { "width", 4 } };
            entries.Add(new Dictionary<string, object>
                { { "sourcePath", secondSource }, { "destinationPath", folder + "/Second.png" } });
            request["execution"] = new Dictionary<string, object>
                { { "mode", "batched" }, { "operationsPerFrame", 1 } };
            object result = null;
            VmAutomationAssetImportCommands.ImportDeferred(request, value => result = value,
                _ => { if (File.Exists(secondSource)) File.Delete(secondSource); });
            for (int frame = 0; result == null && frame < 240; frame++) yield return null;
            Assert.That(result, Is.Not.Null);
            var receipt = (Dictionary<string, object>)result;
            Assert.That(receipt["success"], Is.False);
            Assert.That(receipt["allTouchedRolledBack"], Is.True);
            Assert.That(File.ReadAllBytes(Absolute(destination)), Is.EqualTo(original));
            Assert.That(AssetDatabase.AssetPathToGUID(destination), Is.EqualTo(guid));
            Assert.That(((Dictionary<string, object>)First(receipt)["resize"])["verified"], Is.False);
            Assert.That(File.Exists(Absolute(folder + "/Second.png")), Is.False);
        }

        [Test]
        public void LoadedNativeAssetOverwritePreservesBytesAndIdentity()
        {
            var request = PrepareLoadedClip(out var original, out var meta, out var guid, out var fileId);
            Assert.That(Import(request)["success"], Is.True);
            AssertLoadedClip(original, meta, guid, fileId, 1);
        }

        [TestCase(false)]
        [TestCase(true)]
        [Platform(Include = "Win")]
        public void MappedNativeAssetPublishesSmallerSnapshot(bool publishBytes)
        {
            var request = PrepareLoadedClip(out var original, out var meta, out var guid, out var fileId);
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(destination);
            clip.name = new string('M', 1024);
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();
            string target = Absolute(destination);
            byte[] mappedBytes = File.ReadAllBytes(target);
            Assert.That(mappedBytes.Length, Is.GreaterThan(original.Length));
            using (var file = new FileStream(target, FileMode.Open, FileAccess.Read,
                       FileShare.ReadWrite | FileShare.Delete))
            using (var mapping = MemoryMappedFile.CreateFromFile(file, null, 0,
                       MemoryMappedFileAccess.Read, HandleInheritability.None, true))
            using (var view = mapping.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read))
            {
                AssetDatabase.ReleaseCachedFileHandles();
                var rejection = Assert.Throws<IOException>(() => File.Copy(source, target, true));
                Assert.That(rejection.HResult & 0xFFFF, Is.EqualTo(1224));
                Assert.That(File.ReadAllBytes(target), Is.EqualTo(mappedBytes));
                if (publishBytes)
                {
                    VmAutomationPersistenceFile.WriteAllBytes(target, original);
                    AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport);
                }
                else Assert.That(Import(request)["success"], Is.True);
                var stillMapped = new byte[mappedBytes.Length];
                view.ReadArray(0, stillMapped, 0, stillMapped.Length);
                Assert.That(stillMapped, Is.EqualTo(mappedBytes));
                AssertLoadedClip(original, meta, guid, fileId, 1);
            }
        }

        [UnityTest]
        public IEnumerator DeferredLoadedNativeAssetOverwritePreservesBytesAndIdentity() => LoadedClipDeferred(false);

        [UnityTest]
        public IEnumerator DeferredLoadedNativeAssetFailureRestoresBytesAndIdentity() => LoadedClipDeferred(true);

        private IEnumerator LoadedClipDeferred(bool fail)
        {
            var request = PrepareLoadedClip(out var original, out var meta, out var guid, out var fileId);
            var changed = File.ReadAllBytes(Absolute(destination));
            string control = Path.Combine(sourceDirectory, "Control.anim");
            File.Copy(source, control);
            ((List<Dictionary<string, object>>)request["imports"]).Add(new Dictionary<string, object>
                { { "sourcePath", control }, { "destinationPath", folder + "/Control.anim" } });
            request["execution"] = new Dictionary<string, object>
                { { "mode", "batched" }, { "operationsPerFrame", 1 } };
            object receipt = null;
            VmAutomationAssetImportCommands.ImportDeferred(request, result => receipt = result,
                _ => { if (fail && File.Exists(control)) File.Delete(control); });
            for (int frame = 0; receipt == null && frame < 240; frame++) yield return null;
            Assert.That(receipt, Is.Not.Null);
            var result = (Dictionary<string, object>)receipt;
            Assert.That(result["success"], Is.EqualTo(!fail));
            if (fail) Assert.That(result["allTouchedRolledBack"], Is.True);
            AssertLoadedClip(fail ? changed : original, meta, guid, fileId, fail ? 9 : 1);
        }

        private Dictionary<string, object> PrepareLoadedClip(out byte[] original, out byte[] meta,
            out string guid, out long fileId)
        {
            destination = folder + "/Loaded.anim";
            source = Path.Combine(sourceDirectory, "Original.anim");
            var clip = new AnimationClip();
            var binding = EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.x");
            AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Linear(0, 1, 1, 1));
            AssetDatabase.CreateAsset(clip, destination);
            AssetDatabase.SaveAssets();
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(clip, out guid, out fileId);
            original = File.ReadAllBytes(Absolute(destination));
            meta = File.ReadAllBytes(Absolute(destination) + ".meta");
            File.Copy(Absolute(destination), source);
            AnimationUtility.SetEditorCurve(clip, binding, AnimationCurve.Linear(0, 9, 1, 9));
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();
            Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>(destination), Is.Not.Null);
            return new Dictionary<string, object>
            {
                { "imports", new List<Dictionary<string, object>> { new Dictionary<string, object>
                    { { "sourcePath", source }, { "destinationPath", destination }, { "overwrite", true },
                        { "dedupeMode", "none" } } } }
            };
        }

        private void AssertLoadedClip(byte[] bytes, byte[] meta, string guid, long fileId, float value)
        {
            Assert.That(File.ReadAllBytes(Absolute(destination)), Is.EqualTo(bytes));
            Assert.That(File.ReadAllBytes(Absolute(destination) + ".meta"), Is.EqualTo(meta));
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(destination);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(clip, out string actualGuid, out long actualFileId);
            Assert.That(actualGuid, Is.EqualTo(guid));
            Assert.That(actualFileId, Is.EqualTo(fileId));
            var binding = EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.x");
            Assert.That(AnimationUtility.GetEditorCurve(clip, binding).Evaluate(0), Is.EqualTo(value));
        }

        private Dictionary<string, object> Request(string path = null) => new Dictionary<string, object>
        {
            { "defaults", new Dictionary<string, object>
                {
                    { "resize", new Dictionary<string, object> { { "width", 1 } } },
                    { "textureType", "Sprite" }, { "spriteMode", "Single" },
                    { "pixelsPerUnit", 100 }, { "meshType", "FullRect" },
                    { "compression", "uncompressed" }, { "mipmapEnabled", false },
                    { "dedupeMode", "none" },
                }
            },
            { "imports", new List<Dictionary<string, object>>
                { new Dictionary<string, object> { { "sourcePath", source }, { "destinationPath", path ?? destination } } }
            },
        };

        private static Dictionary<string, object> Import(Dictionary<string, object> request) =>
            (Dictionary<string, object>)VmAutomationAssetImportCommands.Import(request);

        private static Dictionary<string, object> First(Dictionary<string, object> result) =>
            ((List<Dictionary<string, object>>)result["imports"])[0];

        private static string Absolute(string path) =>
            Path.Combine(Path.GetDirectoryName(Application.dataPath), path);

        private void WriteSolid(int width, int height) =>
            WriteSource(width, height, Enumerable.Repeat(new Color32(200, 30, 10, 255), width * height).ToArray());

        private void WriteSource(int width, int height, Color32[] pixels)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            try
            {
                texture.SetPixels32(pixels);
                File.WriteAllBytes(source, ImageConversion.EncodeToPNG(texture));
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }
    }
}
