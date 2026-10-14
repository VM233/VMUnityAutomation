using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmAutomationAssetRefreshTests
    {
        [TestCase("same", null)]
        [TestCase("changed", "idempotency_conflict")]
        [TestCase("foreign", "job_owner_mismatch")]
        [TestCase("new", "workspace_job_active")]
        public async Task PublicRefreshReplayRetainsItsOriginalAdmission(string change, string errorCode)
        {
            string owner = "public-refresh-replay";
            string project = Path.GetDirectoryName(Application.dataPath);
            var request = new Dictionary<string, object> {
                { "idempotencyKey", "refresh-replay-" + Guid.NewGuid().ToString("N") },
                { "assetPaths", Array.Empty<string>() } };
            var first = await VmAutomationExecutor.ExecuteAsync("vm_auto_asset_refresh", request,
                agentId: owner, expectedProjectPath: project);
            Assert.That(first.Ok, Is.True, first.Error?.Message);
            var receipt = VmAutomationResponse.ToDictionary(first.Result);
            string id = (string)receipt["jobId"];
            var access = new Dictionary<string, object> {
                { "jobId", id }, { "jobAccessToken", receipt["jobAccessToken"] } };
            try
            {
                var owned = VmAutomationWorkspaceJobStore.Find(id);
                Assert.That(owned.ClientAdopted, Is.False);
                if (change == "changed") request["saveAssets"] = true;
                if (change == "new") request["idempotencyKey"] = "different-" + Guid.NewGuid().ToString("N");
                var second = await VmAutomationExecutor.ExecuteAsync("vm_auto_asset_refresh", request,
                    agentId: change == "foreign" ? "another-public-owner" : owner, expectedProjectPath: project);
                if (errorCode == null)
                {
                    Assert.That(second.Ok, Is.True, second.Error?.Message);
                    var replay = VmAutomationResponse.ToDictionary(second.Result);
                    Assert.That(replay["jobId"], Is.EqualTo(id));
                    Assert.That(replay["jobAccessToken"], Is.EqualTo(receipt["jobAccessToken"]));
                }
                else
                {
                    Assert.That(second.Ok, Is.False);
                    Assert.That(second.Error.Code, Is.EqualTo(errorCode));
                }
                Assert.That(owned.ClientAdopted, Is.False);
                Assert.That(owned.AssetRefreshInvocationCount, Is.Zero);
                Assert.That(owned.Status, Is.EqualTo("queued"));
            }
            finally { VmAutomationWorkspaceJobRunner.Cancel(access); }
        }

        [TestCase("same", null)]
        [TestCase("changed", "idempotency_conflict")]
        [TestCase("foreign", "job_owner_mismatch")]
        [TestCase("new", "workspace_job_active")]
        public async Task PublicPlayModeReplayPreservesThePendingStop(string change, string errorCode)
        {
            string project = Path.GetDirectoryName(Application.dataPath);
            string owner = "public-transition-replay";
            var play = new Dictionary<string, object> {
                { "action", "play" }, { "idempotencyKey", "play-replay-" + Guid.NewGuid().ToString("N") } };
            var first = await VmAutomationExecutor.ExecuteAsync("vm_auto_editor_play_mode", play,
                agentId: owner, expectedProjectPath: project);
            Assert.That(first.Ok, Is.True, first.Error?.Message);
            var playReceipt = VmAutomationResponse.ToDictionary(first.Result);
            var playAccess = new Dictionary<string, object> {
                { "jobId", playReceipt["jobId"] }, { "jobAccessToken", playReceipt["jobAccessToken"] } };
            Dictionary<string, object> stopAccess = null;
            try
            {
                var stop = await VmAutomationExecutor.ExecuteAsync("vm_auto_editor_play_mode",
                    new Dictionary<string, object> {
                        { "action", "stop" }, { "idempotencyKey", "stop-replay-" + Guid.NewGuid().ToString("N") } },
                    agentId: "public-stop-owner", expectedProjectPath: project);
                Assert.That(stop.Ok, Is.True, stop.Error?.Message);
                var stopReceipt = VmAutomationResponse.ToDictionary(stop.Result);
                stopAccess = new Dictionary<string, object> {
                    { "jobId", stopReceipt["jobId"] }, { "jobAccessToken", stopReceipt["jobAccessToken"] } };
                var original = VmAutomationWorkspaceJobStore.Find((string)playReceipt["jobId"]);
                var pendingStop = VmAutomationWorkspaceJobStore.Find((string)stopReceipt["jobId"]);
                Assert.That(original.Status, Is.EqualTo("canceled"));
                Assert.That(pendingStop.Status, Is.EqualTo("queued"));
                int count = VmAutomationWorkspaceJobStore.GetAll().Count;
                DateTime stopUpdatedAt = pendingStop.UpdatedAt;
                if (change == "changed") play["stableFrames"] = 3;
                if (change == "new") play["idempotencyKey"] = "another-play-" + Guid.NewGuid().ToString("N");
                var replay = await VmAutomationExecutor.ExecuteAsync("vm_auto_editor_play_mode", play,
                    agentId: change == "foreign" ? "foreign-transition-owner" : owner, expectedProjectPath: project);
                if (errorCode == null)
                {
                    Assert.That(replay.Ok, Is.True, replay.Error?.Message);
                    var returned = VmAutomationResponse.ToDictionary(replay.Result);
                    Assert.That(returned["jobId"], Is.EqualTo(playReceipt["jobId"]));
                    Assert.That(returned["jobAccessToken"], Is.EqualTo(playReceipt["jobAccessToken"]));
                }
                else
                {
                    Assert.That(replay.Ok, Is.False);
                    Assert.That(replay.Error.Code, Is.EqualTo(errorCode));
                }
                Assert.That(VmAutomationWorkspaceJobStore.GetAll().Count, Is.EqualTo(count));
                Assert.That(pendingStop.Status, Is.EqualTo("queued"));
                Assert.That(pendingStop.UpdatedAt, Is.EqualTo(stopUpdatedAt));
                Assert.That(pendingStop.ClientAdopted, Is.False);
                Assert.That(original.ClientAdopted, Is.False);
                Assert.That(pendingStop.TransactionState, Is.Null);
                Assert.That(original.TransactionState, Is.Null);
            }
            finally
            {
                if (stopAccess != null) VmAutomationWorkspaceJobRunner.Cancel(stopAccess);
                VmAutomationWorkspaceJobRunner.Cancel(playAccess);
            }
        }

        [TestCase("same", null)]
        [TestCase("dryRun", "idempotency_conflict")]
        [TestCase("foreign", "job_owner_mismatch")]
        public async Task PublicTransactionReplaySurvivesMovedSource(string change, string errorCode)
        {
            string folder = CreateTestFolder();
            string before = folder + "/Before.asset", after = folder + "/After.asset";
            Dictionary<string, object> access = null;
            try
            {
                var fixture = ScriptableObject.CreateInstance<VmAutomationInspectionTestAsset>();
                AssetDatabase.CreateAsset(fixture, before);
                AssetDatabase.SaveAssetIfDirty(fixture);
                string guid = AssetDatabase.AssetPathToGUID(before);
                var request = new Dictionary<string, object> {
                    { "idempotencyKey", "transaction-replay-" + Guid.NewGuid().ToString("N") },
                    { "operations", new object[] { new Dictionary<string, object> {
                        { "type", "move" }, { "sourcePath", before }, { "targetPath", after } } } } };
                string project = Path.GetDirectoryName(Application.dataPath), owner = "public-transaction-replay";
                var first = await VmAutomationExecutor.ExecuteAsync("asset/transaction", request,
                    agentId: owner, expectedProjectPath: project);
                Assert.That(first.Ok, Is.True, first.Error?.Message);
                var receipt = VmAutomationResponse.ToDictionary(first.Result);
                access = new Dictionary<string, object> {
                    { "jobId", receipt["jobId"] }, { "jobAccessToken", receipt["jobAccessToken"] } };
                var original = VmAutomationWorkspaceJobStore.Find((string)receipt["jobId"]);
                Assert.That(AssetDatabase.MoveAsset(before, after), Is.Empty);
                if (change == "dryRun") request["dryRun"] = true;
                var replay = await VmAutomationExecutor.ExecuteAsync("asset/transaction", request,
                    agentId: change == "foreign" ? "foreign-transaction-owner" : owner, expectedProjectPath: project);
                if (errorCode == null)
                {
                    Assert.That(replay.Ok, Is.True, replay.Error?.Message);
                    Assert.That(VmAutomationResponse.ToDictionary(replay.Result)["jobId"], Is.EqualTo(receipt["jobId"]));
                }
                else
                {
                    Assert.That(replay.Ok, Is.False);
                    Assert.That(replay.Error.Code, Is.EqualTo(errorCode));
                }
                Assert.That(original.Status, Is.EqualTo("queued"));
                Assert.That(original.ClientAdopted, Is.False);
                Assert.That(original.TransactionState, Is.Null);
                Assert.That(AssetDatabase.AssetPathToGUID(after), Is.EqualTo(guid));
                Assert.That(AssetDatabase.LoadMainAssetAtPath(before), Is.Null);
            }
            finally
            {
                if (access != null) VmAutomationWorkspaceJobRunner.Cancel(access);
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MetadataRefreshPreservesFolderAndChildIdentity(bool forceUpdate)
        {
            string folder = CreateTestFolder();
            string childPath = folder + "/Child.asset";
            try
            {
                var child = ScriptableObject.CreateInstance<VmAutomationInspectionTestAsset>();
                AssetDatabase.CreateAsset(child, childPath);
                AssetDatabase.SaveAssetIfDirty(child);
                string folderGuid = AssetDatabase.AssetPathToGUID(folder);
                string childGuid = AssetDatabase.AssetPathToGUID(childPath);
                byte[] folderMetadata = File.ReadAllBytes(folder + ".meta");
                byte[] childMetadata = File.ReadAllBytes(childPath + ".meta");
                var result = (Dictionary<string, object>)
                    VmAutomationAssetCommands.ExecuteRefreshImmediate(
                        new Dictionary<string, object>
                        {
                            { "assetPaths", new[] { folder + ".meta", folder,
                                childPath + ".meta", childPath } },
                            { "forceUpdate", forceUpdate },
                        });

                Assert.That(result["success"], Is.True);
                Assert.That(result["refreshMode"], Is.EqualTo("targeted"));
                Assert.That(result["importedPaths"], Is.EquivalentTo(new[] { folder, childPath }));
                Assert.That(AssetDatabase.IsValidFolder(folder), Is.True);
                Assert.That(AssetDatabase.AssetPathToGUID(folder), Is.EqualTo(folderGuid));
                Assert.That(AssetDatabase.AssetPathToGUID(childPath), Is.EqualTo(childGuid));
                Assert.That(File.ReadAllBytes(folder + ".meta"), Is.EqualTo(folderMetadata));
                Assert.That(File.ReadAllBytes(childPath + ".meta"), Is.EqualTo(childMetadata));
                Assert.That(AssetDatabase.LoadAssetAtPath<VmAutomationInspectionTestAsset>(childPath),
                    Is.Not.Null);
            }
            finally
            {
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [TestCase(".cs")]
        [TestCase(".asmdef")]
        [TestCase(".asmref")]
        [TestCase(".rsp")]
        public void CompilationMetadataUsesCompilationImportPolicy(string extension)
        {
            ImportAssetOptions options = VmAutomationAssetCommands.GetTargetedImportOptions(
                "Assets/Compilation Asset" + extension + ".meta", true);
            Assert.That(options & ImportAssetOptions.ForceUpdate,
                Is.EqualTo(ImportAssetOptions.Default));
            Assert.That(options & ImportAssetOptions.ForceSynchronousImport,
                Is.EqualTo(ImportAssetOptions.ForceSynchronousImport));
        }

        [Test]
        public void LoadedSceneMetadataIsRejectedBeforeImport()
        {
            string folder = CreateTestFolder();
            string scenePath = folder + "/Loaded Scene.unity";
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            try
            {
                Assert.That(EditorSceneManager.SaveScene(scene, scenePath), Is.True);
                string sceneGuid = AssetDatabase.AssetPathToGUID(scenePath);
                byte[] metadata = File.ReadAllBytes(scenePath + ".meta");
                var result = (Dictionary<string, object>)
                    VmAutomationAssetCommands.ExecuteRefreshImmediate(
                        new Dictionary<string, object> { { "assetPaths", new[] { scenePath + ".meta" } } });
                Assert.That(result["success"], Is.False);
                Assert.That(result["errorCode"], Is.EqualTo("loaded_scene_asset_mutation_blocked"));
                Assert.That(AssetDatabase.AssetPathToGUID(scenePath), Is.EqualTo(sceneGuid));
                Assert.That(File.ReadAllBytes(scenePath + ".meta"), Is.EqualTo(metadata));
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(folder);
            }
        }

        private static string CreateTestFolder()
        {
            string folderName = "Asset Refresh Test " + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folderName);
            return "Assets/" + folderName;
        }
    }
}
