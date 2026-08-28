#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmAutomationPackageResolutionTests
    {
        private const string ExpectedRevision =
            "cce4342aa5c4ae991589ef1463703e388d9ca1d1";
        private const string StaleRevision =
            "a4722c0379c6d97f7b0cde674f843743e2a0ad21";

        [Test]
        public void RegisteredIdentifierCannotMaskStaleResolvedFingerprint()
        {
            const string identifier =
                "https://github.com/VM233/VMUnityAutomation.git#" +
                ExpectedRevision;

            Assert.That(
                VmAutomationPackageManagerCommands.ResolvedGitRevisionMatches(
                    identifier, StaleRevision, ExpectedRevision),
                Is.False);
            Assert.That(
                VmAutomationPackageManagerCommands.ResolvedGitRevisionMatches(
                    identifier, ExpectedRevision, ExpectedRevision),
                Is.True);
        }

        [TestCase("{\"_fingerprint\":\"abc123\"}", "abc123")]
        [TestCase("{\"name\":\"com.example.package\"}", "")]
        [TestCase("not-json", "")]
        public void ResolvedPackageFingerprintIsReadConservatively(
            string packageJson, string expected)
        {
            Assert.That(
                VmAutomationPackageManagerCommands.ReadPackageFingerprint(packageJson),
                Is.EqualTo(expected));
        }

        [TestCase(false, false)]
        [TestCase(true, true)]
        public void ModifiedManifestRequiresPackageTestAssemblies(
            bool packageTestAssembliesAvailable, bool expected)
        {
            Assert.That(
                VmAutomationPackageTestCommands.IsManifestResolveProductAdopted(
                    ManifestResolveTarget.Modified,
                    packageTestAssembliesAvailable,
                    resolveIssued: true,
                    assemblyReloadObserved: true,
                    editorStable: true),
                Is.EqualTo(expected));
        }

        [Test]
        public void OriginalManifestAcceptsRemovedPackageTestAssemblies()
        {
            Assert.That(
                VmAutomationPackageTestCommands.IsManifestResolveProductAdopted(
                    ManifestResolveTarget.Original,
                    packageTestAssembliesAvailable: false,
                    resolveIssued: false,
                    assemblyReloadObserved: false,
                    editorStable: false),
                Is.True);
        }

        [TestCase(false, false, true)]
        [TestCase(true, false, true)]
        [TestCase(true, true, false)]
        public void OriginalManifestRejectsStickyAssemblyWithoutCompleteAdoptionWitness(
            bool resolveIssued, bool assemblyReloadObserved, bool editorStable)
        {
            Assert.That(
                VmAutomationPackageTestCommands.IsManifestResolveProductAdopted(
                    ManifestResolveTarget.Original,
                    packageTestAssembliesAvailable: true,
                    resolveIssued: resolveIssued,
                    assemblyReloadObserved: assemblyReloadObserved,
                    editorStable: editorStable),
                Is.False);
        }

        [Test]
        public void OriginalManifestAcceptsStickyAssemblyAfterResolveReloadAndStableEditor()
        {
            Assert.That(
                VmAutomationPackageTestCommands.IsManifestResolveProductAdopted(
                    ManifestResolveTarget.Original,
                    packageTestAssembliesAvailable: true,
                    resolveIssued: true,
                    assemblyReloadObserved: true,
                    editorStable: true),
                Is.True);
        }

        [Test]
        public void ManifestResolveReloadWitnessSurvivesSerializationAndResetsOnCompletion()
        {
            var workflow = new PackageTestWorkflow
            {
                WorkflowId = "package-resolve-test",
                State = "restoring",
                Mode = "EditMode",
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            workflow.BeginManifestResolve(ManifestResolveTarget.Original);
            workflow.MarkManifestResolveIssued();
            workflow.MarkManifestResolveAssemblyReloadObserved();

            Dictionary<string, object> serialized = workflow.ToDictionary();
            PackageTestWorkflow restored =
                PackageTestWorkflow.FromDictionary(serialized);

            Assert.That(restored.ManifestResolveIssued, Is.True);
            Assert.That(restored.ManifestResolveActivityObserved, Is.True);
            Assert.That(restored.ManifestResolveAssemblyReloadObserved, Is.True);

            restored.CompleteManifestResolve(ManifestResolveTarget.Original);

            Assert.That(restored.ManifestResolve, Is.EqualTo(ManifestResolveTarget.None));
            Assert.That(restored.ManifestResolveIssued, Is.False);
            Assert.That(restored.ManifestResolveActivityObserved, Is.False);
            Assert.That(restored.ManifestResolveAssemblyReloadObserved, Is.False);
        }
    }
}
#endif
