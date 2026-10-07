using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

#if UNITY_EDITOR_WIN
namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationEditorViewCapture
    {
        private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        internal static object Capture(EditorWindow window, bool floating, string path, int maxDimension)
        {
            EditorWindow previousFocus = EditorWindow.focusedWindow;
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture target = null;
            Texture2D texture = null;
            try
            {
                window.Focus();
                var host = (ScriptableObject)typeof(EditorWindow).GetField("m_Parent", Flags).GetValue(window);
                if (host == null || host.GetType().GetProperty("actualView", Flags).GetValue(host) != window)
                    return VmAutomationResponse.Error("The requested EditorWindow is not the native host's actual view.",
                        "target_view_unverified");

                Type viewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GUIView", true);
                float scale = (float)viewType.GetMethod("GetBackingScaleFactor", Flags).Invoke(host, null);
                var viewRect = (Rect)host.GetType().GetProperty("position", Flags).GetValue(host);
                int width = Mathf.RoundToInt(viewRect.width * scale);
                int height = Mathf.RoundToInt(viewRect.height * scale);
                int cap = Math.Min(maxDimension, SystemInfo.maxTextureSize);
                if (width <= 0 || height <= 0 || width > cap || height > cap)
                    return VmAutomationResponse.Error($"Native Editor view size {width}x{height} exceeds the capture contract (cap {cap}).",
                        "invalid_arguments");

                target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
                texture = new Texture2D(width, height, TextureFormat.RGB24, false);
                VmAutomationScreenshotCommands.RepaintImmediately(window);
                viewType.GetMethod("GrabPixels", Flags).Invoke(host,
                    new object[] { target, new Rect(0, 0, width, height) });
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                byte[] pixels = texture.GetRawTextureData<byte>().ToArray();
                bool sourceUVStartsAtTop = SystemInfo.graphicsUVStartsAtTop;
                NormalizeReadbackRows(pixels, width, height, sourceUVStartsAtTop);
                texture.LoadRawTextureData(pixels);
                VmAutomationScreenshotCommands.AnalyzeCenterPixels(pixels, width, height,
                    out int colorRange, out int buckets, out bool blank, true);
                byte[] png = texture.EncodeToPNG();
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllBytes(path, png);
                string normalized = path.Replace('\\', '/');
                if (normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
                    normalized.Contains("/Assets/"))
                    AssetDatabase.Refresh();

                return new Dictionary<string, object>
                {
                    { "success", true }, { "path", path }, { "window", window.GetType().FullName },
                    { "floating", floating }, { "width", width }, { "height", height }, { "sizeBytes", png.Length },
                    { "captureMethod", "editor-view" }, { "targetWindowVerified", true },
                    { "coordinateMode", "view-local" },
                    { "captureGeometry", new Dictionary<string, object>
                        {
                            { "editorWindowInstanceId", VmObjectId.Get(window) },
                            { "hostViewInstanceId", VmObjectId.Get(host) },
                            { "viewRect", new[] { 0f, 0f, viewRect.width, viewRect.height } },
                            { "pixelsPerPoint", scale },
                            { "sourceUVStartsAtTop", sourceUVStartsAtTop },
                        }
                    },
                    { "contentRect", new Dictionary<string, object>
                        {
                            { "x", Mathf.RoundToInt(window.rootVisualElement.worldBound.x * scale) },
                            { "y", Mathf.RoundToInt(window.rootVisualElement.worldBound.y * scale) },
                            { "width", Mathf.RoundToInt(window.rootVisualElement.worldBound.width * scale) },
                            { "height", Mathf.RoundToInt(window.rootVisualElement.worldBound.height * scale) },
                        }
                    },
                    { "centerColorRange", colorRange }, { "centerDistinctColorBuckets", buckets },
                    { "centerVisuallyBlank", blank }, { "warning", "" },
                };
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (texture != null)
                    UnityEngine.Object.DestroyImmediate(texture);
                if (target != null)
                    RenderTexture.ReleaseTemporary(target);
                if (previousFocus != null && previousFocus != window)
                    previousFocus.Focus();
            }
        }

        internal static void NormalizeReadbackRows(byte[] pixels, int width, int height, bool topOrigin)
        {
            if (!topOrigin)
                return;
            int rowSize = width * 3;
            var row = new byte[rowSize];
            for (int y = 0; y < height / 2; y++)
            {
                int first = y * rowSize;
                int last = (height - 1 - y) * rowSize;
                Buffer.BlockCopy(pixels, first, row, 0, rowSize);
                Buffer.BlockCopy(pixels, last, pixels, first, rowSize);
                Buffer.BlockCopy(row, 0, pixels, last, rowSize);
            }
        }
    }
}
#endif
