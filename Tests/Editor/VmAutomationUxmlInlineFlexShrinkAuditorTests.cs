#if UNITY_EDITOR
using System;
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
        public void SerializedBlobReferencesDoNotAllocateCandidateStrings()
        {
            var index = new VmAutomationUxmlLayoutModels.UxmlElementNameReferenceIndex(true);
            index.AddDefinition("BeforeBlob");
            index.AddDefinition("AfterBlob");
            index.AddDefinition("QuotedAfterBlob");
            string blob = new string('a', 2 * 1024 * 1024);
            string text = "path: BeforeBlob\n  bytes: " + blob + "\n" +
                          blob + "\n  quote: \"" + blob + "\"\n" +
                          "names:\n  - AfterBlob\npath: 'QuotedAfterBlob'\n";
            VmAutomationUxmlLayoutContractIndexer.IndexTextNameReferences(
                "  field: unrelated\nvar value = \"unrelated\";\n", index,
                includeYamlScalars: true);
            long before = GC.GetAllocatedBytesForCurrentThread();
            VmAutomationUxmlLayoutContractIndexer.IndexTextNameReferences(text,
                index, includeYamlScalars: true);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(index.IsReferenced("BeforeBlob"), Is.True);
            Assert.That(index.IsReferenced("AfterBlob"), Is.True);
            Assert.That(index.IsReferenced("QuotedAfterBlob"), Is.True);
            Assert.That(allocated, Is.Zero,
                "The name index must consume source ranges without materializing blobs or candidates.");
        }

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

        [Test]
        public void AggregateUssStyleCasesPass()
        {
            var selfTests = VmAutomationUssStyleAuditor.RunSelfTests();
            var failedCases = ((IEnumerable<Dictionary<string, object>>)selfTests["cases"])
                .Where(testCase => (bool)testCase["passed"] == false)
                .Select(testCase => (string)testCase["name"])
                .ToArray();

            Assert.That(failedCases, Is.Empty,
                "Failed aggregate USS style audit cases: " +
                string.Join(", ", failedCases));
        }
    }
}
#endif
