using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmSerializedSingletonTests
    {
        [TearDown]
        public void RemoveOwnedSettings()
        {
            UnityEngine.Object.DestroyImmediate(VmSerializedSingletonTestSettings.instance);
            File.Delete(VmSerializedSingletonTestSettings.SettingsPath);
            LogAssert.NoUnexpectedReceived();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void WritePersistsThroughNativeSingletonReload(bool useInstanceId)
        {
            var original = VmSerializedSingletonTestSettings.instance;
            var args = new Dictionary<string, object>
            {
                { "propertyPath", "resourceFolder" },
                { "value", "Assets/GameResources/Configurations/CommonPriorities" }
            };
            if (useInstanceId)
                args["instanceId"] = VmObjectId.Get(original);
            else
                args["scriptableSingletonType"] = typeof(VmSerializedSingletonTestSettings).FullName;

            var write = (Dictionary<string, object>)VmAutomationSerializedObjectCommands.Set(args);
            Assert.That(write["success"], Is.True);
            Assert.That(write["settingsPath"], Is.EqualTo(VmSerializedSingletonTestSettings.SettingsPath));
            Assert.That(File.Exists(VmSerializedSingletonTestSettings.SettingsPath), Is.True);
            UnityEngine.Object.DestroyImmediate(original);

            var read = (Dictionary<string, object>)VmAutomationSerializedObjectCommands.Get(
                new Dictionary<string, object>
                {
                    { "scriptableSingletonType", typeof(VmSerializedSingletonTestSettings).FullName },
                    { "propertyPath", "resourceFolder" }
                });
            Assert.That(read["success"], Is.True);
            var property = (Dictionary<string, object>)read["property"];
            Assert.That(property["value"], Is.EqualTo(args["value"]));
            Assert.That(VmSerializedSingletonTestSettings.instance.ResourceFolder, Is.EqualTo(args["value"]));
            Assert.That(ReferenceEquals(original, VmSerializedSingletonTestSettings.instance), Is.False);
        }

        [TestCase(typeof(Texture2D), "scriptable_singleton_type_invalid")]
        [TestCase(typeof(VmSerializedSingletonWithoutFilePath), "scriptable_singleton_file_path_missing")]
        [TestCase(typeof(VmSerializedSingletonPreferences), "scriptable_singleton_file_path_outside_project")]
        public void UnsupportedPersistenceIsRejectedBeforeCreatingTheSingleton(Type type, string expectedCode)
        {
            object result = VmAutomationSerializedObjectCommands.Get(
                new Dictionary<string, object> { { "scriptableSingletonType", type.FullName } });
            Assert.That(VmAutomationResponse.TryGetError(result, out _, out string code, out _), Is.True);
            Assert.That(code, Is.EqualTo(expectedCode));
        }

        [Test]
        public void SingletonSelectorCannotBeCombinedWithAnInstanceSelector()
        {
            object result = VmAutomationSerializedObjectCommands.Get(new Dictionary<string, object>
            {
                { "scriptableSingletonType", typeof(VmSerializedSingletonTestSettings).FullName },
                { "instanceId", 1 }
            });
            Assert.That(VmAutomationResponse.TryGetError(result, out _, out string code, out _), Is.True);
            Assert.That(code, Is.EqualTo("serialized_object_target_selector_conflict"));
        }
    }
}
