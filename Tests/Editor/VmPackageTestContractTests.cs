using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    internal sealed class VmPackageTestContractTests
    {
        private static readonly string[] JobFields =
        {
            "jobId",
            "jobAccessToken",
            "jobType",
            "status",
            "pollRoute",
            "pollArgs",
            "packageName",
            "mode",
            "assemblies",
            "startedAt",
            "updatedAt",
            "compilationDiagnostics",
            "tags",
            "testJobId",
            "error",
            "testResult",
        };

        [Test]
        public void PackageTestOutputSchemas_DeclareDurableJobFields()
        {
            Assert.That(
                VmAutomationGeneratedRouteContracts.TryGetOutput(
                    "testing/run-package-tests",
                    out Dictionary<string, object> runSchema),
                Is.True);
            var variants = (List<object>)runSchema["oneOf"];
            Dictionary<string, object> runJobSchema = variants
                .Cast<Dictionary<string, object>>()
                .Single(candidate =>
                    ((Dictionary<string, object>)candidate["properties"])
                    .ContainsKey("jobId"));
            AssertJobFields(runJobSchema);

            Assert.That(
                VmAutomationGeneratedRouteContracts.TryGetOutput(
                    "testing/get-package-job",
                    out Dictionary<string, object> statusSchema),
                Is.True);
            AssertJobFields(statusSchema);
            var statusProperties =
                (Dictionary<string, object>)statusSchema["properties"];
            Assert.That(statusProperties.ContainsKey("cleared"), Is.True);
        }

        private static void AssertJobFields(
            Dictionary<string, object> schema)
        {
            Assert.That(schema["additionalProperties"], Is.False);
            var properties =
                (Dictionary<string, object>)schema["properties"];
            foreach (string field in JobFields)
            {
                Assert.That(properties.ContainsKey(field), Is.True,
                    $"Package-test job field '{field}' is absent from its " +
                    "closed output schema.");
            }
        }
    }
}
