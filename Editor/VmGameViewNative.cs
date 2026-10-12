using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    internal sealed class VmGameViewNative
    {
        internal const int MaximumPresets = 4096;
        private const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;
        private readonly Type viewType;
        private readonly object sizes;
        private readonly object group;
        private readonly Type groupType;
        private readonly Type sizeType;
        private readonly string groupName;
        private readonly int groupIndex;

        internal VmGameViewNative()
        {
            var assembly = typeof(UnityEditor.Editor).Assembly;
            viewType = assembly.GetType("UnityEditor.GameView", true);
            Type sizesType = assembly.GetType("UnityEditor.GameViewSizes", true);
            sizes = sizesType.GetProperty("instance", StaticFlags).GetValue(null);
            group = sizesType.GetProperty("currentGroup", InstanceFlags).GetValue(sizes);
            object currentGroup = sizesType.GetProperty("currentGroupType", InstanceFlags).GetValue(sizes);
            groupName = currentGroup.ToString();
            groupIndex = Convert.ToInt32(currentGroup);
            groupType = group.GetType();
            sizeType = assembly.GetType("UnityEditor.GameViewSize", true);
            if (Count > MaximumPresets)
                throw new VmProjectToolException("game_view_preset_limit", $"Game View group '{groupName}' exceeds {MaximumPresets} entries.");
        }

        internal int Count => (int)groupType.GetMethod("GetTotalCount", InstanceFlags).Invoke(group, null);
        internal int BuiltinCount => (int)groupType.GetMethod("GetBuiltinCount", InstanceFlags).Invoke(group, null);
        internal string GroupName => groupName;
        internal UnityEngine.Object Preferences => (UnityEngine.Object)sizes;
        internal string PreferencePath => (string)typeof(ScriptableSingleton<>).MakeGenericType(sizes.GetType())
            .GetMethod("GetFilePath", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Invoke(null, null);
        internal bool IsCurrentGroup => sizes.GetType().GetProperty("currentGroupType", InstanceFlags).GetValue(sizes).ToString() == groupName;
        internal object Size(int index) => groupType.GetMethod("GetGameViewSize", InstanceFlags).Invoke(group, new object[] { index });

        internal EditorWindow[] Windows()
        {
            var objects = Resources.FindObjectsOfTypeAll(viewType);
            if (objects.Length > 32)
                throw new VmProjectToolException("game_view_window_limit", "Game View operations admit at most 32 existing windows.");
            var result = new EditorWindow[objects.Length];
            for (int i = 0; i < objects.Length; i++) result[i] = (EditorWindow)objects[i];
            return result;
        }

        internal EditorWindow SingleWindow()
        {
            var windows = Windows();
            if (windows.Length != 1)
                throw new VmProjectToolException(windows.Length == 0 ? "game_view_unavailable" : "game_view_ambiguous",
                    windows.Length == 0 ? "No existing Game View is open." : "More than one existing Game View is open.");
            return windows[0];
        }

        internal object SelectedSize(EditorWindow window) => Size(SelectedIndex(window));
        internal int SelectedIndex(EditorWindow window) => IsCurrentGroup
            ? (int)viewType.GetProperty("selectedSizeIndex", InstanceFlags).GetValue(window)
            : ((int[])viewType.GetField("m_SelectedSizes", InstanceFlags).GetValue(window))[groupIndex];
        internal Vector2 Zoom(EditorWindow window) => (Vector2)viewType.GetProperty("zoomAreaScale", InstanceFlags).GetValue(window);

        internal void Select(EditorWindow window, int index)
        {
            ((int[])viewType.GetField("m_SelectedSizes", InstanceFlags).GetValue(window))[groupIndex] = index;
            if (!IsCurrentGroup) return;
            viewType.GetMethod("UpdateZoomAreaAndParent", InstanceFlags).Invoke(window, null);
            viewType.GetProperty("targetSize", InstanceFlags).SetValue(window,
                viewType.GetProperty("targetRenderSize", InstanceFlags).GetValue(window));
            window.Repaint();
        }

        internal void RestoreZoom(EditorWindow window, Vector2 scale)
        {
            object zoom = viewType.GetField("m_ZoomArea", InstanceFlags).GetValue(window);
            Type zoomType = zoom.GetType();
            Rect area = (Rect)zoomType.GetProperty("shownAreaInsideMargins", InstanceFlags).GetValue(zoom);
            zoomType.GetMethod("SetScaleFocused", InstanceFlags, null,
                new[] { typeof(Vector2), typeof(Vector2) }, null).Invoke(zoom, new object[] { area.center, scale });
            window.Repaint();
        }

        internal VmGameViewState Read(EditorWindow window)
        {
            int index = SelectedIndex(window);
            var preset = Describe(index);
            Vector2 zoom = Zoom(window);
            Vector2 render = (Vector2)viewType.GetProperty("targetRenderSize", InstanceFlags).GetValue(window);
            return new VmGameViewState
            {
                WindowInstanceId = VmObjectId.Get(window), Group = groupName, SelectedIndex = index,
                DisplayText = (string)sizeType.GetProperty("displayText", InstanceFlags).GetValue(Size(index)),
                Width = preset.Width, Height = preset.Height, RenderWidth = render.x, RenderHeight = render.y,
                ScaleX = zoom.x, ScaleY = zoom.y
            };
        }

        internal VmGameViewPreset Describe(int index)
        {
            object size = Size(index);
            return new VmGameViewPreset
            {
                Index = index, Builtin = index < BuiltinCount,
                BaseText = (string)sizeType.GetProperty("baseText", InstanceFlags).GetValue(size),
                SizeType = sizeType.GetProperty("sizeType", InstanceFlags).GetValue(size).ToString(),
                Width = (int)sizeType.GetProperty("width", InstanceFlags).GetValue(size),
                Height = (int)sizeType.GetProperty("height", InstanceFlags).GetValue(size)
            };
        }

        internal VmGameViewPresetInventory Inventory()
        {
            int count = Count;
            var presets = new VmGameViewPreset[count];
            using var bytes = new MemoryStream();
            using (var writer = new BinaryWriter(bytes, System.Text.Encoding.UTF8, true))
            {
                writer.Write(groupName);
                writer.Write(BuiltinCount);
                for (int i = 0; i < count; i++)
                {
                    var p = presets[i] = Describe(i);
                    writer.Write(p.Index); writer.Write(p.Builtin); writer.Write(p.BaseText);
                    writer.Write(p.SizeType); writer.Write(p.Width); writer.Write(p.Height);
                }
            }
            using var sha = SHA256.Create();
            string fingerprint = BitConverter.ToString(sha.ComputeHash(bytes.ToArray())).Replace("-", "").ToLowerInvariant();
            string fileHash = VmGameViewPreferencesSource.FileHash(PreferencePath);
            return new VmGameViewPresetInventory
            {
                Group = groupName, BuiltinCount = BuiltinCount, Presets = presets,
                Fingerprint = fingerprint, PreferenceFileHash = fileHash,
                PreferenceSnapshotLoaded = VmGameViewPreferencesSource.instance.Matches(Preferences, fingerprint, fileHash)
            };
        }

        internal int FindResolution(int width, int height, object excluded = null)
        {
            int count = Count;
            for (int i = 0; i < count; i++)
            {
                if (ReferenceEquals(Size(i), excluded)) continue;
                var p = Describe(i);
                if (p.SizeType == "FixedResolution" && p.Width == width && p.Height == height) return i;
            }
            return -1;
        }

        internal Dictionary<object, int> Indices()
        {
            var indices = new Dictionary<object, int>();
            int count = Count;
            for (int i = 0; i < count; i++) indices.Add(Size(i), i);
            return indices;
        }

        internal object AddTemporary(int width, int height)
        {
            if (Count == MaximumPresets)
                throw new VmProjectToolException("game_view_preset_limit", "No temporary preset slot is available.");
            object fixedResolution = Enum.Parse(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameViewSizeType", true), "FixedResolution");
            object size = Activator.CreateInstance(sizeType, fixedResolution, width, height, "Temporary resolution");
            groupType.GetMethod("AddCustomSize", InstanceFlags).Invoke(group, new[] { size });
            return size;
        }

        internal void ResizeTemporary(object size, int width, int height)
        {
            sizeType.GetProperty("width", InstanceFlags).SetValue(size, width);
            sizeType.GetProperty("height", InstanceFlags).SetValue(size, height);
            sizes.GetType().GetMethod("Changed", InstanceFlags).Invoke(sizes, null);
        }

        internal void Remove(int index) => groupType.GetMethod("RemoveCustomSize", InstanceFlags).Invoke(group, new object[] { index });
        internal void SavePreferences() => sizes.GetType().GetMethod("SaveToHDD", InstanceFlags).Invoke(sizes, null);
    }
}
