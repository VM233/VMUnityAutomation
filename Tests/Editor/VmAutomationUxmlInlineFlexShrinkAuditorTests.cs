#if UNITY_EDITOR
using System.Linq;
using NUnit.Framework;

namespace VMUnityAutomation.Editor.Tests
{
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
    }
}
#endif
