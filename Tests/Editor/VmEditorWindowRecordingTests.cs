using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmEditorWindowRecordingTests
    {
        [Test]
        public void ExactNativeWindowIdentityReachesOutputAdmission()
        {
            var window = ScriptableObject.CreateInstance<EditorWindow>();
            var focused = EditorWindow.focusedWindow;
            try
            {
                var error = Assert.Throws<VmProjectToolException>(() =>
                    VmEditorWindowRecording.Begin(new VmEditorWindowRecordingRequest
                    {
                        WindowInstanceId = VmObjectId.Get(window)
                    }));
                Assert.That(error.ErrorCode, Is.EqualTo("recording_invalid_arguments"));
                StringAssert.Contains("absolute new .mp4 path", error.Message);
                Assert.That(EditorWindow.focusedWindow, Is.SameAs(focused));
            }
            finally { Object.DestroyImmediate(window); }
        }

        [TestCase(null)]
        [TestCase("0")]
        [TestCase("not-an-object-id")]
        public void InvalidIdentityRejectsBeforeOutputAdmission(string identity)
        {
            var focused = EditorWindow.focusedWindow;
            var error = Assert.Throws<VmProjectToolException>(() =>
                VmEditorWindowRecording.Begin(new VmEditorWindowRecordingRequest
                {
                    WindowInstanceId = identity
                }));
            Assert.That(error.ErrorCode, Is.EqualTo("recording_invalid_arguments"));
            StringAssert.Contains("existing EditorWindow", error.Message);
            Assert.That(EditorWindow.focusedWindow, Is.SameAs(focused));
        }

        [Test]
        public void AnotherNativeObjectCannotBecomeARecordingWindow()
        {
            var texture = new Texture2D(1, 1);
            try
            {
                var error = Assert.Throws<VmProjectToolException>(() =>
                    VmEditorWindowRecording.Begin(new VmEditorWindowRecordingRequest
                    {
                        WindowInstanceId = VmObjectId.Get(texture)
                    }));
                Assert.That(error.ErrorCode, Is.EqualTo("recording_invalid_arguments"));
                StringAssert.Contains("existing EditorWindow", error.Message);
            }
            finally { Object.DestroyImmediate(texture); }
        }
    }
}
