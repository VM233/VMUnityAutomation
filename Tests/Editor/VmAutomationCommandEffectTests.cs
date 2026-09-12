using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmAutomationCommandEffectTests
    {
        [Test]
        public void BuildContractPublishesActualEffectsAndModeRequirement()
        {
            Assert.That(VmAutomationCatalog.TryGetTool("build/start", true, out var tool), Is.True);
            Assert.That(Strings(tool["sideEffects"]), Is.EquivalentTo(new[]
            {
                "writesBuildOutput", "startsProcesses", "reloadsDomain"
            }));
            Assert.That(Strings(tool["preconditions"]), Does.Contain("stableEditMode"));
            var schema = (Dictionary<string, object>)tool["inputSchema"];
            Assert.That(Strings(schema["required"]), Does.Contain("expectedProjectPath"));
            Assert.That(VmAutomationCatalog.IsRouteReadOnly("build/start"), Is.False);
        }

        [Test]
        public void OptionalBuildHistoryCleanupRequiresBinding()
        {
            Assert.That(VmAutomationCatalog.TryGetTool("build/get-job", true, out var tool), Is.True);
            Assert.That(Strings(tool["sideEffects"]), Does.Contain("writesJobHistory"));
            Assert.That(VmAutomationCatalog.RouteRequiresTargetBinding("build/get-job"), Is.True);
            Assert.That(VmAutomationCatalog.IsRouteReadOnly("build/get-job"), Is.False);
            Assert.That(VmAutomationCatalog.IsRouteReadOnly("editor/state"), Is.True);
        }

        [Test]
        public void EffectProductDoesNotAdoptSubsequentInputMutation()
        {
            var effects = new[] { "writesBuildOutput" };
            var profile = VmAutomationToolProfile.Create(sideEffects: effects);
            var clone = profile.Clone();
            effects[0] = "read";
            Assert.That(profile.SideEffects, Is.EqualTo(new[] { "writesBuildOutput" }));
            Assert.That(clone.SideEffects, Is.EqualTo(profile.SideEffects));
        }

        [Test]
        public void EveryBuiltInOwnerDeclaresItsEffects()
        {
            var routes = VmAutomationBuiltInRouteDescriptorRegistry.Routes.ToArray();
            Assert.That(routes.Length, Is.LessThanOrEqualTo(1024));
            foreach (string route in routes)
            {
                var profile = VmAutomationToolProfileCatalog.Get(route);
                Assert.That(VmAutomationContractMetadata.BuildSideEffects(profile.SideEffects,
                    profile.ReadOnly, profile.MutatesAssets, profile.MutatesRuntime, profile.MayReloadDomain),
                    Is.Not.Empty, route);
            }
        }

        private static string[] Strings(object value) => ((IEnumerable)value).Cast<object>()
            .Select(item => item.ToString()).ToArray();
    }
}
