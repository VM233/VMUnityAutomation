using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    internal sealed class VmGameViewPreferencesSource : ScriptableSingleton<VmGameViewPreferencesSource>
    {
        [NonSerialized] private UnityEngine.Object preferenceObject;
        [NonSerialized] private string tableFingerprint;
        [NonSerialized] private string preferenceFileHash;

        internal bool Matches(UnityEngine.Object source, string table, string file) =>
            preferenceObject == source && tableFingerprint == table && preferenceFileHash == file;

        internal static string FileHash(string path)
        {
            if (!File.Exists(path)) return null;
            if (new FileInfo(path).Length > 4 * 1024 * 1024)
                throw new VmProjectToolException("game_view_preference_file_limit", "Game View preference files admit at most 4 MiB.");
            using var sha = SHA256.Create();
            using var stream = File.OpenRead(path);
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        internal VmGameViewPresetInventory Reload()
        {
            if (VmGameViewResolutionSession.instance.Active)
                throw new VmProjectToolException("game_view_session_active", "End the resolution session before reloading global preferences.");
            var previous = new VmGameViewNative();
            var before = previous.Inventory();
            if (before.PreferenceFileHash == null)
                throw new VmProjectToolException("game_view_preference_file_missing", "There is no saved global Game View preference file to reload.");
            var views = previous.Windows();
            var selections = new VmGameViewPreset[views.Length];
            var zooms = new Vector2[views.Length];
            for (int i = 0; i < views.Length; i++)
            {
                selections[i] = previous.Describe(previous.SelectedIndex(views[i]));
                zooms[i] = previous.Zoom(views[i]);
            }
            Type singleton = typeof(ScriptableSingleton<>).MakeGenericType(previous.Preferences.GetType());
            FieldInfo nativeInstance = singleton.GetField("s_Instance", BindingFlags.Static | BindingFlags.NonPublic);
            UnityEngine.Object[] loaded = null;
            VmGameViewNative current;
            int[] selectedIndices;
            try
            {
                nativeInstance.SetValue(null, null);
                loaded = InternalEditorUtility.LoadSerializedFileAndForget(previous.PreferencePath);
                if (loaded.Length != 1 || loaded[0].GetType() != previous.Preferences.GetType() ||
                    !ReferenceEquals(nativeInstance.GetValue(null), loaded[0]))
                    throw new VmProjectToolException("game_view_preference_load_failed", "The native preference file did not publish exactly one GameViewSizes owner.");
                current = new VmGameViewNative();
                var inventory = current.Inventory();
                if (inventory.PreferenceFileHash != before.PreferenceFileHash)
                    throw new VmProjectToolException("game_view_presets_changed", "Global preferences changed during their native reload.");
                selectedIndices = new int[views.Length];
                for (int i = 0; i < views.Length; i++)
                    selectedIndices[i] = MatchSelection(before.Presets, selections[i], inventory.Presets);
            }
            catch
            {
                nativeInstance.SetValue(null, previous.Preferences);
                if (loaded != null)
                    foreach (var item in loaded) UnityEngine.Object.DestroyImmediate(item);
                throw;
            }
            for (int i = 0; i < views.Length; i++)
            {
                current.Select(views[i], selectedIndices[i]);
                current.RestoreZoom(views[i], zooms[i]);
            }
            UnityEngine.Object.DestroyImmediate(previous.Preferences);
            var result = current.Inventory();
            preferenceObject = current.Preferences;
            tableFingerprint = result.Fingerprint;
            preferenceFileHash = result.PreferenceFileHash;
            result.PreferenceSnapshotLoaded = true;
            return result;
        }

        private static int MatchSelection(VmGameViewPreset[] previous, VmGameViewPreset selected, VmGameViewPreset[] current)
        {
            int occurrence = 0;
            for (int i = 0; i < selected.Index; i++)
                if (SameRecord(previous[i], selected)) occurrence++;
            foreach (var item in current)
            {
                if (!SameRecord(item, selected)) continue;
                if (occurrence-- == 0) return item.Index;
            }
            throw new VmProjectToolException("game_view_selection_removed", "A selected preset was removed from the saved preferences. Select a surviving preset explicitly before reloading.");
        }

        private static bool SameRecord(VmGameViewPreset left, VmGameViewPreset right) =>
            left.Builtin == right.Builtin && left.BaseText == right.BaseText && left.SizeType == right.SizeType &&
            left.Width == right.Width && left.Height == right.Height;
    }
}
