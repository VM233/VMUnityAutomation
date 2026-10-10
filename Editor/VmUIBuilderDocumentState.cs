using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace VMUnityAutomation.Editor
{
    internal sealed class VmUIBuilderDocumentState
    {
        internal readonly UnityEngine.Object Document;
        internal readonly VisualTreeAsset Asset;
        internal readonly string Path;
        internal readonly bool HasUnsavedChanges;
        internal readonly bool WindowHasUnsavedChanges;

        internal VmUIBuilderDocumentState(UnityEngine.Object document, VisualTreeAsset asset,
            string path, bool unsaved, bool windowUnsaved)
        {
            Document = document;
            Asset = asset;
            Path = path ?? "";
            HasUnsavedChanges = unsaved;
            WindowHasUnsavedChanges = windowUnsaved;
        }

        internal Dictionary<string, object> Product() => new Dictionary<string, object>
        {
            { "documentInstanceId", VmObjectId.Get(Document) },
            { "assetPath", Path },
            { "assetGuid", Path.Length == 0 ? "" : AssetDatabase.AssetPathToGUID(Path) },
            { "assetInstanceId", Asset == null ? null : VmObjectId.Get(Asset) },
            { "hasUnsavedChanges", HasUnsavedChanges },
            { "windowHasUnsavedChanges", WindowHasUnsavedChanges },
        };
    }
}
