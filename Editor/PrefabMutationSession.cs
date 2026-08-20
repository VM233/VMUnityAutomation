using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static VMUnityAutomation.Editor.VmAutomationPrefabCommandUtility;
using static VMUnityAutomation.Editor.VmAutomationPrefabYamlUtility;

namespace VMUnityAutomation.Editor
{
    internal sealed class PrefabMutationSession : IDisposable
    {
        public readonly string AssetPath;
        public readonly AssetTextSnapshot BeforeSnapshot;

        public GameObject Root { get; private set; }
        public bool SaveAttempted { get; private set; }
        public bool Saved { get; private set; }
        public bool Committed { get; private set; }
        public bool RollbackAttempted { get; private set; }
        public bool RollbackSucceeded { get; private set; }

        private bool _disposed;

        public PrefabMutationSession(string assetPath, AssetTextSnapshot beforeSnapshot,
            GameObject root)
        {
            AssetPath = assetPath;
            BeforeSnapshot = beforeSnapshot;
            Root = root ?? throw new ArgumentNullException(nameof(root));
        }

        public GameObject SaveAndClose(ISet<string> explicitYamlPropertyRoots = null,
            ICollection<string> warnings = null)
        {
            if (Root == null)
                throw new InvalidOperationException(
                    $"Prefab mutation session for '{AssetPath}' is already closed.");

            string absolutePath = GetAbsoluteAssetPath(AssetPath);
            byte[] beforeBytes = null;
            if (!string.IsNullOrEmpty(absolutePath) && File.Exists(absolutePath))
            {
                try
                {
                    beforeBytes = ReadAllBytesWithRetry(absolutePath);
                }
                catch (Exception ex)
                {
                    string warning =
                        $"Could not capture prefab YAML before saving '{AssetPath}': " +
                        ex.GetBaseException().Message;
                    warnings?.Add(warning);
                    Debug.LogWarning($"[VM Unity Automation] {warning}");
                }
            }

            SaveAttempted = true;
            GameObject savedRoot;
            try
            {
                savedRoot = RetryTransientFileIo(
                    () => PrefabUtility.SaveAsPrefabAsset(Root, AssetPath),
                    TransientFileIoMaxAttempts, null);
            }
            finally
            {
                CloseAuthoringRoot();
            }

            Saved = savedRoot != null;
            if (Saved && !TryStabilizePrefabYaml(AssetPath, beforeBytes,
                    explicitYamlPropertyRoots, out string stabilizationWarning))
            {
                // Whitespace/block-order stabilization is auxiliary. SaveAsPrefabAsset already
                // persisted the authoritative Unity data, so an exhausted Win32 file lock must
                // not turn a successful mutation into an unknown/failed result.
                warnings?.Add(stabilizationWarning);
                Debug.LogWarning($"[VM Unity Automation] {stabilizationWarning}");
            }

            return savedRoot;
        }

        public void Commit()
        {
            if (!Saved)
                throw new InvalidOperationException(
                    $"Prefab mutation session for '{AssetPath}' has no saved product to commit.");
            Committed = true;
        }

        public void CommitVerifiedPublication()
        {
            if (!SaveAttempted)
                throw new InvalidOperationException(
                    $"Prefab mutation session for '{AssetPath}' did not attempt publication.");
            Committed = true;
        }

        public void CloseAuthoringRoot()
        {
            if (Root == null)
                return;

            GameObject root = Root;
            PrefabUtility.UnloadPrefabContents(root);
            Root = null;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            CloseAuthoringRoot();
            if (SaveAttempted && !Committed)
            {
                RollbackAttempted = true;
                RollbackSucceeded = RestoreAssetSnapshot(BeforeSnapshot);
            }
            _disposed = true;
        }
    }


}
