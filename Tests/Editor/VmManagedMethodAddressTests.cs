using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmManagedMethodAddressTests
    {
        [Test]
        public void RuntimeQueryPublishesOnlyCurrentIdentity()
        {
#if UNITY_EDITOR_WIN
            var result = ObserveRuntime();
            Assert.That(result["managedDomainId"], Is.EqualTo(AppDomain.CurrentDomain.Id));
            Assert.That(result["runtimeId"], Is.EqualTo(ObserveRuntime()["runtimeId"]));
            Assert.That((IEnumerable<object>)result["resolvedMethods"], Is.Empty);
#else
            var result = ObserveRuntime();
            Assert.That(result["errorCode"], Is.EqualTo("capability_unavailable"));
#endif
        }

        [Test]
        public void RecycledDomainNumberIsRejectedAsACaptureIdentity()
        {
#if UNITY_EDITOR_WIN
            var current = ObserveRuntime();
            using var process = System.Diagnostics.Process.GetCurrentProcess();
            string recycled = string.Format(CultureInfo.InvariantCulture, "{0}:{1}:{2}",
                process.Id, process.StartTime.ToUniversalTime().Ticks, AppDomain.CurrentDomain.Id);
            var schema = VmAutomationToolInputSchemaCatalog.Get("profiler/managed-runtime");
            Assert.That(VmAutomationInputValidator.TryValidate(new Dictionary<string, object>
            {
                { "expectedRuntimeId", recycled },
                { "methodAddresses", new object[] { "0x0000000000000001" } },
            }, schema, out string code, out _, out _), Is.False);
            Assert.That(code, Is.EqualTo("invalid_arguments"));
            Assert.That(current["runtimeId"], Is.Not.EqualTo(recycled));
#else
            Assert.That(ObserveRuntime()["errorCode"], Is.EqualTo("capability_unavailable"));
#endif
        }

#if UNITY_EDITOR_WIN
        [Test]
        public async Task ExecutorContextDoesNotChangeTheAuthoredRequestShape()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            var identity = await VmAutomationExecutor.ExecuteAsync("profiler/managed-runtime",
                expectedProjectPath: projectRoot);
            Assert.That(identity.Ok, Is.True, identity.Error?.Message);
            var runtime = (Dictionary<string, object>)identity.Result;
            var resolution = await VmAutomationExecutor.ExecuteAsync("profiler/managed-runtime",
                new Dictionary<string, object>
                {
                    { "expectedRuntimeId", runtime["runtimeId"] },
                    { "methodAddresses", new object[] { "0x0000000000000001" } },
                }, expectedProjectPath: projectRoot);
            Assert.That(resolution.Ok, Is.True, resolution.Error?.Message);
            var stale = await VmAutomationExecutor.ExecuteAsync("profiler/managed-runtime",
                new Dictionary<string, object>
                {
                    { "expectedRuntimeId", "0:0:00000000000000000000000000000000" },
                    { "methodAddresses", new object[] { "0x0000000000000001" } },
                }, expectedProjectPath: projectRoot);
            Assert.That(stale.Ok, Is.False);
            Assert.That(stale.Error.Code, Is.EqualTo("managed_runtime_changed"));
        }

        [Test]
        public void CurrentJitAddressResolvesTheActualMethodAndRange()
        {
            Assert.That(KnownMethod(7), Is.EqualTo(12));
            MethodInfo method = typeof(VmManagedMethodAddressTests).GetMethod(
                nameof(KnownMethod), BindingFlags.Static | BindingFlags.NonPublic);
            ulong instruction = unchecked((ulong)method.MethodHandle.GetFunctionPointer().ToInt64());
            var result = Resolve((string)ObserveRuntime()["runtimeId"], "0x" + instruction.ToString("x16"));
            var entry = (Dictionary<string, object>)((IEnumerable<object>)result["resolvedMethods"]).Single();
            Assert.That(entry["resolved"], Is.True);
            Assert.That(entry["className"], Is.EqualTo(nameof(VmManagedMethodAddressTests)));
            Assert.That(entry["methodName"], Is.EqualTo(nameof(KnownMethod)));
            Assert.That(entry["metadataToken"], Is.EqualTo("0x" + method.MetadataToken.ToString("x8")));
            ulong start = ulong.Parse(((string)entry["codeStart"]).Substring(2), NumberStyles.HexNumber);
            Assert.That(instruction, Is.GreaterThanOrEqualTo(start));
            Assert.That(instruction, Is.LessThan(start + (ulong)(int)entry["codeSize"]));
        }

        [Test]
        public void NativeAddressRemainsUnresolvedAndStaleRuntimeIsRejected()
        {
            var current = Resolve((string)ObserveRuntime()["runtimeId"], "0x0000000000000001");
            var entry = (Dictionary<string, object>)((IEnumerable<object>)current["resolvedMethods"]).Single();
            Assert.That(entry["resolved"], Is.False);
            Assert.That(entry["methodName"], Is.Null);
            var stale = Resolve("0:0:00000000000000000000000000000000", "0x0000000000000001");
            Assert.That(stale["success"], Is.False);
            Assert.That(stale["errorCode"], Is.EqualTo("managed_runtime_changed"));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static int KnownMethod(int value) => value + 5;

        private static Dictionary<string, object> Resolve(string runtimeId, string address) =>
            (Dictionary<string, object>)VmAutomationManagedMethodCommands.ReadManagedRuntime(new()
            {
                { "expectedRuntimeId", runtimeId }, { "methodAddresses", new object[] { address } },
            });
#endif

        [Test]
        public void ContractRequiresAnExactLifetimeForBoundedAddressQueries()
        {
            var schema = VmAutomationToolInputSchemaCatalog.Get("profiler/managed-runtime");
            Assert.That(VmAutomationInputValidator.TryValidate(new Dictionary<string, object>(),
                schema, out _, out _, out _), Is.True);
            foreach (var invalid in new[]
                     {
                         new Dictionary<string, object> { { "methodAddresses", new object[] { "0x0000000000000001" } } },
                         new Dictionary<string, object> { { "expectedRuntimeId", "1:2:00000000000000000000000000000000" }, { "methodAddresses", Array.Empty<object>() } },
                         new Dictionary<string, object> { { "expectedRuntimeId", "1:2:00000000000000000000000000000000" }, { "methodAddresses", new object[] { "0x0000000000000001", "0x0000000000000001" } } },
                         new Dictionary<string, object> { { "expectedRuntimeId", "1:2:00000000000000000000000000000000" }, { "methodAddresses", new object[] { "0xABCDEFABCDEFABCD" } } },
                         new Dictionary<string, object> { { "expectedRuntimeId", "1:2:00000000000000000000000000000000" }, { "methodAddresses", Enumerable.Range(1, 17).Select(value => (object)("0x" + value.ToString("x16"))).ToArray() } },
                     })
                Assert.That(VmAutomationInputValidator.TryValidate(invalid, schema,
                    out _, out _, out _), Is.False);
        }

        private static Dictionary<string, object> ObserveRuntime() =>
            (Dictionary<string, object>)VmAutomationManagedMethodCommands.ReadManagedRuntime(new());
    }
}
