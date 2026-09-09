using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmAutomationVFXGraphSessionTests
    {
        [Test]
        public void OpeningImportedGraphPreservesAssetAndResourceIdentity()
        {
            if (!VmAutomationVFXReflection.IsAvailable)
                Assert.Ignore("VFX Graph is not installed in this test project.");

            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/VFX Session Test.vfx");
            try
            {
                Type utility = VmAutomationVFXReflection.RequireType(
                    VmAutomationVFXReflection.AssetUtilityTypeName);
                VmAutomationVFXReflection.Invoke(utility, "CreateNewAsset", path);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                byte[] before = File.ReadAllBytes(path);
                string guid = AssetDatabase.AssetPathToGUID(path);

                Assert.That(VmAutomationVFXGraphSession.TryOpen(path, out var first,
                    out var firstError), Is.True, firstError?.ToString());
                Assert.That(VmAutomationVFXGraphSession.TryOpen(path, out var second,
                    out var secondError), Is.True, secondError?.ToString());
                Assert.That(first.Graph, Is.Not.Null);
                Assert.That(second.Graph, Is.SameAs(first.Graph));
                Assert.That(second.Resource, Is.SameAs(first.Resource));
                Assert.That(second.Asset, Is.SameAs(first.Asset));
                Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(before));
            }
            finally
            {
                AssetDatabase.DeleteAsset(path);
            }
        }
    }
}
