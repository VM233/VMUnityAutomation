using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmPlayerBuildDiagnosticsTests
    {
        private static BuildPlayerOptions Options => new BuildPlayerOptions
        { locationPathName = "Library/Rejected.exe", target = BuildTarget.StandaloneWindows64 };

        [Test]
        public void NullNativeReportPreservesOriginalDiagnosticAndRequestedContext()
        {
            using var capture = new VmPlayerBuildDiagnostics();
            const string message = "Invalid build path: Library/Rejected.exe is an internal Unity work directory.";
            LogAssert.Expect(LogType.Error, message);
            Debug.LogError(message);
            var result = capture.Complete(null, Options);
            Assert.That(result["success"], Is.False);
            Assert.That(result["errorCode"], Is.EqualTo("build_report_unavailable"));
            Assert.That(result["error"], Is.EqualTo(message));
            Assert.That(result["requestedOutputPath"], Is.EqualTo(Options.locationPathName));
            Assert.That(result.ContainsKey("buildGUID"), Is.False);
            Assert.That(result.ContainsKey("developmentBuild"), Is.False);
            var native = (Dictionary<string, object>)result["nativeDiagnostics"];
            Assert.That(native["complete"], Is.True);
            Assert.That(native["observedErrors"], Is.EqualTo(1));
            var entries = (List<Dictionary<string, object>>)native["entries"];
            Assert.That(entries[0]["message"], Is.EqualTo(message));
            Assert.That(entries[0]["stackTrace"], Is.Not.Null);
        }

        [Test]
        public void NoReportWithoutNativeErrorNamesTheActualBoundary()
        {
            using var capture = new VmPlayerBuildDiagnostics();
            var result = capture.Complete(null, Options);
            Assert.That(result["success"], Is.False);
            Assert.That(result["error"], Does.Contain("returned no BuildReport"));
            Assert.That(result["error"], Does.Not.Contain("Object reference"));
        }

        [Test]
        public void NativeExceptionRetainsItsTypeAndPublicationReleasesSubscription()
        {
            using var capture = new VmPlayerBuildDiagnostics();
            var result = capture.Complete(null, Options, new ArgumentException("Native target failure"));
            LogAssert.Expect(LogType.Error, "After build scope");
            Debug.LogError("After build scope");
            Assert.That(result["errorCode"], Is.EqualTo("build_pipeline_exception"));
            Assert.That(result.ContainsKey("buildGUID"), Is.False);
            Assert.That(result.ContainsKey("developmentBuild"), Is.False);
            Assert.That(result["error"], Does.Contain("System.ArgumentException"));
            var native = (Dictionary<string, object>)result["nativeDiagnostics"];
            Assert.That(native["observedErrors"], Is.EqualTo(0));
            Assert.That(((List<Dictionary<string, object>>)native["entries"]).Count, Is.Zero);
        }

        [Test]
        public void OversizedNativeDiagnosticIsExplicitlyIncompleteAndNeverPasses()
        {
            using var capture = new VmPlayerBuildDiagnostics();
            string oversized = new string('x', VmPlayerBuildDiagnostics.MaximumCharacters + 1);
            LogAssert.Expect(LogType.Error, oversized);
            Debug.LogError(oversized);
            var result = capture.Complete(null, Options);
            var native = (Dictionary<string, object>)result["nativeDiagnostics"];
            Assert.That(result["success"], Is.False);
            Assert.That(native["complete"], Is.False);
            Assert.That(native["observedErrors"], Is.EqualTo(1));
            Assert.That(native["retainedCharacters"], Is.EqualTo(0));
        }
    }
}
