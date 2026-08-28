#if UNITY_EDITOR
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
    }
}
#endif
