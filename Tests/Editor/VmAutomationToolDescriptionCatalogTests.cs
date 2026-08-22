using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

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
        [TestCase("scene/save", "Save Unity scenes.")]
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

        [Test]
        public void ComponentPropertiesCanDiscoverHiddenNativeFields()
        {
            var gameObject = new GameObject(
                "VM Automation Hidden Component Property Test");
            try
            {
                gameObject.AddComponent<SortingGroup>();
                var arguments = new Dictionary<string, object>
                {
                    { "path", gameObject.name },
                    {
                        "componentType",
                        "UnityEngine.Rendering.SortingGroup"
                    },
                    { "includeHidden", true },
                };
                var response =
                    (Dictionary<string, object>)
                    VmAutomationComponentCommands.GetProperties(arguments);
                var properties =
                    (List<Dictionary<string, object>>)
                    response["properties"];

                Assert.That(properties.Select(property =>
                        property["propertyPath"]),
                    Does.Contain("m_SortingLayerID"));
                Assert.That(properties.Select(property =>
                        property["propertyPath"]),
                    Does.Contain("m_SortingOrder"));
                Assert.That(properties.Select(property =>
                        property["propertyPath"]),
                    Does.Contain("m_SortAtRoot"));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void ComponentPropertyContractsPublishHiddenDiscovery()
        {
            Dictionary<string, object> inputSchema =
                VmAutomationToolInputSchemaCatalog.Get(
                    "component/get-properties");
            var inputProperties =
                (Dictionary<string, object>)inputSchema["properties"];
            Assert.That(inputProperties.ContainsKey("includeHidden"),
                Is.True);

            Assert.That(
                VmAutomationGeneratedRouteContracts.TryGetOutput(
                    "component/get-properties",
                    out Dictionary<string, object> outputSchema),
                Is.True);
            var outputProperties =
                (Dictionary<string, object>)outputSchema["properties"];
            var propertyArray =
                (Dictionary<string, object>)outputProperties["properties"];
            var propertyItem =
                (Dictionary<string, object>)propertyArray["items"];
            var propertyFields =
                (Dictionary<string, object>)propertyItem["properties"];
            Assert.That(propertyFields.ContainsKey("propertyPath"),
                Is.True);
            Assert.That(
                (Dictionary<string, object>)propertyFields["value"],
                Contains.Key("$ref"));
        }

        [Test]
        public void SceneSaveGuidancePublishesInPlaceAndSaveAsBoundaries()
        {
            string description = VmAutomationToolDescriptionCatalog.Get(
                "scene/save");
            Assert.That(description, Does.Contain("active loaded scene"));
            Assert.That(description, Does.Contain("Assets/*.unity"));
            Assert.That(description, Does.Contain("overwrite"));

            Dictionary<string, object> schema =
                VmAutomationToolInputSchemaCatalog.Get("scene/save");
            var properties =
                (Dictionary<string, object>)schema["properties"];
            Assert.That(
                ((Dictionary<string, object>)properties["path"])
                ["description"],
                Does.Contain("omit it to save the active scene in place"));
            Assert.That(
                ((Dictionary<string, object>)properties["overwrite"])
                ["description"],
                Does.Contain("Defaults to false"));
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
