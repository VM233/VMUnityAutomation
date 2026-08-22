using NUnit.Framework;

namespace VMUnityAutomation.Editor.Tests
{
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
    }
}
