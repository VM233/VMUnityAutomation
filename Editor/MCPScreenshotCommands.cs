using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
#if UNITY_EDITOR_WIN
using System.Diagnostics;
using System.Runtime.InteropServices;
#endif
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    /// <summary>
    /// Commands for capturing screenshots from the Unity Editor.
    /// </summary>
    public static class MCPScreenshotCommands
    {
        // ─── Capture Scene View ───

        public static object CaptureSceneView(Dictionary<string, object> args)
        {
            int width = args.ContainsKey("width") ? Convert.ToInt32(args["width"]) : 1920;
            int height = args.ContainsKey("height") ? Convert.ToInt32(args["height"]) : 1080;
            if (width <= 0 || height <= 0)
                return new { error = "width and height must be greater than 0" };

            string transport = GetString(args, "transport");
            if (string.IsNullOrEmpty(transport))
                transport = "file";
            transport = transport.Trim().ToLowerInvariant();
            if (transport != "file" && transport != "base64" && transport != "both")
                return new { error = "transport must be 'file', 'base64', or 'both'" };

            bool writeFile = transport == "file" || transport == "both";
            bool returnBase64 = transport == "base64" || transport == "both";
            string path = GetString(args, "path");
            if (writeFile && string.IsNullOrEmpty(path))
                path = VmAutomationSettings.CreateDefaultScreenshotPath("SceneView");

            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
                return new { error = "No active Scene View found" };

            var camera = sceneView.camera;
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture renderTexture = null;
            Texture2D texture = null;
            try
            {
                renderTexture = new RenderTexture(width, height, 24);
                camera.targetTexture = renderTexture;
                camera.Render();

                RenderTexture.active = renderTexture;
                texture = new Texture2D(width, height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                byte[] bytes = texture.EncodeToPNG();

                var result = new Dictionary<string, object>
                {
                    { "success", true },
                    { "transport", transport },
                    { "width", width },
                    { "height", height },
                    { "sizeBytes", bytes.Length },
                };

                if (writeFile)
                {
                    string fullPath = ResolveFilePath(path);
                    string directory = Path.GetDirectoryName(fullPath);
                    if (!string.IsNullOrEmpty(directory))
                        Directory.CreateDirectory(directory);
                    File.WriteAllBytes(fullPath, bytes);
                    result["path"] = path;
                    result["fullPath"] = fullPath.Replace('\\', '/');

                    RefreshAssetIfNeeded(fullPath);
                }

                if (returnBase64)
                    result["base64"] = Convert.ToBase64String(bytes);

                return result;
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                if (texture != null)
                    UnityEngine.Object.DestroyImmediate(texture);
                if (renderTexture != null)
                    UnityEngine.Object.DestroyImmediate(renderTexture);
            }
        }

        // ─── Get Scene View Camera Info ───

        public static object GetSceneViewInfo(Dictionary<string, object> args)
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
                return new { error = "No active Scene View found" };

            var pivot = sceneView.pivot;
            var rotation = sceneView.rotation.eulerAngles;

            return new Dictionary<string, object>
            {
                { "pivot", new Dictionary<string, object>
                    {
                        { "x", pivot.x }, { "y", pivot.y }, { "z", pivot.z },
                    }
                },
                { "rotation", new Dictionary<string, object>
                    {
                        { "x", rotation.x }, { "y", rotation.y }, { "z", rotation.z },
                    }
                },
                { "size", sceneView.size },
                { "orthographic", sceneView.orthographic },
                { "is2D", sceneView.in2DMode },
                { "drawGizmos", sceneView.drawGizmos },
            };
        }

        // ─── Set Scene View Camera ───

        public static object SetSceneViewCamera(Dictionary<string, object> args)
        {
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
                return new { error = "No active Scene View found" };

            var updated = new List<string>();

            if (args.ContainsKey("pivot") && args["pivot"] is Dictionary<string, object> pivotDict)
            {
                float x = pivotDict.ContainsKey("x") ? Convert.ToSingle(pivotDict["x"]) : sceneView.pivot.x;
                float y = pivotDict.ContainsKey("y") ? Convert.ToSingle(pivotDict["y"]) : sceneView.pivot.y;
                float z = pivotDict.ContainsKey("z") ? Convert.ToSingle(pivotDict["z"]) : sceneView.pivot.z;
                sceneView.pivot = new Vector3(x, y, z);
                updated.Add("pivot");
            }

            if (args.ContainsKey("rotation") && args["rotation"] is Dictionary<string, object> rotDict)
            {
                float x = rotDict.ContainsKey("x") ? Convert.ToSingle(rotDict["x"]) : 0;
                float y = rotDict.ContainsKey("y") ? Convert.ToSingle(rotDict["y"]) : 0;
                float z = rotDict.ContainsKey("z") ? Convert.ToSingle(rotDict["z"]) : 0;
                sceneView.rotation = Quaternion.Euler(x, y, z);
                updated.Add("rotation");
            }

            if (args.ContainsKey("size"))
            {
                sceneView.size = Convert.ToSingle(args["size"]);
                updated.Add("size");
            }

            if (args.ContainsKey("orthographic"))
            {
                sceneView.orthographic = Convert.ToBoolean(args["orthographic"]);
                updated.Add("orthographic");
            }

            if (args.ContainsKey("is2D"))
            {
                sceneView.in2DMode = Convert.ToBoolean(args["is2D"]);
                updated.Add("is2D");
            }

            if (args.ContainsKey("lookAt") && args["lookAt"] is Dictionary<string, object> lookDict)
            {
                float x = lookDict.ContainsKey("x") ? Convert.ToSingle(lookDict["x"]) : 0;
                float y = lookDict.ContainsKey("y") ? Convert.ToSingle(lookDict["y"]) : 0;
                float z = lookDict.ContainsKey("z") ? Convert.ToSingle(lookDict["z"]) : 0;
                float sz = args.ContainsKey("lookAtSize") ? Convert.ToSingle(args["lookAtSize"]) : 10f;
                sceneView.LookAt(new Vector3(x, y, z), sceneView.rotation, sz);
                updated.Add("lookAt");
            }

            if (args.ContainsKey("frameSelected") && Convert.ToBoolean(args["frameSelected"]))
            {
                sceneView.FrameSelected();
                updated.Add("frameSelected");
            }

            sceneView.Repaint();

            return new Dictionary<string, object>
            {
                { "success", true },
                { "updated", updated },
            };
        }

        public static object GetGameViewInfo(Dictionary<string, object> args)
        {
            if (!TryGetGameView(out Type gameViewType, out EditorWindow gameView, out object error))
                return error;

            return BuildGameViewInfo(gameViewType, gameView);
        }

        public static object SetGameViewResolution(Dictionary<string, object> args)
        {
            int width = GetInt(args, "width", 0);
            int height = GetInt(args, "height", 0);
            if (width <= 0 || height <= 0)
                return new { error = "width and height must be greater than 0" };

            if (!TryGetGameView(out Type gameViewType, out EditorWindow gameView, out object error))
                return error;

            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            MethodInfo setCustomResolution = gameViewType.GetMethod("SetCustomResolution", flags);
            if (setCustomResolution == null)
                return new { error = "UnityEditor.GameView.SetCustomResolution was not found in this Unity version" };

            string label = GetString(args, "label");
            if (string.IsNullOrEmpty(label))
                label = $"{width}x{height}";

            setCustomResolution.Invoke(gameView, new object[] { new Vector2(width, height), label });
            gameView.Repaint();

            var result = BuildGameViewInfo(gameViewType, gameView);
            result["success"] = true;
            result["requestedWidth"] = width;
            result["requestedHeight"] = height;
            result["label"] = label;
            return result;
        }

        public static object SetGameViewScale(Dictionary<string, object> args)
        {
            string mode = GetString(args, "mode");
            if (string.IsNullOrEmpty(mode))
                mode = "value";
            mode = mode.Trim().ToLowerInvariant();
            if (mode != "value" && mode != "minimum")
                return new { error = "mode must be 'value' or 'minimum'" };

            if (!TryGetGameView(out Type gameViewType, out EditorWindow gameView, out object error))
                return error;

            float scale;
            float fallbackScale = 0f;
            if (mode == "minimum")
            {
                fallbackScale = GetFloat(args, "fallbackScale", 0.76f);
                scale = GetGameViewZoomAreaMinimumScale(gameViewType, gameView, fallbackScale);
            }
            else
            {
                if (!TryGetFloat(args, "scale", out scale))
                    return new { error = "scale is required" };

                if (scale <= 0)
                    return new { error = "scale must be greater than 0" };
            }

            if (!TrySnapGameViewZoom(gameViewType, gameView, scale, out string zoomError))
                return new { error = zoomError };

            var result = BuildGameViewInfo(gameViewType, gameView);
            result["success"] = true;
            result["mode"] = mode;
            result["requestedScale"] = scale;
            if (mode == "minimum")
            {
                result["appliedScale"] = scale;
                result["fallbackScale"] = fallbackScale;
            }
            return result;
        }

        public static object CropImage(Dictionary<string, object> args)
        {
            string sourcePath = GetString(args, "sourcePath");
            if (string.IsNullOrEmpty(sourcePath))
                return new { error = "sourcePath is required" };

            string absoluteSourcePath = ResolveFilePath(sourcePath);
            if (File.Exists(absoluteSourcePath) == false)
                return new { error = $"Image file not found at '{sourcePath}'" };

            if (!TryGetRectInt(args, out RectInt rect))
                return new { error = "rect is required with x, y, width, and height" };

            string outputPath = GetString(args, "outputPath");
            if (string.IsNullOrEmpty(outputPath))
            {
                string dir = Path.GetDirectoryName(absoluteSourcePath) ?? "";
                string name = Path.GetFileNameWithoutExtension(absoluteSourcePath);
                outputPath = Path.Combine(dir, name + "_crop.png");
            }

            string absoluteOutputPath = ResolveFilePath(outputPath);
            bool originTopLeft = GetBool(args, "originTopLeft", true);
            Texture2D source = null;
            Texture2D cropped = null;
            try
            {
                source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!source.LoadImage(File.ReadAllBytes(absoluteSourcePath)))
                    return new { error = $"Could not decode image '{sourcePath}'" };

                int x = Mathf.Clamp(rect.x, 0, source.width);
                int y = Mathf.Clamp(rect.y, 0, source.height);
                int width = Mathf.Clamp(rect.width, 0, source.width - x);
                int height = Mathf.Clamp(rect.height, 0, source.height - y);
                if (width <= 0 || height <= 0)
                    return new { error = $"Crop rect is outside image bounds. Image={source.width}x{source.height}, rect={rect}" };

                int readY = originTopLeft ? source.height - y - height : y;
                readY = Mathf.Clamp(readY, 0, source.height - height);
                cropped = new Texture2D(width, height, TextureFormat.RGBA32, false);
                cropped.SetPixels(source.GetPixels(x, readY, width, height));
                cropped.Apply();

                string directory = Path.GetDirectoryName(absoluteOutputPath);
                if (string.IsNullOrEmpty(directory) == false)
                    Directory.CreateDirectory(directory);

                byte[] png = cropped.EncodeToPNG();
                File.WriteAllBytes(absoluteOutputPath, png);
                RefreshAssetIfNeeded(absoluteOutputPath);

                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "sourcePath", sourcePath },
                    { "outputPath", outputPath },
                    { "absoluteOutputPath", absoluteOutputPath },
                    { "sourceWidth", source.width },
                    { "sourceHeight", source.height },
                    { "originTopLeft", originTopLeft },
                    { "cropRect", new Dictionary<string, object>
                        {
                            { "x", x },
                            { "y", y },
                            { "width", width },
                            { "height", height },
                        }
                    },
                    { "sizeBytes", png.Length },
                };
            }
            finally
            {
                if (source != null)
                    UnityEngine.Object.DestroyImmediate(source);
                if (cropped != null)
                    UnityEngine.Object.DestroyImmediate(cropped);
            }
        }

        private static string GetString(Dictionary<string, object> args, string key)
        {
            return args != null && args.ContainsKey(key) && args[key] != null ? args[key].ToString() : "";
        }

        private static int GetInt(Dictionary<string, object> args, string key, int defaultValue)
        {
            if (args == null || args.ContainsKey(key) == false || args[key] == null)
                return defaultValue;

            return int.TryParse(args[key].ToString(), out int value) ? value : defaultValue;
        }

        internal static bool TryReadPngInfo(string path, out int width, out int height, out string error)
        {
            width = 0;
            height = 0;
            error = "";
            Texture2D texture = null;
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                if (bytes.Length == 0)
                {
                    error = "PNG file is empty.";
                    return false;
                }

                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (texture.LoadImage(bytes, false) == false)
                {
                    error = "PNG decode failed.";
                    return false;
                }

                width = texture.width;
                height = texture.height;
                return width > 0 && height > 0;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
            finally
            {
                if (texture != null)
                    UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        private static float GetFloat(Dictionary<string, object> args, string key, float defaultValue)
        {
            return TryGetFloat(args, key, out float value) ? value : defaultValue;
        }

        private static bool TryGetFloat(Dictionary<string, object> args, string key, out float value)
        {
            value = 0;
            return args != null && args.ContainsKey(key) && args[key] != null &&
                   float.TryParse(args[key].ToString(), out value);
        }

        private static bool GetBool(Dictionary<string, object> args, string key, bool defaultValue)
        {
            if (args == null || args.ContainsKey(key) == false || args[key] == null)
                return defaultValue;

            if (args[key] is bool value)
                return value;

            return bool.TryParse(args[key].ToString(), out bool parsed) ? parsed : defaultValue;
        }

        private static bool TryGetRectInt(Dictionary<string, object> args, out RectInt rect)
        {
            rect = default(RectInt);
            if (args == null)
                return false;

            var dictionary = args;
            if (args.TryGetValue("rect", out object rectValue))
            {
                dictionary = rectValue as Dictionary<string, object>;
                if (dictionary == null)
                    return false;
            }

            if (!TryGetInt(dictionary, "x", out int x) ||
                !TryGetInt(dictionary, "y", out int y) ||
                !TryGetInt(dictionary, "width", out int width) ||
                !TryGetInt(dictionary, "height", out int height))
            {
                return false;
            }

            rect = new RectInt(x, y, width, height);
            return true;
        }

        private static bool TryGetInt(Dictionary<string, object> args, string key, out int value)
        {
            value = 0;
            return args != null && args.ContainsKey(key) && args[key] != null &&
                   int.TryParse(args[key].ToString(), out value);
        }

        private static string ResolveFilePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            string normalized = path.Replace('\\', '/');
            if (Path.IsPathRooted(path))
                return path;

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.GetFullPath(Path.Combine(projectRoot, normalized));
        }

        private static void RefreshAssetIfNeeded(string absolutePath)
        {
            string normalized = absolutePath.Replace('\\', '/');
            string assetsRoot = Application.dataPath.Replace('\\', '/');
            if (normalized.StartsWith(assetsRoot, StringComparison.OrdinalIgnoreCase))
                AssetDatabase.Refresh();
        }

        private static bool TryGetGameView(out Type gameViewType, out EditorWindow gameView, out object error)
        {
            gameViewType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            if (gameViewType == null)
            {
                gameView = null;
                error = new { error = "UnityEditor.GameView was not found in this Unity version" };
                return false;
            }

            gameView = EditorWindow.GetWindow(gameViewType);
            if (gameView == null)
            {
                error = new { error = "Could not open Unity Game View" };
                return false;
            }

            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            gameViewType.GetMethod("InitializeZoomArea", flags)?.Invoke(gameView, null);
            error = null;
            return true;
        }

        private static bool TrySnapGameViewZoom(Type gameViewType, EditorWindow gameView, float scale,
            out string error)
        {
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            MethodInfo snapZoom = gameViewType.GetMethod("SnapZoom", flags, null, new[] { typeof(float) }, null);
            if (snapZoom == null)
            {
                error = "UnityEditor.GameView.SnapZoom(float) was not found in this Unity version";
                return false;
            }

            snapZoom.Invoke(gameView, new object[] { scale });
            gameView.Repaint();
            error = null;
            return true;
        }

        private static float GetGameViewZoomAreaMinimumScale(Type gameViewType, EditorWindow gameView,
            float fallbackScale)
        {
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            FieldInfo zoomAreaField = gameViewType.GetField("m_ZoomArea", flags);
            object zoomArea = zoomAreaField?.GetValue(gameView);
            if (zoomArea == null)
                return fallbackScale;

            Type zoomAreaType = zoomArea.GetType();
            float hMin = GetZoomAreaScaleLimit(zoomAreaType, zoomArea, "m_HScaleMin", "hScaleMin", fallbackScale);
            float vMin = GetZoomAreaScaleLimit(zoomAreaType, zoomArea, "m_VScaleMin", "vScaleMin", fallbackScale);
            float minScale = Mathf.Max(hMin, vMin);

            return IsReasonableGameViewScale(minScale) ? minScale : fallbackScale;
        }

        private static float GetZoomAreaScaleLimit(Type zoomAreaType, object zoomArea, string fieldName,
            string propertyName, float fallbackScale)
        {
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            object value = zoomAreaType.GetField(fieldName, flags)?.GetValue(zoomArea);
            if (value == null)
                value = zoomAreaType.GetProperty(propertyName, flags)?.GetValue(zoomArea);

            if (value == null)
                return fallbackScale;

            try
            {
                float scale = Convert.ToSingle(value);
                return IsReasonableGameViewScale(scale) ? scale : fallbackScale;
            }
            catch
            {
                return fallbackScale;
            }
        }

        private static bool IsReasonableGameViewScale(float scale)
        {
            return !float.IsNaN(scale) && !float.IsInfinity(scale) && scale >= 0.05f && scale <= 10f;
        }

        private static Dictionary<string, object> BuildGameViewInfo(Type gameViewType, EditorWindow gameView)
        {
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            object targetRenderSize = gameViewType.GetProperty("targetRenderSize", flags)?.GetValue(gameView);
            object zoomAreaScale = gameViewType.GetProperty("zoomAreaScale", flags)?.GetValue(gameView);
            object selectedSizeIndex = gameViewType.GetProperty("selectedSizeIndex", flags)?.GetValue(gameView);
            object currentSizeGroupType = gameViewType.GetProperty("currentSizeGroupType", flags)?.GetValue(gameView);
            object currentGameViewSize = gameViewType.GetProperty("currentGameViewSize", flags)?.GetValue(gameView);

            string displayText = "";
            if (currentGameViewSize != null)
            {
                displayText = currentGameViewSize.GetType()
                    .GetProperty("displayText", flags)
                    ?.GetValue(currentGameViewSize)
                    ?.ToString() ?? "";
            }

            float fallbackScale = 0.76f;
            return new Dictionary<string, object>
            {
                { "gameViewTitle", gameView.titleContent != null ? gameView.titleContent.text : gameView.name },
                { "selectedSizeIndex", selectedSizeIndex != null ? selectedSizeIndex.ToString() : "" },
                { "currentSizeGroupType", currentSizeGroupType != null ? currentSizeGroupType.ToString() : "" },
                { "displayText", displayText },
                { "targetRenderSize", targetRenderSize != null ? targetRenderSize.ToString() : "" },
                { "scale", zoomAreaScale != null ? zoomAreaScale.ToString() : "" },
                { "minScale", GetGameViewZoomAreaMinimumScale(gameViewType, gameView, fallbackScale) },
            };
        }

        // ─── Capture an arbitrary EditorWindow (Inspector, Project, custom windows…) ───
        // Unlike Game/Scene view (which ARE cameras and can be captured from a RenderTexture),
        // a general EditorWindow has no public render target. Most windows use Win32 PrintWindow
        // for an occlusion-proof capture. GPU-composited surfaces such as UI Builder's viewport
        // require an on-screen desktop capture after temporarily raising the host window.
        // PLATFORM: Windows editor only — these Win32 paths have no macOS/Linux equivalent.
        //
        // args: window (required — EditorWindow type FullName e.g. "UnityEditor.InspectorWindow",
        //       simple type name, or tab title); path (optional — default project screenshot directory,
        //       any user-chosen .png path is honoured); maxDimension (optional, default 8192);
        //       captureMode (optional: auto, print-window, or screen).
        public static object CaptureEditorWindow(Dictionary<string, object> args)
        {
#if UNITY_EDITOR_WIN
            string window = args != null && args.ContainsKey("window") ? args["window"].ToString()
                          : args != null && args.ContainsKey("typeOrTitle") ? args["typeOrTitle"].ToString() : "";
            if (string.IsNullOrEmpty(window))
                return Err("'window' is required (EditorWindow type FullName e.g. 'UnityEditor.InspectorWindow', simple type name, or tab title).");

            string path = args != null && args.ContainsKey("path") ? args["path"].ToString() : "";
            if (string.IsNullOrEmpty(path))
                path = VmAutomationSettings.CreateDefaultScreenshotPath("EditorWindow");
            if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                return Err("path must end in .png");
            int maxDimension = args != null && args.ContainsKey("maxDimension") ? Convert.ToInt32(args["maxDimension"]) : 8192;

            var win = FindWindow(window, out int matchCount);
            if (win == null)
                return Err(matchCount > 1
                    ? "Ambiguous: " + matchCount + " windows match '" + window + "'. Pass the exact type FullName."
                    : "No EditorWindow matches '" + window + "'.");

            string captureMode = ResolveEditorWindowCaptureMode(
                args != null && args.ContainsKey("captureMode") ? args["captureMode"]?.ToString() : "auto",
                win.GetType().FullName ?? win.GetType().Name,
                win.titleContent?.text ?? win.name);
            if (string.IsNullOrEmpty(captureMode))
            {
                return Err("captureMode must be 'auto', 'print-window', or 'screen'.");
            }

            var (pid, main) = ProcInfo();
            if (main == IntPtr.Zero) return Err("Could not resolve the main editor window handle.");

            bool floating = IsFloating(win);
            bool restoreFocus = false;
            EditorWindow prevFocus = EditorWindow.focusedWindow;
            try
            {
                // Only the docked path needs the tab activated; a floating window is captured by
                // its own HWND, so it is never raised/focused (keeps the occlusion-proof promise).
                if (!floating)
                {
                    win.Focus();
                    restoreFocus = true;
                }
                win.Repaint();
                RepaintImmediately(win);

                IntPtr hwnd; bool whole; int px = 0, py = 0, pw = 0, ph = 0;
                float pixelsPerPoint = Math.Max(1f, EditorGUIUtility.pixelsPerPoint);
                if (floating)
                {
                    hwnd = FindHwndByTitleExact(win, pid, main, out int n);
                    if (hwnd == IntPtr.Zero)
                    {
                        if (n > 1)
                            return Err("Ambiguous floating-window HWND (" + n + " matches).");

                        win.Focus();
                        restoreFocus = true;
                        floating = false;
                        hwnd = main;
                        whole = false;
                        var rp = win.position;
                        px = (int)Math.Round(rp.x); py = (int)Math.Round(rp.y);
                        pw = (int)Math.Round(rp.width); ph = (int)Math.Round(rp.height);
                        if (pw <= 0 || ph <= 0) return Err("Bad panel rect " + pw + "x" + ph);
                    }
                    else
                    {
                        whole = true;
                    }
                }
                else
                {
                    hwnd = main; whole = false;
                    var rp = win.position;
                    px = (int)Math.Round(rp.x); py = (int)Math.Round(rp.y);
                    pw = (int)Math.Round(rp.width); ph = (int)Math.Round(rp.height);
                    if (pw <= 0 || ph <= 0) return Err("Bad panel rect " + pw + "x" + ph);
                }

                return GrabAndEncode(hwnd, whole, px, py, pw, ph, pixelsPerPoint, path, maxDimension, win,
                    floating, captureMode);
            }
            finally
            {
                // Restore the user's previously-focused tab (only the docked path changed it).
                if (restoreFocus && prevFocus != null && prevFocus != win)
                    try { prevFocus.Focus(); } catch { }
            }
#else
            return new Dictionary<string, object>
            {
                { "success", false },
                { "error", "CaptureEditorWindow is Windows-only: it uses Win32 window or desktop capture APIs, which have no macOS/Linux equivalent. For the game or scene view use screenshot/game or screenshot/scene (camera-based, cross-platform)." },
                { "platform", Application.platform.ToString() },
            };
#endif
        }

        private static RectInt MapPanelRectToHostClientCapture(Rect panelRect, Rect hostRect,
            RectInt clientRectInCapture)
        {
            if (panelRect.width <= 0 || panelRect.height <= 0 ||
                hostRect.width <= 0 || hostRect.height <= 0 ||
                clientRectInCapture.width <= 0 || clientRectInCapture.height <= 0)
                return new RectInt();

            float scaleX = clientRectInCapture.width / hostRect.width;
            float scaleY = clientRectInCapture.height / hostRect.height;
            return new RectInt(
                clientRectInCapture.x + Mathf.RoundToInt((panelRect.x - hostRect.x) * scaleX),
                clientRectInCapture.y + Mathf.RoundToInt((panelRect.y - hostRect.y) * scaleY),
                Mathf.Max(1, Mathf.RoundToInt(panelRect.width * scaleX)),
                Mathf.Max(1, Mathf.RoundToInt(panelRect.height * scaleY)));
        }

        private static string ResolveEditorWindowCaptureMode(string requestedMode, string fullTypeName, string title)
        {
            string normalized = (requestedMode ?? "auto").Trim().ToLowerInvariant();
            switch (normalized)
            {
                case "":
                case "auto":
                    return RequiresScreenCapture(fullTypeName, title) ? "screen" : "print-window";
                case "print":
                case "printwindow":
                case "print-window":
                case "offscreen":
                    return "print-window";
                case "desktop":
                case "onscreen":
                case "on-screen":
                case "screen":
                    return "screen";
                default:
                    return "";
            }
        }

        private static bool RequiresScreenCapture(string fullTypeName, string title)
        {
            string type = fullTypeName ?? "";
            string windowTitle = title ?? "";
            return string.Equals(type, "UnityEditor.GameView", StringComparison.Ordinal) ||
                   type.IndexOf("Unity.UI.Builder", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   type.IndexOf("UIBuilder", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   string.Equals(windowTitle, "UI Builder", StringComparison.OrdinalIgnoreCase);
        }

#if UNITY_EDITOR_WIN
        static Dictionary<string, object> Err(string msg, int win32 = 0)
        {
            var d = new Dictionary<string, object> { { "success", false }, { "error", msg } };
            if (win32 != 0) d["win32Error"] = win32;
            return d;
        }

        // All GDI handles and the Texture2D are released in finally (deselect-before-delete).
        static Dictionary<string, object> GrabAndEncode(IntPtr hwnd, bool wholeWindow,
            int panelX, int panelY, int panelW, int panelH,
            float pixelsPerPoint, string path, int maxDimension, EditorWindow win, bool floating,
            string captureMode)
        {
            if (IsIconic(hwnd)) return Err("Window is minimized.");
            if (!GetWindowRect(hwnd, out RECT wr)) return Err("GetWindowRect failed.", Marshal.GetLastWin32Error());
            int winW = wr.right - wr.left, winH = wr.bottom - wr.top;
            if (winW <= 0 || winH <= 0) return Err("Bad window rect " + winW + "x" + winH);

            int cropX, cropY, cropW, cropH;
            string cropWarning = "";
            string coordinateMode = "whole-window";
            if (wholeWindow) { cropX = 0; cropY = 0; cropW = winW; cropH = winH; }
            else
            {
                var candidates = new List<CropCandidate>();
                if (IsFloating(win) == false &&
                    TryGetHostContainerPosition(win, out Rect hostPosition) &&
                    GetClientRect(hwnd, out RECT hostClientRect))
                {
                    var hostClientOrigin = new POINT
                    {
                        x = hostClientRect.left,
                        y = hostClientRect.top,
                    };
                    if (ClientToScreen(hwnd, ref hostClientOrigin))
                    {
                        var clientRectInCapture = new RectInt(
                            hostClientOrigin.x - wr.left,
                            hostClientOrigin.y - wr.top,
                            hostClientRect.right - hostClientRect.left,
                            hostClientRect.bottom - hostClientRect.top);
                        RectInt mapped = MapPanelRectToHostClientCapture(win.position, hostPosition,
                            clientRectInCapture);
                        candidates.Add(new CropCandidate(mapped.x, mapped.y, mapped.width, mapped.height,
                            "host-client-relative"));
                    }
                }

                candidates.Add(new CropCandidate(panelX - wr.left, panelY - wr.top, panelW, panelH,
                    "screen-pixels"));
                candidates.Add(new CropCandidate(panelX, panelY, panelW, panelH,
                    "window-local-pixels"));
                if (Math.Abs(pixelsPerPoint - 1f) > 0.01f)
                {
                    int scaledX = (int)Math.Round(panelX * pixelsPerPoint);
                    int scaledY = (int)Math.Round(panelY * pixelsPerPoint);
                    int scaledW = (int)Math.Round(panelW * pixelsPerPoint);
                    int scaledH = (int)Math.Round(panelH * pixelsPerPoint);
                    candidates.Add(new CropCandidate(scaledX - wr.left, scaledY - wr.top, scaledW, scaledH,
                        "screen-points-scaled"));
                    candidates.Add(new CropCandidate(scaledX, scaledY, scaledW, scaledH,
                        "window-local-points-scaled"));
                }

                var selected = candidates.FirstOrDefault(candidate => candidate.HasValidOrigin(winW, winH));
                if (selected.Width <= 0 || selected.Height <= 0)
                    return Err("Panel rect could not be mapped into the captured window; DPI / multi-monitor mismatch.");

                cropX = selected.X;
                cropY = selected.Y;
                cropW = selected.Width;
                cropH = selected.Height;
                coordinateMode = selected.Mode;
                if (coordinateMode != "screen-pixels" && coordinateMode != "host-client-relative")
                    cropWarning = "EditorWindow crop used " + coordinateMode + " coordinate fallback.";

                if (cropX + cropW > winW) cropW = winW - cropX;
                if (cropY + cropH > winH) cropH = winH - cropY;
                if (cropW <= 0 || cropH <= 0) return Err("Bad crop " + cropW + "x" + cropH);
            }

            int contentX = 0;
            int contentY = 0;
            int contentW = cropW;
            int contentH = cropH;
            if (wholeWindow)
            {
                if (GetClientRect(hwnd, out RECT clientRect))
                {
                    var clientOrigin = new POINT { x = clientRect.left, y = clientRect.top };
                    if (ClientToScreen(hwnd, ref clientOrigin))
                    {
                        contentX = Math.Max(0, clientOrigin.x - wr.left - cropX);
                        contentY = Math.Max(0, clientOrigin.y - wr.top - cropY);
                        contentW = Math.Min(clientRect.right - clientRect.left, cropW - contentX);
                        contentH = Math.Min(clientRect.bottom - clientRect.top, cropH - contentY);
                    }
                    else
                    {
                        cropWarning = AppendWarning(cropWarning,
                            "Could not map the floating window client area; contentRect falls back to the full capture.");
                    }
                }
                else
                {
                    cropWarning = AppendWarning(cropWarning,
                        "Could not read the floating window client area; contentRect falls back to the full capture.");
                }
            }

            if (contentW <= 0 || contentH <= 0)
            {
                contentX = 0;
                contentY = 0;
                contentW = cropW;
                contentH = cropH;
                cropWarning = AppendWarning(cropWarning,
                    "The mapped client area was empty; contentRect falls back to the full capture.");
            }

            // Bound dimensions before allocating: int-overflow guard (long math) + GPU limit.
            int maxTex = SystemInfo.maxTextureSize; if (maxTex <= 0) maxTex = 8192;
            int cap = Math.Min(maxDimension > 0 ? maxDimension : int.MaxValue, maxTex);
            if (cropW > cap || cropH > cap) return Err("Too large " + cropW + "x" + cropH + " (cap " + cap + ")");
            long need = (long)cropW * cropH * 4L;
            if (need > int.MaxValue) return Err("Capture buffer too large (" + need + " B)");

            ScreenCaptureWindowState screenCaptureState = default;
            string screenCaptureWarning = "";
            bool captureFromScreen = string.Equals(captureMode, "screen", StringComparison.Ordinal);
            if (captureFromScreen)
            {
                screenCaptureState = PrepareWindowForScreenCapture(hwnd, out screenCaptureWarning);
            }

            IntPtr hScreen = IntPtr.Zero, hMemFull = IntPtr.Zero, hBmpFull = IntPtr.Zero, oldFull = IntPtr.Zero;
            IntPtr hMemCrop = IntPtr.Zero, hBmpCrop = IntPtr.Zero, oldCrop = IntPtr.Zero;
            try
            {
                hScreen = GetDC(IntPtr.Zero); if (hScreen == IntPtr.Zero) return Err("GetDC failed.", Marshal.GetLastWin32Error());
                hMemFull = CreateCompatibleDC(hScreen); if (hMemFull == IntPtr.Zero) return Err("CreateCompatibleDC failed.", Marshal.GetLastWin32Error());
                hBmpFull = CreateCompatibleBitmap(hScreen, winW, winH); if (hBmpFull == IntPtr.Zero) return Err("CreateCompatibleBitmap failed.", Marshal.GetLastWin32Error());
                oldFull = SelectObject(hMemFull, hBmpFull);

                if (captureFromScreen)
                {
                    if (!BitBlt(hMemFull, 0, 0, winW, winH, hScreen, wr.left, wr.top,
                            SRCCOPY | CAPTUREBLT))
                    {
                        return Err("On-screen window capture failed.", Marshal.GetLastWin32Error());
                    }
                }
                else if (!PrintWindow(hwnd, hMemFull, PW_RENDERFULLCONTENT))
                {
                    return Err("PrintWindow failed.", Marshal.GetLastWin32Error());
                }

                IntPtr grabBmp;
                if (wholeWindow)
                {
                    // GetDIBits needs the bitmap deselected; oldFull is NOT nulled so the finally
                    // re-asserts the deselect even if this SelectObject fails (else DeleteObject leaks).
                    SelectObject(hMemFull, oldFull);
                    grabBmp = hBmpFull;
                }
                else
                {
                    hMemCrop = CreateCompatibleDC(hScreen); if (hMemCrop == IntPtr.Zero) return Err("CreateCompatibleDC(2) failed.", Marshal.GetLastWin32Error());
                    hBmpCrop = CreateCompatibleBitmap(hScreen, cropW, cropH); if (hBmpCrop == IntPtr.Zero) return Err("CreateCompatibleBitmap(2) failed.", Marshal.GetLastWin32Error());
                    oldCrop = SelectObject(hMemCrop, hBmpCrop);
                    if (!BitBlt(hMemCrop, 0, 0, cropW, cropH, hMemFull, cropX, cropY, SRCCOPY)) return Err("BitBlt failed.", Marshal.GetLastWin32Error());
                    SelectObject(hMemCrop, oldCrop);
                    grabBmp = hBmpCrop;
                }

                var bmi = new BITMAPINFOHEADER
                {
                    biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                    biWidth = cropW, biHeight = cropH, // positive = bottom-up, matches Texture2D row order
                    biPlanes = 1, biBitCount = 32, biCompression = 0
                };
                byte[] buf = new byte[cropW * cropH * 4];
                int scan = GetDIBits(hScreen, grabBmp, 0, (uint)cropH, buf, ref bmi, DIB_RGB_COLORS);
                if (scan == 0) return Err("GetDIBits failed.", Marshal.GetLastWin32Error());
                if (scan != cropH) return Err("GetDIBits partial (" + scan + "/" + cropH + ").");

                // All-black detection: aligned RGB sample.
                long sum = 0; int stride = Math.Max(1, (cropW * cropH) / 4096) * 4;
                for (int i = 0; i + 2 < buf.Length; i += stride) sum += buf[i] + buf[i + 1] + buf[i + 2];
                if (sum == 0)
                {
                    return Err(captureFromScreen
                        ? "All-black frame from on-screen window capture."
                        : "All-black frame (GPU refused PW_RENDERFULLCONTENT).");
                }

                AnalyzeCenterPixels(buf, cropW, cropH, out int centerColorRange,
                    out int centerDistinctColorBuckets, out bool centerVisuallyBlank);

                byte[] png = EncodeBgraBottomUp(buf, cropW, cropH);
                if (png == null || png.Length == 0) return Err("PNG encode failed.");

                string dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllBytes(path, png);
                string normalized = path.Replace('\\', '/');
                if (normalized.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) || normalized.Contains("/Assets/"))
                    AssetDatabase.Refresh();

                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "path", path },
                    { "width", cropW },
                    { "height", cropH },
                    { "sizeBytes", png.Length },
                    { "window", win.GetType().FullName },
                    { "floating", floating },
                    { "captureMethod", captureFromScreen ? "screen-bitmap" : "print-window" },
                    { "coordinateMode", coordinateMode },
                    { "contentRect", new Dictionary<string, object>
                        {
                            { "x", contentX },
                            { "y", contentY },
                            { "width", contentW },
                            { "height", contentH },
                        }
                    },
                    { "centerColorRange", centerColorRange },
                    { "centerDistinctColorBuckets", centerDistinctColorBuckets },
                    { "centerVisuallyBlank", centerVisuallyBlank },
                    { "warning", AppendWarning(cropWarning, screenCaptureWarning) },
                };
            }
            finally
            {
                if (oldCrop != IntPtr.Zero && hMemCrop != IntPtr.Zero) SelectObject(hMemCrop, oldCrop);
                if (hBmpCrop != IntPtr.Zero) DeleteObject(hBmpCrop);
                if (hMemCrop != IntPtr.Zero) DeleteDC(hMemCrop);
                if (oldFull != IntPtr.Zero && hMemFull != IntPtr.Zero) SelectObject(hMemFull, oldFull);
                if (hBmpFull != IntPtr.Zero) DeleteObject(hBmpFull);
                if (hMemFull != IntPtr.Zero) DeleteDC(hMemFull);
                if (hScreen != IntPtr.Zero) ReleaseDC(IntPtr.Zero, hScreen);
                if (captureFromScreen)
                    RestoreWindowAfterScreenCapture(hwnd, screenCaptureState);
            }
        }

        private readonly struct ScreenCaptureWindowState
        {
            public readonly IntPtr PreviousForegroundWindow;
            public readonly bool WasTopMost;
            public readonly bool Raised;

            public ScreenCaptureWindowState(IntPtr previousForegroundWindow, bool wasTopMost, bool raised)
            {
                PreviousForegroundWindow = previousForegroundWindow;
                WasTopMost = wasTopMost;
                Raised = raised;
            }
        }

        private static ScreenCaptureWindowState PrepareWindowForScreenCapture(IntPtr hwnd, out string warning)
        {
            warning = "";
            IntPtr previousForegroundWindow = GetForegroundWindow();
            long extendedStyle = GetWindowLongPtrCompat(hwnd, GWL_EXSTYLE).ToInt64();
            bool wasTopMost = (extendedStyle & WS_EX_TOPMOST) != 0;
            bool raised = wasTopMost || SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
            if (raised == false)
            {
                warning = AppendWarning(warning,
                    "Could not temporarily raise the window for GPU-composited capture.");
            }

            if (BringWindowToTop(hwnd) == false)
                warning = AppendWarning(warning, "BringWindowToTop failed.");
            if (SetForegroundWindow(hwnd) == false)
                warning = AppendWarning(warning, "SetForegroundWindow failed.");
            UpdateWindow(hwnd);
            if (DwmFlush() != 0)
                warning = AppendWarning(warning, "DwmFlush failed before capture.");

            return new ScreenCaptureWindowState(previousForegroundWindow, wasTopMost, raised);
        }

        private static void RestoreWindowAfterScreenCapture(IntPtr hwnd, ScreenCaptureWindowState state)
        {
            if (state.Raised && state.WasTopMost == false)
            {
                SetWindowPos(hwnd, HWND_NOTOPMOST, 0, 0, 0, 0,
                    SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            }

            if (state.PreviousForegroundWindow != IntPtr.Zero &&
                state.PreviousForegroundWindow != hwnd)
            {
                SetForegroundWindow(state.PreviousForegroundWindow);
            }

            DwmFlush();
        }

        private static IntPtr GetWindowLongPtrCompat(IntPtr hwnd, int index)
        {
            return IntPtr.Size == 8
                ? GetWindowLongPtr64(hwnd, index)
                : new IntPtr(GetWindowLong32(hwnd, index));
        }

        private readonly struct CropCandidate
        {
            public readonly int X;
            public readonly int Y;
            public readonly int Width;
            public readonly int Height;
            public readonly string Mode;

            public CropCandidate(int x, int y, int width, int height, string mode)
            {
                X = x;
                Y = y;
                Width = width;
                Height = height;
                Mode = mode;
            }

            public bool HasValidOrigin(int windowWidth, int windowHeight)
            {
                return Width > 0 && Height > 0 && X >= 0 && Y >= 0 && X < windowWidth && Y < windowHeight;
            }
        }

        private static bool TryGetHostContainerPosition(EditorWindow win, out Rect position)
        {
            position = default;
            if (win == null)
                return false;

            try
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                var parentField = typeof(EditorWindow).GetField("m_Parent", flags);
                object parent = parentField?.GetValue(win);
                if (parent == null)
                    return false;

                Type parentType = parent.GetType();
                object container = parentType.GetProperty("window", flags)?.GetValue(parent, null) ??
                                   parentType.GetField("window", flags)?.GetValue(parent) ??
                                   parentType.GetField("m_Window", flags)?.GetValue(parent);
                if (container == null)
                    return false;

                object value = container.GetType().GetProperty("position", flags)?.GetValue(container, null);
                if (value is not Rect rect || rect.width <= 0 || rect.height <= 0)
                    return false;

                position = rect;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string AppendWarning(string existing, string warning)
        {
            if (string.IsNullOrEmpty(existing))
                return warning ?? "";
            if (string.IsNullOrEmpty(warning))
                return existing;
            return existing + " " + warning;
        }

        private static void AnalyzeCenterPixels(byte[] bgra, int width, int height, out int colorRange,
            out int distinctColorBuckets, out bool visuallyBlank)
        {
            int minLuminance = 255;
            int maxLuminance = 0;
            var buckets = new HashSet<int>();
            int minX = width / 5;
            int maxX = Math.Max(minX + 1, width * 4 / 5);
            int minY = height / 5;
            int maxY = Math.Max(minY + 1, height * 4 / 5);
            int stepX = Math.Max(1, (maxX - minX) / 64);
            int stepY = Math.Max(1, (maxY - minY) / 64);

            for (int y = minY; y < maxY; y += stepY)
            {
                for (int x = minX; x < maxX; x += stepX)
                {
                    int offset = (y * width + x) * 4;
                    int blue = bgra[offset];
                    int green = bgra[offset + 1];
                    int red = bgra[offset + 2];
                    int luminance = (red * 3 + green * 6 + blue) / 10;
                    minLuminance = Math.Min(minLuminance, luminance);
                    maxLuminance = Math.Max(maxLuminance, luminance);
                    buckets.Add((red >> 4) << 8 | (green >> 4) << 4 | (blue >> 4));
                }
            }

            colorRange = maxLuminance - minLuminance;
            distinctColorBuckets = buckets.Count;
            visuallyBlank = colorRange <= 6 && distinctColorBuckets <= 4;
        }

        // GDI 32bpp DIB is BGRA; Texture2D wants RGBA + bottom-up rows (our positive biHeight).
        // Alpha is forced opaque (PrintWindow leaves it garbage). Texture released in finally.
        static byte[] EncodeBgraBottomUp(byte[] bgra, int w, int h)
        {
            var cols = new Color32[w * h];
            for (int i = 0; i < cols.Length; i++) { int o = i * 4; cols[i] = new Color32(bgra[o + 2], bgra[o + 1], bgra[o], 255); }
            Texture2D tex = null;
            try { tex = new Texture2D(w, h, TextureFormat.RGBA32, false); tex.SetPixels32(cols); tex.Apply(false); return tex.EncodeToPNG(); }
            finally { if (tex != null) UnityEngine.Object.DestroyImmediate(tex); }
        }

        // Exact FullName → exact simple type name → exact title → unambiguous substring.
        static EditorWindow FindWindow(string typeOrTitle, out int matchCount)
        {
            matchCount = 0;
            if (string.IsNullOrEmpty(typeOrTitle)) return null;
            var wins = Resources.FindObjectsOfTypeAll<EditorWindow>();
            foreach (var w in wins) if (w != null && w.GetType().FullName == typeOrTitle) { matchCount = 1; return w; }

            EditorWindow nameHit = null; int nameN = 0;
            foreach (var w in wins) if (w != null && string.Equals(w.GetType().Name, typeOrTitle, StringComparison.Ordinal)) { nameN++; if (nameHit == null) nameHit = w; }
            if (nameN >= 1) { matchCount = nameN; return nameN == 1 ? nameHit : null; }

            EditorWindow exact = null; int exactN = 0;
            foreach (var w in wins) { if (w == null) continue; var t = w.titleContent != null ? w.titleContent.text : w.name; if (!string.IsNullOrEmpty(t) && string.Equals(t, typeOrTitle, StringComparison.OrdinalIgnoreCase)) { exactN++; if (exact == null) exact = w; } }
            if (exactN >= 1) { matchCount = exactN; return exactN == 1 ? exact : null; }

            EditorWindow sub = null; int subN = 0;
            foreach (var w in wins) { if (w == null) continue; var t = w.titleContent != null ? w.titleContent.text : w.name; if (!string.IsNullOrEmpty(t) && t.IndexOf(typeOrTitle, StringComparison.OrdinalIgnoreCase) >= 0) { subN++; if (sub == null) sub = w; } }
            matchCount = subN; return subN == 1 ? sub : null;
        }

        // EditorWindow.docked is internal → reflected, guarded. Unknown ⇒ docked (main + crop).
        static bool IsFloating(EditorWindow win)
        {
            try { var p = typeof(EditorWindow).GetProperty("docked", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public); if (p != null && p.GetValue(win, null) is bool d) return !d; } catch { }
            return false;
        }

        // EditorWindow.RepaintImmediately is internal → reflected, guarded best-effort (H3).
        internal static void RepaintImmediately(EditorWindow win)
        {
            try { var m = typeof(EditorWindow).GetMethod("RepaintImmediately", BindingFlags.Instance | BindingFlags.NonPublic); if (m != null) m.Invoke(win, null); } catch { }
        }

        static (int pid, IntPtr main) ProcInfo()
        {
            using (var p = Process.GetCurrentProcess()) return (p.Id, p.MainWindowHandle);
        }

        // A floating EditorWindow's OS window carries its tab title (or type name). Exact match,
        // excludes the main editor HWND, reports the count so ambiguity is surfaced not guessed.
        static IntPtr FindHwndByTitleExact(EditorWindow win, int pid, IntPtr main, out int count)
        {
            string t1 = win.titleContent != null ? win.titleContent.text : null;
            string t2 = win.GetType().Name;
            IntPtr found = IntPtr.Zero; int n = 0;
            EnumWindows((h, l) =>
            {
                // Never let a managed exception unwind through the native frame.
                try
                {
                    if (h == main) return true;
                    if (!IsWindowVisible(h)) return true;
                    GetWindowThreadProcessId(h, out uint wp);
                    if (wp != (uint)pid) return true;
                    int len = GetWindowTextLength(h); if (len <= 0) return true;
                    var sb = new System.Text.StringBuilder(len + 1);
                    GetWindowText(h, sb, sb.Capacity);
                    string title = sb.ToString();
                    if ((t1 != null && string.Equals(title, t1, StringComparison.OrdinalIgnoreCase)) || string.Equals(title, t2, StringComparison.OrdinalIgnoreCase))
                    { n++; if (found == IntPtr.Zero) found = h; }
                }
                catch { }
                return true;
            }, IntPtr.Zero);
            count = n; return n == 1 ? found : IntPtr.Zero;
        }

        // ── Win32 P/Invoke + GDI types (Windows editor only) ──
        [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);
        [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] static extern bool GetWindowRect(IntPtr hWnd, out RECT r);
        [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] static extern bool GetClientRect(IntPtr hWnd, out RECT r);
        [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] static extern bool ClientToScreen(IntPtr hWnd, ref POINT point);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] static extern bool BringWindowToTop(IntPtr hWnd);
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] static extern bool UpdateWindow(IntPtr hWnd);
        [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int width, int height,
            uint flags);
        [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
        static extern int GetWindowLong32(IntPtr hWnd, int index);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
        static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int index);
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] static extern bool EnumWindows(EnumWindowsProc cb, IntPtr l);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] static extern bool IsIconic(IntPtr hWnd);
        [DllImport("user32.dll", SetLastError = true)] static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder s, int max);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowTextLength(IntPtr hWnd);
        delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("gdi32.dll", SetLastError = true)] static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll", SetLastError = true)] static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int w, int h);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);
        [DllImport("gdi32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] static extern bool BitBlt(IntPtr hdcDest, int xD, int yD, int w, int h, IntPtr hdcSrc, int xS, int yS, uint rop);
        [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] static extern bool DeleteObject(IntPtr ho);
        [DllImport("gdi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll", SetLastError = true)] static extern int GetDIBits(IntPtr hdc, IntPtr hbm, uint start, uint cLines, byte[] bits, ref BITMAPINFOHEADER bmi, uint usage);
        [DllImport("dwmapi.dll")] static extern int DwmFlush();

        const uint SRCCOPY = 0x00CC0020;
        const uint CAPTUREBLT = 0x40000000;
        const uint PW_RENDERFULLCONTENT = 0x00000002; // Windows 8.1+
        const uint DIB_RGB_COLORS = 0;
        const int GWL_EXSTYLE = -20;
        const long WS_EX_TOPMOST = 0x00000008L;
        const uint SWP_NOSIZE = 0x0001;
        const uint SWP_NOMOVE = 0x0002;
        const uint SWP_NOACTIVATE = 0x0010;
        const uint SWP_SHOWWINDOW = 0x0040;
        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        [StructLayout(LayoutKind.Sequential)] struct RECT { public int left, top, right, bottom; }
        [StructLayout(LayoutKind.Sequential)] struct POINT { public int x, y; }
        [StructLayout(LayoutKind.Sequential)]
        struct BITMAPINFOHEADER
        {
            public uint biSize; public int biWidth; public int biHeight;
            public ushort biPlanes; public ushort biBitCount; public uint biCompression;
            public uint biSizeImage; public int biXPPM; public int biYPPM;
            public uint biClrUsed; public uint biClrImportant;
        }
#endif
    }
}
