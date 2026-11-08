using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    public sealed class VmUIBuilderCanvasSettingsTests
    {
        [Serializable]
        private sealed class Settings
        {
            public int CanvasWidth, CanvasHeight;
            public float ZoomScale;
            public Vector2 PanOffset;
            public string AdditionalSetting;
        }

        [Test]
        public void FullNativeSnapshotIsRestoredBeforeViewportPublication()
        {
            var settings = new Settings { CanvasWidth = 740, CanvasHeight = 1600, ZoomScale = .42f,
                PanOffset = new Vector2(17, 38), AdditionalSetting = "Preserve fields outside the preview controls" };
            string original = EditorJsonUtility.ToJson(settings, true);
            settings.CanvasWidth = 1080; settings.CanvasHeight = 2400; settings.ZoomScale = .2f;
            settings.PanOffset = Vector2.zero; settings.AdditionalSetting = "Changed";
            bool published = false;
            VmUIBuilderCanvasSettingsScope.RestoreNativeSettings(settings, original, () =>
            {
                Assert.That(EditorJsonUtility.ToJson(settings, true), Is.EqualTo(original));
                published = true;
            });
            Assert.That(published, Is.True);
        }

        [Test]
        public void NativePublicationCannotSilentlyChangeTheRestoredSnapshot()
        {
            var settings = new Settings { CanvasWidth = 740, AdditionalSetting = "Original" };
            string original = EditorJsonUtility.ToJson(settings, true);
            Assert.Throws<InvalidOperationException>(() => VmUIBuilderCanvasSettingsScope.RestoreNativeSettings(
                settings, original, () => settings.AdditionalSetting = "Unexpected native writer"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ExactFileBytesAndOriginalAbsenceAreBothRestored(bool existed)
        {
            string folder = Path.Combine(Path.GetTempPath(), "VMUIBuilderCanvasSettings_" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(folder, "owned.json");
            Directory.CreateDirectory(folder);
            byte[] original = existed ? System.Text.Encoding.UTF8.GetBytes("{\r\n  \"canvas\": 740\r\n}\r\n") : null;
            try
            {
                File.WriteAllText(path, "Changed preview settings");
                Assert.That(VmUIBuilderCanvasSettingsScope.FileMatches(path, original), Is.False);
                VmUIBuilderCanvasSettingsScope.RestoreFile(path, original);
                Assert.That(VmUIBuilderCanvasSettingsScope.FileMatches(path, original), Is.True);
            }
            finally
            {
                File.Delete(path);
                Directory.Delete(folder);
            }
        }
    }
}
