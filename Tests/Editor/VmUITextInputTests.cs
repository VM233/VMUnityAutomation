using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmUITextInputTests
    {
        [Test]
        public void TextInputRequiresPlayModeBeforeLookingUpTheTarget()
        {
            Assert.That(EditorApplication.isPlaying, Is.False);
            var result = (Dictionary<string, object>)VmAutomationUICommands.TypeUIText(
                new Dictionary<string, object> { { "path", "nonexistent" }, { "text", "test" } });
            Assert.That(result["success"], Is.False);
            Assert.That(result["errorCode"], Is.EqualTo("play_mode_required"));
        }

        [Test]
        public void TextInputHasAnExactRuntimeMutationContract()
        {
            var profile = VmAutomationToolProfileCatalog.Get("ui/type-text");
            Assert.That(profile.RequiresPlayMode, Is.True);
            Assert.That(profile.MutatesRuntime, Is.True);
            Assert.That(VmAutomationGeneratedRouteContracts.TryGetInput("ui/type-text", out var input), Is.True);
            Assert.That(input["additionalProperties"], Is.False);
            Assert.That(input["required"], Does.Contain("path").And.Contain("text"));
            Assert.That(VmAutomationGeneratedRouteContracts.TryGetOutput("ui/type-text", out var output), Is.True);
            Assert.That(output["additionalProperties"], Is.False);
            Assert.That(output["required"], Does.Contain("text").And.Contain("isFocused"));
        }
    }
}
