using System.Collections.Generic;
using NUnit.Framework;

namespace VMUnityAutomation.Editor.Tests
{
    public sealed class VmAutomationProviderProfileOwnershipTests
    {
        [Test]
        public void CoreCoverageRejectsMissingCoreProfilesWithoutRequiringExternalCopies()
        {
            var profile = VmAutomationToolProfile.Create(readOnly: true);
            var contract = new VmAutomationBuiltInRouteContract("ui/owned", "com.example.ui",
                "Provider-owned review", profile,
                new Dictionary<string, object>(), new Dictionary<string, object>(),
                new[] { "invalid_arguments" });
            var descriptors = new[]
            {
                VmAutomationBuiltInRouteDescriptor.CreateImmediate("core/covered", _ => null),
                VmAutomationBuiltInRouteDescriptor.CreateImmediate("core/missing", _ => null),
                VmAutomationBuiltInRouteDescriptor.CreateImmediate("ui/owned", contract, _ => null)
            };
            var profiles = new Dictionary<string, VmAutomationToolProfile> { { "core/covered", profile } };
            Assert.That(VmAutomationToolProfileCatalog.FindMissingProfiles(descriptors, profiles),
                Is.EqualTo(new[] { "core/missing" }));
            Assert.That(descriptors[2].Profile, Is.SameAs(profile));
        }
    }
}
