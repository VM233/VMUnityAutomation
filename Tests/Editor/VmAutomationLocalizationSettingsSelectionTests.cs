using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor.Tests
{
    public sealed class VmAutomationLocalizationSettingsSelectionTests
    {
        private string folder;
        private Type settingsType;
        private PropertyInfo activeSettings;
        private PropertyInfo runtimeSettings;
        private MethodInfo updateSettings;
        private object originalActive;
        private object originalRuntime;

        [SetUp]
        public void SetUp()
        {
            settingsType = Type.GetType("UnityEngine.Localization.Settings.LocalizationSettings, Unity.Localization");
            var editorType = Type.GetType("UnityEditor.Localization.LocalizationEditorSettings, Unity.Localization.Editor");
            var commandType = Type.GetType("VMUnityAutomation.Editor.Localization.VmAutomationLocalizationCommands, VMUnityAutomation.Editor.Localization");
            if (settingsType == null || editorType == null || commandType == null)
                Assert.Ignore("Unity Localization is not installed in this test project.");

            activeSettings = editorType.GetProperty("ActiveLocalizationSettings", BindingFlags.Public | BindingFlags.Static);
            runtimeSettings = settingsType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            updateSettings = commandType.GetMethod("UpdateSettings", BindingFlags.NonPublic | BindingFlags.Static);
            originalActive = activeSettings.GetValue(null);
            originalRuntime = runtimeSettings.GetValue(null);
            folder = "Assets/VM Localization Selection " + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));
        }

        [TearDown]
        public void TearDown()
        {
            if (activeSettings != null)
                activeSettings.SetValue(null, originalActive);
            if (runtimeSettings != null)
                runtimeSettings.SetValue(null, originalRuntime);
            if (folder != null)
                AssetDatabase.DeleteAsset(folder);
        }

        [Test]
        public void RegistersExistingSettingsFromAnEmptyProjectBinding()
        {
            string path = folder + "/Localization Settings.asset";
            var settings = ScriptableObject.CreateInstance(settingsType);
            AssetDatabase.CreateAsset(settings, path);
            activeSettings.SetValue(null, null);

            var result = Invoke(new Dictionary<string, object>
            {
                { "settingsAssetPath", path },
                { "initializeSynchronously", true }
            });

            Assert.That(result["success"], Is.True);
            Assert.That(result["settingsAssetPath"], Is.EqualTo(path));
            Assert.That(activeSettings.GetValue(null), Is.SameAs(settings));
            Assert.That(runtimeSettings.GetValue(null), Is.SameAs(settings));
            CollectionAssert.Contains((System.Collections.IEnumerable)result["changed"], "settingsAssetPath");
            Assert.That(new SerializedObject(settings).FindProperty("m_InitializeSynchronously").boolValue, Is.True);
            Assert.That(System.IO.File.ReadAllText("ProjectSettings/EditorBuildSettings.asset"),
                Does.Contain(AssetDatabase.AssetPathToGUID(path)));
        }

        [TestCase("Packages/Invalid.asset")]
        [TestCase("Assets/Missing Localization Settings.asset")]
        public void InvalidSelectionPreservesBothSettingsAuthorities(string path)
        {
            var result = Invoke(new Dictionary<string, object> { { "settingsAssetPath", path } });
            Assert.That(result["success"], Is.False);
            Assert.That(activeSettings.GetValue(null), Is.SameAs(originalActive));
            Assert.That(runtimeSettings.GetValue(null), Is.SameAs(originalRuntime));
        }

        [Test]
        public void InvalidLocaleDoesNotPublishTheRequestedSettings()
        {
            string path = folder + "/Unpublished Settings.asset";
            var settings = ScriptableObject.CreateInstance(settingsType);
            AssetDatabase.CreateAsset(settings, path);
            var result = Invoke(new Dictionary<string, object>
            {
                { "settingsAssetPath", path },
                { "projectLocale", "invalid-unregistered-locale" }
            });

            Assert.That(result["success"], Is.False);
            Assert.That(activeSettings.GetValue(null), Is.SameAs(originalActive));
            Assert.That(runtimeSettings.GetValue(null), Is.SameAs(originalRuntime));
        }

        private Dictionary<string, object> Invoke(Dictionary<string, object> arguments)
        {
            return (Dictionary<string, object>)updateSettings.Invoke(null, new object[] { arguments });
        }
    }
}
