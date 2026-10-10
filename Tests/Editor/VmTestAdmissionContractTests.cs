using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    [Category("TestAdmissionContract")]
    public sealed class VmTestAdmissionContractTests
    {
        [Test]
        public void DirtyLoadedSceneIsRejectedBeforeNativeTestAdmission()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            scene.name = "AutomationDirtySceneAdmissionFixture";
            try
            {
                EditorSceneManager.MarkSceneDirty(scene);
                Assert.That(VmAutomationTestRunnerCommands.TryValidateLoadedScenesSaved(out var error), Is.False);
                Assert.That(error, Does.Contain(scene.name));
                Assert.That(error, Does.Contain("Save modified scenes"));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void TestResultPollingAcceptsItsPrivateCapability()
        {
            var schema = VmAutomationToolInputSchemaCatalog.Get("testing/get-job");
            Assert.That(schema, Is.Not.Null);
            var properties = (Dictionary<string, object>)schema["properties"];
            Assert.That(properties.ContainsKey("jobAccessToken"), Is.True);
            Assert.That(((Dictionary<string, object>)properties["jobAccessToken"])["type"], Is.EqualTo("string"));
        }

        [Test]
        public void FrameStep_RequiresAnAdoptableDurableJob()
        {
            Assert.That(VmAutomationPlayModeJobRunner.RequiresDurableTransition("step"), Is.True);
            Assert.That(VmAutomationGeneratedRouteContracts.TryGetOutput("editor/play-mode", out var schema), Is.True);
            var variants = ((List<object>)schema["oneOf"]).Cast<Dictionary<string, object>>().ToList();
            var job = variants.Single(variant =>
                ((Dictionary<string, object>)variant["properties"]).ContainsKey("jobAccessToken"));
            Assert.That(((Dictionary<string, object>)job["properties"]).ContainsKey("pollRoute"), Is.True);
        }

        [TestCase(0)]
        [TestCase(301)]
        public void FrameStep_RejectsInvalidIntervalsWithoutQueuing(int frames)
        {
            var result = (Dictionary<string, object>)VmAutomationPlayModeJobRunner.Start(
                new Dictionary<string, object> { { "action", "step" }, { "frames", frames } });
            Assert.That(result["success"], Is.False);
            Assert.That(result["errorCode"], Is.EqualTo("invalid_arguments"));
            Assert.That(result.ContainsKey("jobId"), Is.False);
        }

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
