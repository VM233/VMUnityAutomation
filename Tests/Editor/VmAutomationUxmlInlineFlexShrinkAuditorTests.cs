#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmAutomationUxmlInlineFlexShrinkAuditorTests
    {
        [Test]
        public void DeterministicRuleCasesPass()
        {
            var failedCases = VmAutomationUxmlInlineFlexShrinkAuditor.RunSelfTests()
                .Where(testCase => (bool)testCase["passed"] == false)
                .Select(testCase => (string)testCase["name"])
                .ToArray();

            Assert.That(failedCases, Is.Empty,
                "Failed inline flex-shrink audit cases: " +
                string.Join(", ", failedCases));
        }

        [Test]
        public void AggregateUxmlLayoutCasesPass()
        {
            var selfTests = VmAutomationUxmlLayoutAuditSelfTests.RunSelfTests();
            var failedCases = ((IEnumerable<Dictionary<string, object>>)selfTests["cases"])
                .Where(testCase => (bool)testCase["passed"] == false)
                .Select(testCase => (string)testCase["name"])
                .ToArray();

            Assert.That(failedCases, Is.Empty,
                "Failed aggregate UXML layout audit cases: " +
                string.Join(", ", failedCases));
        }
    }
}
#endif
