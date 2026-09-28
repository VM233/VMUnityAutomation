#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    internal sealed class VmAutomationUIToolkitAuditPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            VmAutomationUIToolkitAutomaticAuditCoordinator.QueueImportedAssets(
                (importedAssets ?? new string[0]).Concat(movedAssets ?? new string[0]));
        }
    }
}
#endif
