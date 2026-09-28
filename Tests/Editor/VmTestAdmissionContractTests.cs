using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category("TestAdmissionContract")]
    public sealed class VmTestAdmissionContractTests
    {
        [Test]
        public void StartedTestJob_DeclaresItsPrivateCapabilityInTheClosedCatalogSchema()
        {
            Assert.That(VmAutomationGeneratedRouteContracts.TryGetOutput("testing/run-tests", out var schema), Is.True);
            var variants = ((List<object>)schema["oneOf"]).Cast<Dictionary<string, object>>().ToList();
            Assert.That(variants.Count, Is.EqualTo(3));
            var started = variants.Single(variant =>
                ((Dictionary<string, object>)variant["properties"]).ContainsKey("jobType"));
            var properties = (Dictionary<string, object>)started["properties"];
            var observedAdmission = new Dictionary<string, object>
            {
                {"jobId", "fixture-job"}, {"jobType", "unity-test"},
                {"status", "running"}, {"mode", "EditMode"}, {"jobAccessToken", "private-fixture-token"},
            };
            Assert.That(observedAdmission.Keys.All(properties.ContainsKey), Is.True,
                "The observed production admission must fit its closed output schema.");
            Assert.That(((Dictionary<string, object>)properties["jobAccessToken"])["type"], Is.EqualTo("string"));
            Assert.That(started["required"], Does.Contain("jobAccessToken"));
            Assert.That(started["additionalProperties"], Is.False);
            Assert.That(variants.Where(variant => !ReferenceEquals(variant, started)).All(variant =>
                !((Dictionary<string, object>)variant["properties"]).ContainsKey("jobAccessToken")), Is.True);
        }
    }
}
