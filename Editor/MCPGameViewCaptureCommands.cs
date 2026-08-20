using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    /// <summary>
    /// Owns the Game View evidence product: runtime preconditions, editor-overlay
    /// suppression, frame readiness, PNG validation, and presentation restoration.
    /// </summary>
    public static class MCPGameViewCaptureCommands
    {
        private const BindingFlags GameViewMemberFlags = BindingFlags.Instance |
                                                         BindingFlags.Public |
                                                         BindingFlags.NonPublic;

        public static void CaptureGameView(Dictionary<string, object> args,
            Action<object> resolve)
        {
            args ??= new Dictionary<string, object>();
            if (MCPRuntimePreconditions.TryRequirePlayMode(
                    "screenshot/game",
                    "Game View capture needs a live rendered runtime frame",
                    out var preconditionError) == false)
            {
                resolve(preconditionError);
                return;
            }

            string editorOverlayMode = GetString(
                    args, "editorOverlays", "suppress")
                .Trim().ToLowerInvariant();
            if (editorOverlayMode != "suppress" &&
                editorOverlayMode != "preserve")
            {
                resolve(MCPResponse.Error(
                    "editorOverlays must be 'suppress' or 'preserve'.",
                    "invalid_arguments"));
                return;
            }

            GameViewPresentationScope presentation;
            if (editorOverlayMode == "preserve")
            {
                presentation = GameViewPresentationScope.CreatePreserving();
            }
            else if (GameViewPresentationScope.TryCreate(
                         out presentation, out object presentationError) == false)
            {
                resolve(presentationError);
                return;
            }

            string path = args.ContainsKey("path") ? args["path"].ToString() : "";
            if (string.IsNullOrEmpty(path))
            {
                path = VmAutomationSettings.CreateDefaultScreenshotPath("GameView");
            }

            int superSize = Math.Max(1, args.ContainsKey("superSize")
                ? Convert.ToInt32(args["superSize"])
                : 1);
            int waitFrames = Math.Max(1, GetInt(args, "waitFrames", 2));
            int stableFrames = Math.Max(1, GetInt(args, "stableFrames", 2));
            int timeoutMs = Math.Max(1000, GetInt(args, "timeoutMs", 10000));

            string fullPath = Path.GetFullPath(path);
            string dir = Path.GetDirectoryName(fullPath);
            try
            {
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                if (File.Exists(fullPath))
                    File.Delete(fullPath);
            }
            catch (Exception ex)
            {
                CompleteCapture(new Dictionary<string, object>
                {
                    { "success", false },
                    { "error", $"Could not prepare screenshot path '{path}': {ex.Message}" },
                }, presentation, resolve);
                return;
            }

            if (EditorApplication.isPaused)
            {
                object pausedResult;
                try
                {
                    pausedResult = CapturePausedGameViewCore(path, fullPath, superSize);
                }
                catch (Exception ex)
                {
                    pausedResult = MCPResponse.Error(
                        $"Could not capture the paused Game View: {ex.Message}",
                        "game_view_capture_failed");
                }

                CompleteCapture(pausedResult, presentation, resolve);
                return;
            }

            int frame = 0;
            int stableFileFrames = 0;
            long lastSize = -1;
            bool captureRequested = false;
            bool resolved = false;
            double startedAt = EditorApplication.timeSinceStartup;

            void Finish(object result)
            {
                if (resolved)
                    return;

                resolved = true;
                EditorApplication.update -= Tick;
                CompleteCapture(result, presentation, resolve);
            }

            void Tick()
            {
                try
                {
                    frame++;
                    double elapsedMs =
                        (EditorApplication.timeSinceStartup - startedAt) * 1000d;
                    if (elapsedMs >= timeoutMs)
                    {
                        Finish(new Dictionary<string, object>
                        {
                            { "success", false },
                            { "error", $"Timed out waiting for Game View screenshot '{path}'." },
                            { "path", path },
                            { "elapsedMs", Math.Round(elapsedMs, 2) },
                            { "captureRequested", captureRequested },
                            { "fileExists", File.Exists(fullPath) },
                            { "stableFileFrames", stableFileFrames },
                        });
                        return;
                    }

                    if (captureRequested == false)
                    {
                        if (frame < waitFrames)
                        {
                            EditorApplication.QueuePlayerLoopUpdate();
                            return;
                        }

                        ScreenCapture.CaptureScreenshot(path, superSize);
                        captureRequested = true;
                        EditorApplication.QueuePlayerLoopUpdate();
                        return;
                    }

                    if (File.Exists(fullPath))
                    {
                        long size = new FileInfo(fullPath).Length;
                        if (size > 0 && size == lastSize)
                            stableFileFrames++;
                        else
                            stableFileFrames = 0;
                        lastSize = size;

                        if (stableFileFrames >= stableFrames &&
                            MCPScreenshotCommands.TryReadPngInfo(
                                fullPath, out int width, out int height, out _))
                        {
                            Finish(new Dictionary<string, object>
                            {
                                { "success", true },
                                { "path", path },
                                { "fullPath", fullPath.Replace('\\', '/') },
                                { "superSize", superSize },
                                { "width", width },
                                { "height", height },
                                { "sizeBytes", size },
                                { "waitFrames", waitFrames },
                                { "stableFrames", stableFrames },
                                { "elapsedMs", Math.Round(elapsedMs, 2) },
                                { "fileReady", true },
                            });
                            return;
                        }
                    }

                    EditorApplication.QueuePlayerLoopUpdate();
                }
                catch (Exception ex)
                {
                    Finish(MCPResponse.Error(
                        $"Game View capture failed before producing valid evidence: {ex.Message}",
                        "game_view_capture_failed"));
                }
            }

            EditorApplication.update += Tick;
        }

        public static object CaptureGameView(Dictionary<string, object> args)
        {
            return new Dictionary<string, object>
            {
                { "success", false },
                { "error", "screenshot/game must be executed through the deferred route." },
            };
        }

        internal static Dictionary<string, object> CaptureGameViewRenderTexture(
            string path, int superSize = 1)
        {
            if (GameViewPresentationScope.TryCreate(out var presentation,
                    out object presentationError) == false)
            {
                return presentationError as Dictionary<string, object> ??
                       MCPResponse.Error(presentationError?.ToString() ??
                                         "Could not sanitize Game View presentation.",
                           "game_view_presentation_unavailable");
            }

            Dictionary<string, object> result;
            try
            {
                result = CaptureGameViewRenderTextureCore(
                    path, Path.GetFullPath(path), superSize);
            }
            finally
            {
                presentation.Dispose();
            }

            AttachPresentationFacts(result, presentation);
            FailIfPresentationWasNotRestored(result, presentation);
            return result;
        }

        internal static Dictionary<string, object> WriteRenderTexturePng(
            RenderTexture source, string fullPath, int superSize,
            bool flipVertically)
        {
            if (source == null || source.IsCreated() == false)
                throw new InvalidOperationException("The source render texture is unavailable.");

            superSize = Math.Max(1, superSize);
            long requestedWidth = (long)source.width * superSize;
            long requestedHeight = (long)source.height * superSize;
            int maxTextureSize =
                SystemInfo.maxTextureSize > 0 ? SystemInfo.maxTextureSize : 8192;
            if (requestedWidth > maxTextureSize || requestedHeight > maxTextureSize)
            {
                throw new InvalidOperationException(
                    $"Requested screenshot size {requestedWidth}x{requestedHeight} exceeds the GPU texture limit {maxTextureSize}.");
            }

            int width = (int)requestedWidth;
            int height = (int)requestedHeight;
            RenderTexture scaledTexture = null;
            RenderTexture readTexture = source;
            RenderTexture previousActive = RenderTexture.active;
            Texture2D image = null;
            try
            {
                if (superSize > 1)
                {
                    scaledTexture = RenderTexture.GetTemporary(
                        width, height, 0, RenderTextureFormat.ARGB32);
                    scaledTexture.filterMode = FilterMode.Bilinear;
                    Graphics.Blit(source, scaledTexture);
                    readTexture = scaledTexture;
                }

                RenderTexture.active = readTexture;
                image = new Texture2D(width, height, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply(false, false);

                if (flipVertically)
                {
                    Color32[] pixels = image.GetPixels32();
                    FlipPixelsVertically(pixels, width, height);
                    image.SetPixels32(pixels);
                    image.Apply(false, false);
                }

                byte[] png = image.EncodeToPNG();
                if (png == null || png.Length == 0)
                    throw new InvalidOperationException("PNG encoding returned no data.");

                string directory = Path.GetDirectoryName(fullPath);
                if (string.IsNullOrEmpty(directory) == false)
                    Directory.CreateDirectory(directory);
                File.WriteAllBytes(fullPath, png);

                if (MCPScreenshotCommands.TryReadPngInfo(
                        fullPath, out int decodedWidth, out int decodedHeight,
                        out string decodeError) == false)
                {
                    throw new InvalidOperationException(
                        $"Written PNG could not be decoded: {decodeError}");
                }

                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "width", decodedWidth },
                    { "height", decodedHeight },
                    { "sizeBytes", png.Length },
                };
            }
            finally
            {
                RenderTexture.active = previousActive;
                if (image != null)
                    UnityEngine.Object.DestroyImmediate(image);
                if (scaledTexture != null)
                    RenderTexture.ReleaseTemporary(scaledTexture);
            }
        }

        internal static void FlipPixelsVertically(
            Color32[] pixels, int width, int height)
        {
            if (pixels == null)
                throw new ArgumentNullException(nameof(pixels));
            if (width <= 0 || height <= 0 || pixels.Length != width * height)
            {
                throw new ArgumentException(
                    "Pixel buffer dimensions do not match its length.",
                    nameof(pixels));
            }

            var row = new Color32[width];
            for (int y = 0; y < height / 2; y++)
            {
                int oppositeY = height - 1 - y;
                Array.Copy(pixels, y * width, row, 0, width);
                Array.Copy(pixels, oppositeY * width, pixels, y * width, width);
                Array.Copy(row, 0, pixels, oppositeY * width, width);
            }
        }

        private static object CapturePausedGameViewCore(
            string path, string fullPath, int superSize)
        {
            double startedAt = EditorApplication.timeSinceStartup;
            var result = CaptureGameViewRenderTextureCore(path, fullPath, superSize);
            result["waitFrames"] = 0;
            result["stableFrames"] = 0;
            result["elapsedMs"] = Math.Round(
                (EditorApplication.timeSinceStartup - startedAt) * 1000d, 2);
            result["fileReady"] = GetBool(result, "success", false);
            result["paused"] = true;
            return result;
        }

        private static Dictionary<string, object> CaptureGameViewRenderTextureCore(
            string path, string fullPath, int superSize)
        {
            Type gameViewType =
                typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            if (gameViewType == null)
            {
                return MCPResponse.Error(
                    "Game View render-texture capture is unavailable because UnityEditor.GameView could not be resolved.",
                    "game_view_render_texture_unavailable", false,
                    new Dictionary<string, object>
                    {
                        { "path", path },
                        { "paused", EditorApplication.isPaused },
                    });
            }

            EditorWindow focusedWindow = EditorWindow.focusedWindow;
            EditorWindow[] gameViews = Resources.FindObjectsOfTypeAll(gameViewType)
                .OfType<EditorWindow>().ToArray();
            EditorWindow gameView =
                gameViews.FirstOrDefault(window => window == focusedWindow) ??
                gameViews.FirstOrDefault();
            if (gameView == null)
            {
                return MCPResponse.Error(
                    "Game View render-texture capture requires an open Game View window.",
                    "game_view_render_texture_unavailable", false,
                    new Dictionary<string, object>
                    {
                        { "path", path },
                        { "paused", EditorApplication.isPaused },
                    });
            }

            gameView.Repaint();
            MCPScreenshotCommands.RepaintImmediately(gameView);
            FieldInfo renderTextureField =
                gameViewType.GetField("m_RenderTexture", GameViewMemberFlags);
            var renderTexture =
                renderTextureField?.GetValue(gameView) as RenderTexture;
            if (renderTexture == null || renderTexture.IsCreated() == false)
            {
                return MCPResponse.Error(
                    "The Game View does not have a completed render texture yet.",
                    "game_view_render_texture_unavailable", true,
                    new Dictionary<string, object>
                    {
                        { "path", path },
                        { "paused", EditorApplication.isPaused },
                        { "gameViewType", gameViewType.FullName },
                    });
            }

            try
            {
                Dictionary<string, object> result = WriteRenderTexturePng(
                    renderTexture, fullPath, superSize,
                    SystemInfo.graphicsUVStartsAtTop);
                result["path"] = path;
                result["fullPath"] = fullPath.Replace('\\', '/');
                result["superSize"] = superSize;
                result["paused"] = EditorApplication.isPaused;
                result["window"] = gameViewType.FullName;
                result["floating"] = false;
                result["coordinateMode"] = "render-texture";
                result["captureMethod"] = "game-view-render-texture";
                result["contentRect"] = new Dictionary<string, object>
                {
                    { "x", 0 },
                    { "y", 0 },
                    { "width", result["width"] },
                    { "height", result["height"] },
                };
                result["warning"] = "";
                return result;
            }
            catch (Exception ex)
            {
                return MCPResponse.Error(
                    $"Could not capture the Game View render texture: {ex.Message}",
                    "game_view_render_texture_capture_failed", false,
                    new Dictionary<string, object>
                    {
                        { "path", path },
                        { "fullPath", fullPath.Replace('\\', '/') },
                        { "paused", EditorApplication.isPaused },
                    });
            }
        }

        private static void CompleteCapture(object result,
            GameViewPresentationScope presentation, Action<object> resolve)
        {
            presentation.Dispose();
            AttachPresentationFacts(result, presentation);
            if (result is Dictionary<string, object> dictionary)
                FailIfPresentationWasNotRestored(dictionary, presentation);
            resolve(result);
        }

        private static void AttachPresentationFacts(object result,
            GameViewPresentationScope presentation)
        {
            if (result is not IDictionary<string, object> dictionary)
                return;

            dictionary["editorOverlayMode"] = presentation.SuppressionApplied
                ? "suppress"
                : "preserve";
            dictionary["editorOverlaysSuppressed"] =
                presentation.SuppressionApplied;
            dictionary["gameViewGizmosSuppressed"] =
                presentation.SuppressionApplied;
            dictionary["gameViewStatsSuppressed"] =
                presentation.SuppressionApplied;
            dictionary["sanitizedGameViewCount"] = presentation.ViewCount;
            dictionary["editorOverlayStateRestored"] =
                presentation.RestorationSucceeded;
        }

        private static void FailIfPresentationWasNotRestored(
            IDictionary<string, object> result,
            GameViewPresentationScope presentation)
        {
            if (presentation.RestorationSucceeded)
                return;

            result["success"] = false;
            result["errorCode"] = "game_view_presentation_restore_failed";
            result["error"] =
                "The screenshot completed, but the previous Game View Gizmos or Stats state could not be restored.";
            result["retryable"] = false;
        }

        private static int GetInt(
            IReadOnlyDictionary<string, object> args, string key, int fallback)
        {
            return args != null && args.TryGetValue(key, out object value) &&
                   value != null && int.TryParse(value.ToString(), out int parsed)
                ? parsed
                : fallback;
        }

        private static bool GetBool(
            IReadOnlyDictionary<string, object> args, string key, bool fallback)
        {
            return args != null && args.TryGetValue(key, out object value) &&
                   value != null && bool.TryParse(value.ToString(), out bool parsed)
                ? parsed
                : fallback;
        }

        private static string GetString(
            IReadOnlyDictionary<string, object> args, string key, string fallback)
        {
            return args != null && args.TryGetValue(key, out object value) &&
                   value != null
                ? value.ToString()
                : fallback;
        }

        private sealed class GameViewPresentationScope : IDisposable
        {
            private readonly FieldInfo gizmosField;
            private readonly FieldInfo statsField;
            private readonly ViewState[] states;
            private readonly bool suppressionApplied;

            private GameViewPresentationScope(
                FieldInfo gizmosField, FieldInfo statsField, ViewState[] states,
                bool suppressionApplied)
            {
                this.gizmosField = gizmosField;
                this.statsField = statsField;
                this.states = states;
                this.suppressionApplied = suppressionApplied;
            }

            internal int ViewCount => states.Length;
            internal bool IsDisposed { get; private set; }
            internal bool SuppressionApplied => suppressionApplied;
            internal bool RestorationSucceeded { get; private set; }

            internal static GameViewPresentationScope CreatePreserving()
            {
                return new GameViewPresentationScope(
                    null, null, Array.Empty<ViewState>(), false)
                {
                    RestorationSucceeded = true,
                };
            }

            internal static bool TryCreate(
                out GameViewPresentationScope scope, out object error)
            {
                scope = null;
                Type gameViewType =
                    typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
                if (gameViewType == null)
                {
                    error = MCPResponse.Error(
                        "Cannot produce unobstructed Game View evidence because UnityEditor.GameView is unavailable.",
                        "game_view_presentation_unavailable");
                    return false;
                }

                FieldInfo gizmos =
                    gameViewType.GetField("m_Gizmos", GameViewMemberFlags);
                FieldInfo stats =
                    gameViewType.GetField("m_Stats", GameViewMemberFlags);
                if (gizmos?.FieldType != typeof(bool) ||
                    stats?.FieldType != typeof(bool))
                {
                    error = MCPResponse.Error(
                        "Cannot guarantee unobstructed Game View evidence because Gizmos or Stats state is not controllable in this Unity version.",
                        "game_view_presentation_unavailable");
                    return false;
                }

                EditorWindow[] views = Resources.FindObjectsOfTypeAll(gameViewType)
                    .OfType<EditorWindow>().ToArray();
                if (views.Length == 0)
                {
                    error = MCPResponse.Error(
                        "Cannot produce unobstructed Game View evidence because no Game View window is open.",
                        "game_view_presentation_unavailable");
                    return false;
                }

                var viewStates = new List<ViewState>(views.Length);
                try
                {
                    foreach (EditorWindow view in views)
                    {
                        var state = new ViewState(
                            view,
                            (bool)gizmos.GetValue(view),
                            (bool)stats.GetValue(view));
                        viewStates.Add(state);
                        gizmos.SetValue(view, false);
                        stats.SetValue(view, false);
                        view.Repaint();
                    }

                    EditorApplication.QueuePlayerLoopUpdate();
                    scope = new GameViewPresentationScope(
                        gizmos, stats, viewStates.ToArray(), true);
                    error = null;
                    return true;
                }
                catch (Exception ex)
                {
                    Restore(gizmos, stats, viewStates);
                    error = MCPResponse.Error(
                        $"Could not sanitize Game View presentation: {ex.Message}",
                        "game_view_presentation_unavailable");
                    return false;
                }
            }

            public void Dispose()
            {
                if (IsDisposed)
                    return;

                IsDisposed = true;
                if (suppressionApplied == false)
                {
                    RestorationSucceeded = true;
                    return;
                }

                RestorationSucceeded =
                    Restore(gizmosField, statsField, states);
            }

            private static bool Restore(
                FieldInfo gizmos, FieldInfo stats, IEnumerable<ViewState> values)
            {
                bool restored = true;
                foreach (ViewState state in values)
                {
                    if (state.View == null)
                        continue;

                    try
                    {
                        gizmos.SetValue(state.View, state.Gizmos);
                        stats.SetValue(state.View, state.Stats);
                        state.View.Repaint();
                    }
                    catch
                    {
                        restored = false;
                    }
                }

                return restored;
            }

            private readonly struct ViewState
            {
                internal ViewState(EditorWindow view, bool gizmos, bool stats)
                {
                    View = view;
                    Gizmos = gizmos;
                    Stats = stats;
                }

                internal EditorWindow View { get; }
                internal bool Gizmos { get; }
                internal bool Stats { get; }
            }
        }
    }
}
