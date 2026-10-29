using System;
using NUnit.Framework;

namespace VMUnityAutomation.Editor.Tests
{
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
