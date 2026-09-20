using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        public void TransactionCreatesProfileForInstalledPlatform()
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

            bool settingsFolderExisted = AssetDatabase.IsValidFolder(SettingsFolder);
            bool buildProfilesFolderExisted = AssetDatabase.IsValidFolder(BuildProfilesFolder);
            string profileName = "VM Automation Test " + Guid.NewGuid().ToString("N");
            string profilePath = BuildProfilesFolder + "/" + profileName + ".asset";
            Dictionary<string, object> platform = installedPlatforms[0];
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
            }
            finally
            {
                AssetDatabase.DeleteAsset(profilePath);
                DeleteCreatedFolderIfEmpty(BuildProfilesFolder, buildProfilesFolderExisted);
                DeleteCreatedFolderIfEmpty(SettingsFolder, settingsFolderExisted);
            }
        }

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
