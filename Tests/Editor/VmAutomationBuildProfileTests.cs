using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmAutomationBuildProfileTests
    {
        private const string SettingsFolder = "Assets/Settings";
        private const string BuildProfilesFolder = SettingsFolder + "/Build Profiles";

        [Test]
        public void MissingBuildProfilesReturnsCapabilityUnavailable()
        {
            if (VmAutomationAssetGraphUtility.FindType(
                    "UnityEditor.Build.Profile.BuildProfile") != null)
                Assert.Ignore("This assertion targets Editors without native Build Profiles.");

            var response = (Dictionary<string, object>)
                VmAutomationBuildProfileCommands.Execute(new Dictionary<string, object>
                {
                    { "action", "info" },
                });

            Assert.That(response["success"], Is.False);
            Assert.That(response["errorCode"], Is.EqualTo("capability_unavailable"));
            Assert.That(response["retryable"], Is.False);
        }

        [Test]
        public void TransactionCreatesProfileForInstalledPlatform()
        {
            Type profileType = VmAutomationAssetGraphUtility.FindType(
                "UnityEditor.Build.Profile.BuildProfile");
            if (profileType == null)
                Assert.Ignore("Build Profiles are unavailable in this Unity version.");
            var getActive = profileType.GetMethod("GetActiveBuildProfile");
            var setActive = profileType.GetMethod("SetActiveBuildProfile");
            object originalActive = getActive.Invoke(null, null);

            var info = (Dictionary<string, object>)
                VmAutomationBuildProfileCommands.Execute(new Dictionary<string, object>
                {
                    { "action", "info" },
                });
            var installedPlatforms =
                (List<Dictionary<string, object>>)info["installedPlatforms"];
            if (installedPlatforms.Count == 0)
                Assert.Ignore("No installed Build Profile platform is available.");

            bool settingsFolderExisted = AssetDatabase.IsValidFolder(SettingsFolder);
            bool buildProfilesFolderExisted = AssetDatabase.IsValidFolder(BuildProfilesFolder);
            string profileName = "VM Automation Test " + Guid.NewGuid().ToString("N");
            string profilePath = BuildProfilesFolder + "/" + profileName + ".asset";
            PropertyInfo activePlatform = typeof(EditorUserBuildSettings).GetProperty(
                "activePlatformGuid", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(activePlatform, Is.Not.Null,
                "Unity must publish the active platform GUID before a profile is activated.");
            string activePlatformId = activePlatform.GetValue(null).ToString();
            Dictionary<string, object> platform = installedPlatforms.Single(candidate =>
                candidate["platformId"].ToString() == activePlatformId);
            try
            {
                var response = (Dictionary<string, object>)
                    VmAutomationBuildProfileCommands.Execute(new Dictionary<string, object>
                    {
                        { "action", "transaction" },
                        { "operations", new List<object>
                            {
                                new Dictionary<string, object>
                                {
                                    { "action", "create" },
                                    { "profileName", profileName },
                                    { "platformId", platform["platformId"] },
                                },
                            }
                        },
                    });

                Assert.That(response["success"], Is.True);
                var results = (List<Dictionary<string, object>>)response["results"];
                Assert.That(results, Has.Count.EqualTo(1));
                Assert.That(results[0]["assetPath"], Is.EqualTo(profilePath));
                Assert.That(results[0]["platformId"], Is.EqualTo(platform["platformId"]));
                Assert.That(AssetDatabase.LoadMainAssetAtPath(profilePath), Is.Not.Null);

                var profile = (Dictionary<string, object>)results[0]["profile"];
                Assert.That(profile["platformId"], Is.EqualTo(platform["platformId"]));
                Assert.That(profile["name"], Is.EqualTo(profileName));
                Resources.UnloadAsset(AssetDatabase.LoadMainAssetAtPath(profilePath));
                object createdProfile = AssetDatabase.LoadMainAssetAtPath(profilePath);
                Assert.That(createdProfile, Is.Not.Null,
                    "Native creation must persist a reloadable profile asset.");
                Assert.That(profileType.GetProperty("platformId",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(createdProfile).ToString(),
                    Is.EqualTo(activePlatformId));
                Assert.That(getActive.Invoke(null, null), Is.SameAs(originalActive),
                    "Authoring a profile must preserve the active selection.");
                setActive.Invoke(null, new[] { createdProfile });
                var platformSelection = new Dictionary<string, object>
                {
                    { "action", "transaction" },
                    { "operations", new List<object>
                        {
                            new Dictionary<string, object>
                            {
                                { "action", "set-active" },
                                { "assetPath", null },
                            },
                        }
                    },
                    { "dryRun", true },
                };
                var dryRun = (Dictionary<string, object>)
                    VmAutomationBuildProfileCommands.Execute(platformSelection);
                Assert.That(dryRun["success"], Is.True);
                Assert.That(getActive.Invoke(null, null), Is.SameAs(createdProfile));
                platformSelection["dryRun"] = false;
                var selected = (Dictionary<string, object>)
                    VmAutomationBuildProfileCommands.Execute(platformSelection);
                Assert.That(selected["success"], Is.True);
                Assert.That(getActive.Invoke(null, null), Is.Null);
                var selectionResults = (List<Dictionary<string, object>>)selected["results"];
                Assert.That(selectionResults.Single()["assetPath"], Is.Null);
                Assert.That(selectionResults.Single()["profile"], Is.Null);
            }
            finally
            {
                setActive.Invoke(null, new[] { originalActive });
                AssetDatabase.DeleteAsset(profilePath);
                DeleteCreatedFolderIfEmpty(BuildProfilesFolder, buildProfilesFolderExisted);
                DeleteCreatedFolderIfEmpty(SettingsFolder, settingsFolderExisted);
            }
        }

#if UNITY_6000_3_OR_NEWER
        [Test]
        public void DefinesEnableNativeOverrideAndPersistWithoutActivatingProfile()
        {
            Type profileType = VmAutomationAssetGraphUtility.FindType(
                "UnityEditor.Build.Profile.BuildProfile");
            object originalActive = profileType.GetMethod("GetActiveBuildProfile").Invoke(null, null);
            PropertyInfo activePlatform = typeof(EditorUserBuildSettings).GetProperty(
                "activePlatformGuid", BindingFlags.Static | BindingFlags.NonPublic);
            string profileName = "VM Automation Defines Test " + Guid.NewGuid().ToString("N");
            string profilePath = BuildProfilesFolder + "/" + profileName + ".asset";
            bool settingsExisted = AssetDatabase.IsValidFolder(SettingsFolder);
            bool profilesExisted = AssetDatabase.IsValidFolder(BuildProfilesFolder);
            try
            {
                var created = (Dictionary<string, object>)VmAutomationBuildProfileCommands.Execute(
                    new Dictionary<string, object>
                    {
                        { "action", "transaction" },
                        { "operations", new List<object> { new Dictionary<string, object>
                            {
                                { "action", "create" }, { "profileName", profileName },
                                { "platformId", activePlatform.GetValue(null).ToString() },
                            } } },
                    });
                Assert.That(created["success"], Is.True);
                var request = new Dictionary<string, object>
                {
                    { "action", "transaction" },
                    { "operations", new List<object> { new Dictionary<string, object>
                        {
                            { "action", "set-scripting-defines" }, { "assetPath", profilePath },
                            { "defines", new List<object> { "VM_AUTOMATION_PROFILE_DEFINE" } },
                        } } },
                };
                var changed = (Dictionary<string, object>)VmAutomationBuildProfileCommands.Execute(request);
                Assert.That(changed["success"], Is.True);
                Resources.UnloadAsset(AssetDatabase.LoadMainAssetAtPath(profilePath));
                var reloaded = AssetDatabase.LoadMainAssetAtPath(profilePath);
                Assert.That(profileType.GetProperty("hasScriptingDefines",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(reloaded), Is.True);
                Assert.That((string[])profileType.GetProperty("scriptingDefines").GetValue(reloaded),
                    Is.EqualTo(new[] { "VM_AUTOMATION_PROFILE_DEFINE" }));
                Assert.That(profileType.GetMethod("GetActiveBuildProfile").Invoke(null, null),
                    Is.SameAs(originalActive));
                request["operations"] = new List<object> { new Dictionary<string, object>
                    {
                        { "action", "set-scripting-defines" }, { "assetPath", profilePath },
                        { "defines", new List<object>() },
                    } };
                Assert.That(((Dictionary<string, object>)VmAutomationBuildProfileCommands.Execute(request))
                    ["success"], Is.True);
                Resources.UnloadAsset(reloaded);
                reloaded = AssetDatabase.LoadMainAssetAtPath(profilePath);
                Assert.That(profileType.GetProperty("hasScriptingDefines",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(reloaded), Is.True);
                Assert.That((string[])profileType.GetProperty("scriptingDefines").GetValue(reloaded), Is.Empty);
            }
            finally
            {
                AssetDatabase.DeleteAsset(profilePath);
                DeleteCreatedFolderIfEmpty(BuildProfilesFolder, profilesExisted);
                DeleteCreatedFolderIfEmpty(SettingsFolder, settingsExisted);
            }
        }
#endif

        [Test]
        public void InputSchemaPublishesCreateOperation()
        {
            Dictionary<string, object> schema =
                VmAutomationToolInputSchemaCatalog.Get("build/profile");
            var properties = (Dictionary<string, object>)schema["properties"];
            var operations = (Dictionary<string, object>)properties["operations"];
            var items = (Dictionary<string, object>)operations["items"];
            var variants = (List<object>)items["oneOf"];
            Dictionary<string, object> create = variants
                .Cast<Dictionary<string, object>>()
                .Single(variant =>
                {
                    var variantProperties =
                        (Dictionary<string, object>)variant["properties"];
                    var action = (Dictionary<string, object>)variantProperties["action"];
                    return ((List<object>)action["enum"]).Contains("create");
                });

            var createProperties =
                (Dictionary<string, object>)create["properties"];
            Assert.That(createProperties.Keys,
                Is.EquivalentTo(new[] { "action", "profileName", "platformId" }));
            Assert.That((List<string>)create["required"],
                Is.EquivalentTo(new[] { "action", "profileName", "platformId" }));
            Dictionary<string, object> setActive = variants
                .Cast<Dictionary<string, object>>()
                .Single(variant =>
                {
                    var variantProperties =
                        (Dictionary<string, object>)variant["properties"];
                    var action = (Dictionary<string, object>)variantProperties["action"];
                    return ((List<object>)action["enum"]).Contains("set-active");
                });
            var activeProperties = (Dictionary<string, object>)setActive["properties"];
            var assetPath = (Dictionary<string, object>)activeProperties["assetPath"];
            Assert.That((string[])assetPath["type"],
                Is.EquivalentTo(new[] { "string", "null" }));
        }

        [Test]
        public void TransactionRejectsAssetExtensionBeforeMutation()
        {
            Type profileType = VmAutomationAssetGraphUtility.FindType(
                "UnityEditor.Build.Profile.BuildProfile");
            if (profileType == null)
                Assert.Ignore("Build Profiles are unavailable in this Unity version.");

            var info = (Dictionary<string, object>)
                VmAutomationBuildProfileCommands.Execute(new Dictionary<string, object>
                {
                    { "action", "info" },
                });
            var installedPlatforms =
                (List<Dictionary<string, object>>)info["installedPlatforms"];
            if (installedPlatforms.Count == 0)
                Assert.Ignore("No installed Build Profile platform is available.");

            var response = (Dictionary<string, object>)
                VmAutomationBuildProfileCommands.Execute(new Dictionary<string, object>
                {
                    { "action", "transaction" },
                    { "operations", new List<object>
                        {
                            new Dictionary<string, object>
                            {
                                { "action", "create" },
                                { "profileName", "Invalid.asset" },
                                { "platformId", installedPlatforms[0]["platformId"] },
                            },
                        }
                    },
                });

            Assert.That(response["success"], Is.False);
            Assert.That(response["errorCode"],
                Is.EqualTo("build_profile_transaction_invalid"));
            Assert.That(AssetDatabase.LoadMainAssetAtPath(
                BuildProfilesFolder + "/Invalid.asset.asset"), Is.Null);
        }

        [Test]
        public void PlayerPropertyPersistsOnInactiveProfileWithoutChangingEffectiveSettings()
        {
            Type profileType = VmAutomationAssetGraphUtility.FindType(
                "UnityEditor.Build.Profile.BuildProfile");
            if (profileType == null)
                Assert.Ignore("Build Profiles are unavailable in this Unity version.");
            PropertyInfo activePlatform = typeof(EditorUserBuildSettings).GetProperty(
                "activePlatformGuid", BindingFlags.Static | BindingFlags.NonPublic);
            string platformId = activePlatform.GetValue(null).ToString();
            string profileName = "VM Automation Override Test " + Guid.NewGuid().ToString("N");
            string profilePath = BuildProfilesFolder + "/" + profileName + ".asset";
            string originalTemplate = PlayerSettings.WebGL.template;
            object originalActive = profileType.GetMethod("GetActiveBuildProfile").Invoke(null, null);
            bool settingsFolderExisted = AssetDatabase.IsValidFolder(SettingsFolder);
            bool profilesFolderExisted = AssetDatabase.IsValidFolder(BuildProfilesFolder);
            try
            {
                var created = (Dictionary<string, object>)VmAutomationBuildProfileCommands.Execute(
                    new Dictionary<string, object>
                    {
                        { "action", "transaction" },
                        { "operations", new List<object> { new Dictionary<string, object>
                            {
                                { "action", "create" }, { "profileName", profileName },
                                { "platformId", platformId },
                            } } },
                    });
                Assert.That(created["success"], Is.True);
                var profile = AssetDatabase.LoadMainAssetAtPath(profilePath);
                profileType.GetMethod("CreatePlayerSettingsFromGlobal",
                    BindingFlags.Instance | BindingFlags.NonPublic).Invoke(profile, null);
                var change = new Dictionary<string, object>
                {
                    { "action", "set-player-property" }, { "assetPath", profilePath },
                    { "propertyPath", "webGLTemplate" }, { "value", "APPLICATION:Minimal" },
                };
                var request = new Dictionary<string, object>
                {
                    { "action", "transaction" }, { "operations", new List<object> { change } },
                };
                var changed = (Dictionary<string, object>)VmAutomationBuildProfileCommands.Execute(request);
                Assert.That(changed["success"], Is.True);
                Assert.That(PlayerSettings.WebGL.template, Is.EqualTo(originalTemplate));
                Assert.That(profileType.GetMethod("GetActiveBuildProfile").Invoke(null, null),
                    Is.SameAs(originalActive));
                Resources.UnloadAsset(profile);
                profile = AssetDatabase.LoadMainAssetAtPath(profilePath);
                var settings = (UnityEngine.Object)profileType.GetProperty("playerSettings",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(profile);
                Assert.That(new SerializedObject(settings).FindProperty("webGLTemplate").stringValue,
                    Is.EqualTo("APPLICATION:Minimal"));
                change["value"] = new List<object>();
                var rejected = (Dictionary<string, object>)VmAutomationBuildProfileCommands.Execute(request);
                Assert.That(rejected["success"], Is.False);
                Assert.That(rejected["errorCode"], Is.EqualTo("build_profile_transaction_invalid"));
                Assert.That(new SerializedObject(settings).FindProperty("webGLTemplate").stringValue,
                    Is.EqualTo("APPLICATION:Minimal"));
            }
            finally
            {
                AssetDatabase.DeleteAsset(profilePath);
                DeleteCreatedFolderIfEmpty(BuildProfilesFolder, profilesFolderExisted);
                DeleteCreatedFolderIfEmpty(SettingsFolder, settingsFolderExisted);
            }
        }

        private static void DeleteCreatedFolderIfEmpty(string assetPath, bool existed)
        {
            if (existed || !AssetDatabase.IsValidFolder(assetPath))
                return;
            string absolutePath = Path.Combine(
                Path.GetDirectoryName(Application.dataPath),
                assetPath.Replace('/', Path.DirectorySeparatorChar));
            if (Directory.EnumerateFileSystemEntries(absolutePath).Any())
                return;
            AssetDatabase.DeleteAsset(assetPath);
        }
    }
}
