using System.Linq;
using NUnit.Framework;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    internal sealed class VmAutomationCatalogAvailabilityTests
    {
        [Test]
        public void Addressables_ReadinessChangesReplaceThePublishedRouteSet()
        {
            int current = VmAutomationCapabilityRegistry.AvailabilityMask;
            const int addressables = 1 << 3;
            try
            {
                var absent = VmAutomationCatalog.AdoptAvailability(current & ~addressables);
                Assert.That(absent.Any(tool => tool["route"].ToString() == "addressables/info"), Is.False);
                var present = VmAutomationCatalog.AdoptAvailability(current | addressables);
                Assert.That(present.Single(tool => tool["route"].ToString() == "addressables/info")["toolName"],
                    Is.EqualTo("vm_auto_addressables_info"));
                Assert.That(VmAutomationCatalog.AdoptAvailability(current | addressables), Is.SameAs(present));
                var removed = VmAutomationCatalog.AdoptAvailability(current & ~addressables);
                Assert.That(removed.Any(tool => tool["route"].ToString().StartsWith("addressables/")), Is.False);
                Assert.That(present, Is.Not.SameAs(absent));
            }
            finally
            {
                VmAutomationCatalog.AdoptAvailability(current);
            }
        }
    }
}
