using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmRenderDocCaptureTests
    {
        [Test]
        public void SupportedEditorPublishesNativeGraphicsDeviceRecreation()
        {
            var method = typeof(ShaderUtil).GetMethod("RecreateGfxDevice", BindingFlags.Static | BindingFlags.NonPublic,
                null, Type.EmptyTypes, null);
            Assert.That(method, Is.Not.Null, "Loading a debugger DLL without recreating the existing device cannot capture it.");
            Assert.That(method.ReturnType, Is.EqualTo(typeof(void)));
            Assert.That(VmAutomationCatalog.TryGetTool("profiler/renderdoc-load", true, out var tool), Is.True);
            var output = (Dictionary<string, object>)tool["outputSchema"];
            var fields = (Dictionary<string, object>)output["properties"];
            Assert.That(fields.ContainsKey("graphicsDeviceRecreated"), Is.True);
        }
        [TestCase("../outside.rdc")]
        [TestCase("capture.png")]
        public void CaptureRejectsOutputOutsideItsArtifactContract(string suffix)
        {
            string logs = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs"));
            Assert.Throws<ArgumentException>(() => VmRenderDocCaptureCommands.ValidatePath(Path.Combine(logs, suffix)));
        }
        [Test]
        public void CaptureRequiresAnExistingWindowIdentityAndClosedRequest()
        {
            var schema = VmAutomationToolInputSchemaCatalog.Get("profiler/renderdoc-capture");
            Assert.That(VmAutomationInputValidator.TryValidate(new Dictionary<string, object>
                { { "path", "Logs/frame.rdc" } }, schema, out _, out _, out _), Is.False);
            Assert.That(VmAutomationInputValidator.TryValidate(new Dictionary<string, object>
                { { "path", "Logs/frame.rdc" }, { "windowInstanceId", "1" }, { "createWindow", true } },
                schema, out _, out _, out _), Is.False);
        }
        [Test]
        public void CaptureDeclaresRestorationAndNativeFrameIdentity()
        {
            Assert.That(VmAutomationCatalog.TryGetTool("profiler/renderdoc-capture", true, out var tool), Is.True);
            var output = (Dictionary<string, object>)tool["outputSchema"];
            var fields = (Dictionary<string, object>)output["properties"];
            Assert.That(fields.ContainsKey("capturePathRestored"), Is.True);
            Assert.That(fields.ContainsKey("startFrame"), Is.True);
            Assert.That(fields.ContainsKey("endFrame"), Is.True);
        }
    }
}
