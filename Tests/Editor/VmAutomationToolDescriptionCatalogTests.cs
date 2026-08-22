using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmAutomationToolDescriptionCatalogTests
    {
        [Test]
        public void ComponentMutationDescriptionsPublishSelectionAndSaveBoundary()
        {
            Assert.That(
                VmAutomationToolDescriptionCatalog.Get("component/add"),
                Is.EqualTo(
                    "Add one component to an exact loaded-scene GameObject " +
                    "selected by hierarchy path or instance ID. This mutates " +
                    "the loaded scene; call scene/save explicitly to persist " +
                    "the change."));
            Assert.That(
                VmAutomationToolDescriptionCatalog.Get("component/remove"),
                Is.EqualTo(
                    "Remove one indexed component from an exact loaded-scene " +
                    "GameObject selected by hierarchy path or instance ID. " +
                    "This mutates the loaded scene; call scene/save explicitly " +
                    "to persist the change."));
        }

        [TestCase("component/add", "Add components on loaded GameObjects.")]
        [TestCase(
            "component/remove",
            "Remove selected components on loaded GameObjects.")]
        [TestCase("editorprefs/get", "Read Unity Editor preferences.")]
        [TestCase("selection/set", "Set the Unity Editor selection.")]
        [TestCase("script/update", "Update C# script assets.")]
        [TestCase(
            "scenario/activate",
            "Activate Multiplayer Play Mode scenarios.")]
        public void ExactActionsUseDirectObjectGrammar(
            string route,
            string expected)
        {
            Assert.That(
                VmAutomationToolDescriptionComposer.Compose(route),
                Is.EqualTo(expected));
        }

        [Test]
        public void PackageTestGuidanceMatchesOwnedSelectionPolicy()
        {
            string description = VmAutomationToolDescriptionCatalog.Get(
                "testing/run-package-tests");
            Assert.That(description,
                Does.Contain(
                    VmAutomationPackageTestCommands
                        .DefaultPackageSmokeCategory));
            Assert.That(description,
                Does.Contain(
                    VmAutomationPackageTestCommands
                        .FullPackageRegressionCategory));

            Dictionary<string, object> genericSchema =
                VmAutomationToolInputSchemaCatalog.Get(
                    "testing/run-tests");
            Assert.That(
                GetPropertyDescription(genericSchema, "categories"),
                Is.EqualTo("Optional test categories."));

            Dictionary<string, object> packageSchema =
                VmAutomationToolInputSchemaCatalog.Get(
                    "testing/run-package-tests");
            string categoryDescription = GetPropertyDescription(
                packageSchema, "categories");
            Assert.That(categoryDescription,
                Does.Contain(
                    VmAutomationPackageTestCommands
                        .DefaultPackageSmokeCategory));
            Assert.That(categoryDescription,
                Does.Contain(
                    VmAutomationPackageTestCommands
                        .FullPackageRegressionCategory));
        }

        [Test]
        public void PackageSelectionCategoriesCoverEveryTestFixture()
        {
            IEnumerable<System.Type> testFixtures = GetType().Assembly
                .GetTypes()
                .Where(type =>
                    type.IsClass &&
                    type.IsAbstract == false &&
                    string.Equals(type.Namespace, GetType().Namespace) &&
                    type.Name.EndsWith(
                        "Tests", System.StringComparison.Ordinal));

            foreach (System.Type fixture in testFixtures)
            {
                string[] categories = fixture
                    .GetCustomAttributes<CategoryAttribute>()
                    .Select(attribute => attribute.Name)
                    .ToArray();
                Assert.That(categories,
                    Does.Contain(
                        VmAutomationPackageTestCommands
                            .DefaultPackageSmokeCategory),
                    $"Fixture '{fixture.FullName}' is absent from the " +
                    "default package-smoke selection.");
                Assert.That(categories,
                    Does.Contain(
                        VmAutomationPackageTestCommands
                            .FullPackageRegressionCategory),
                    $"Fixture '{fixture.FullName}' is absent from the " +
                    "full package-regression selection.");
            }
        }

        private static string GetPropertyDescription(
            Dictionary<string, object> schema,
            string propertyName)
        {
            var properties =
                (Dictionary<string, object>)schema["properties"];
            var property =
                (Dictionary<string, object>)properties[propertyName];
            return (string)property["description"];
        }
    }
}
