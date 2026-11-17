using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmTextCoreDynamicAtlasLifecycleTests
    {
        [Test]
        public void EmptyDynamicAtlasShrinksAndCanRegenerateWithoutChangingAuthoringOrObjects()
        {
            // A built-in dynamic Font can lack the source data required by TextCore. Use an imported
            // TTF/OTF with Include Font Data; only the independent in-memory FontAsset is mutated.
            string[] sources = AssetDatabase.FindAssets("t:Font", new[] { "Assets" });
            Assert.That(sources.Length, Is.InRange(1, 128), "Supply a bounded imported font fixture for this lifecycle test.");
            string sourcePath = sources.Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path, StringComparer.Ordinal)
                .FirstOrDefault(path => AssetImporter.GetAtPath(path) is TrueTypeFontImporter importer && importer.includeFontData);
            Assert.That(sourcePath, Is.Not.Null, "The imported fixture must include its native font data.");
            var font = FontAsset.CreateFontAsset(AssetDatabase.LoadAssetAtPath<Font>(sourcePath),
                32, 4, GlyphRenderMode.SDFAA, 512, 512, AtlasPopulationMode.Dynamic);
            Assert.That(font, Is.Not.Null, "TextCore must load the imported fixture's real source face.");
            var atlas = font.atlasTextures[0];
            var material = font.material;
            try
            {
                Assert.That(font.TryAddCharacters("Atlas reset regression"), Is.True);
                font.ClearFontAssetData();
                Assert.That(atlas.width, Is.EqualTo(512), "The original default retains a full empty atlas.");
                for (int lease = 0; lease < 2; lease++)
                {
                    VmTextCoreFontAssetRebuildTool.ClearDynamicCache(font);
                    Assert.That(font.atlasTextures[0], Is.SameAs(atlas));
                    Assert.That(font.material, Is.SameAs(material));
                    Assert.That(atlas.width, Is.LessThanOrEqualTo(1));
                    Assert.That(atlas.height, Is.LessThanOrEqualTo(1));
                    Assert.That(font.atlasWidth, Is.EqualTo(512));
                    Assert.That(font.atlasHeight, Is.EqualTo(512));
                    Assert.That(font.glyphTable, Is.Empty);
                    Assert.That(font.characterTable, Is.Empty);
                    Assert.That(font.TryAddCharacters("Atlas reset regression"), Is.True);
                    Assert.That(atlas.width, Is.EqualTo(512));
                    Assert.That(font.characterTable, Is.Not.Empty);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(material);
                UnityEngine.Object.DestroyImmediate(atlas);
                UnityEngine.Object.DestroyImmediate(font);
            }
        }
    }

    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmTextCoreFontAssetRebuildTests
    {
        [TestCase(2L * 4096 * 4096 + 256 * 1024)]
        [TestCase(33723931L)]
        [TestCase(36L * 1024 * 1024)]
        public void SupportedSerializedCachesIncludeAtlasEncodingAndMetadata(long assetBytes)
        {
            Assert.DoesNotThrow(() =>
                VmTextCoreFontAssetRebuildTool.ValidateSerializedFileLengths(assetBytes, 64 * 1024));
        }

        [TestCase(36L * 1024 * 1024 + 1, 1L)]
        [TestCase(1L, 64L * 1024 + 1)]
        public void FileAndMetaLimitsRemainStrict(long assetBytes, long metaBytes)
        {
            var error = Assert.Throws<VmProjectToolException>(() =>
                VmTextCoreFontAssetRebuildTool.ValidateSerializedFileLengths(assetBytes, metaBytes));
            Assert.That(error.ErrorCode, Is.EqualTo("unsupported_font_asset"));
        }

        [Test]
        public void MissingSerializedTargetIsRejectedBeforeNativeFontLoading()
        {
            var error = Assert.Throws<VmProjectToolException>(() =>
                new VmTextCoreFontAssetRebuildTool().Execute(new VmTextCoreFontAssetRebuildRequest
                {
                    FontAssetPath = "Assets/VMFontBudgetMissing-" + Guid.NewGuid().ToString("N") + ".asset"
                }));
            Assert.That(error.ErrorCode, Is.EqualTo("unsupported_font_asset"));
            Assert.That(error.Message, Does.Contain("existing serialized font asset"));
        }
    }
}
