using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace VMUnityAutomation.Editor
{
    internal sealed class VmUIBuilderCanvasSettingsScope : IDisposable
    {
        private const BindingFlags PublicInstance = BindingFlags.Instance | BindingFlags.Public;
        private const int MaximumSnapshotBytes = 16384;
        private VmUIBuilderDocumentBinding binding;
        private VisualTreeAsset asset;
        private EditorWindow window;
        private UnityEngine.Object document;
        private PropertyInfo settingsProperty, viewportProperty;
        private MethodInfo restoreViewport, saveDocument;
        private string originalJson, settingsPath;
        private byte[] originalFile;
        private bool loadStarted;
        internal RestorationReceipt Receipt { get; } = new RestorationReceipt();

        internal void Capture(VmUIBuilderDocumentBinding owner, string path, VisualTreeAsset requested)
        {
            binding = owner; asset = requested;
            var documentType = owner.WindowType.GetProperty("document", PublicInstance).PropertyType;
            settingsProperty = documentType.GetProperty("settings", PublicInstance)
                ?? throw new MissingMemberException(documentType.FullName, "settings");
            var settingsType = settingsProperty.PropertyType;
            var create = settingsType.GetMethod("CreateOrLoadSettingsObject", BindingFlags.Static | BindingFlags.Public,
                null, new[] { settingsType, typeof(string) }, null)
                ?? throw new MissingMethodException(settingsType.FullName, "CreateOrLoadSettingsObject");
            var constants = owner.WindowType.Assembly.GetType("Unity.UI.Builder.BuilderConstants", true);
            var folderProperty = constants.GetProperty("builderDocumentDiskSettingsJsonFolderAbsolutePath", BindingFlags.Static | BindingFlags.Public)
                ?? throw new MissingMemberException(constants.FullName, "builderDocumentDiskSettingsJsonFolderAbsolutePath");
            string folder = Path.GetFullPath((string)folderProperty.GetValue(null));
            string library = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Library")) + Path.DirectorySeparatorChar;
            if (!folder.StartsWith(library, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The native Builder settings folder must belong to this project's Library.");
            settingsPath = Path.Combine(folder, AssetDatabase.AssetPathToGUID(path) + ".json");
            Receipt.SettingsPath = settingsPath;
            Receipt.FileExisted = File.Exists(settingsPath);
            originalFile = Receipt.FileExisted ? ReadBounded(settingsPath) : null;
            originalJson = Snapshot(create.Invoke(null, new object[] { null, path }));
            viewportProperty = owner.WindowType.GetProperty("viewport", PublicInstance)
                ?? throw new MissingMemberException(owner.WindowType.FullName, "viewport");
            restoreViewport = viewportProperty.PropertyType.GetMethod("SetViewFromDocumentSetting", PublicInstance,
                null, Type.EmptyTypes, null)
                ?? throw new MissingMethodException(viewportProperty.PropertyType.FullName, "SetViewFromDocumentSetting");
            saveDocument = documentType.GetMethod("SaveToDisk", PublicInstance, null, Type.EmptyTypes, null)
                ?? throw new MissingMethodException(documentType.FullName, "SaveToDisk");
        }

        internal void BeforeLoad(EditorWindow selected, VmUIBuilderDocumentState before, bool created)
        {
            window = selected; document = before.Document;
            if (!created && before.Asset == asset) originalJson = Snapshot(settingsProperty.GetValue(document));
            loadStarted = true;
        }

        public void Dispose()
        {
            if (!loadStarted) return;
            Receipt.Attempted = true;
            Exception nativeFailure = null;
            try
            {
                Receipt.ActiveDocumentMatches = window != null && binding.Document(window) == document && binding.Asset(document) == asset;
                if (Receipt.ActiveDocumentMatches)
                {
                    var settings = settingsProperty.GetValue(document);
                    RestoreNativeSettings(settings, originalJson,
                        () => restoreViewport.Invoke(viewportProperty.GetValue(window), null));
                    saveDocument.Invoke(document, null);
                    Receipt.NativeSettingsRestored = true;
                }
            }
            catch (Exception failure) { nativeFailure = failure; }
            try
            {
                RestoreFile(settingsPath, originalFile);
                Receipt.FileRestored = FileMatches(settingsPath, originalFile);
                if (!Receipt.FileRestored) throw new IOException("The native Builder settings file did not restore exactly.");
            }
            catch (Exception fileFailure)
            {
                if (nativeFailure != null) throw new AggregateException(nativeFailure, fileFailure);
                throw;
            }
            if (nativeFailure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(nativeFailure).Throw();
        }

        internal static void RestoreNativeSettings(object settings, string json, Action publish)
        {
            EditorJsonUtility.FromJsonOverwrite(json, settings);
            publish();
            if (!string.Equals(Snapshot(settings), json, StringComparison.Ordinal))
                throw new InvalidOperationException("The native Builder changed the restored canvas settings.");
        }

        private static string Snapshot(object settings)
        {
            string json = EditorJsonUtility.ToJson(settings, true);
            if (System.Text.Encoding.UTF8.GetByteCount(json) > MaximumSnapshotBytes)
                throw new InvalidOperationException("The native Builder settings exceed the16KiB snapshot contract.");
            return json;
        }
        private static byte[] ReadBounded(string path)
        {
            if (new FileInfo(path).Length > MaximumSnapshotBytes)
                throw new InvalidOperationException("The native Builder settings file exceeds the16KiB snapshot contract.");
            return File.ReadAllBytes(path);
        }
        internal static void RestoreFile(string path, byte[] original)
        {
            if (original == null) File.Delete(path);
            else File.WriteAllBytes(path, original);
        }
        internal static bool FileMatches(string path, byte[] original)
        {
            if (original == null) return !File.Exists(path);
            if (!File.Exists(path)) return false;
            byte[] current = ReadBounded(path);
            return System.Linq.Enumerable.SequenceEqual(current, original);
        }
        internal sealed class RestorationReceipt
        {
            [VmJsonProperty("settingsPath")] public string SettingsPath { get; set; } = "";
            [VmJsonProperty("fileExisted")] public bool FileExisted { get; set; }
            [VmJsonProperty("attempted")] public bool Attempted { get; set; }
            [VmJsonProperty("activeDocumentMatches")] public bool ActiveDocumentMatches { get; set; }
            [VmJsonProperty("nativeSettingsRestored")] public bool NativeSettingsRestored { get; set; }
            [VmJsonProperty("fileRestored")] public bool FileRestored { get; set; }
            [VmJsonProperty("restoreError")] public string RestoreError { get; set; } = "";
        }
    }
}
