using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using static VMUnityAutomation.Editor.VmAutomationUICommandArguments;
using static VMUnityAutomation.Editor.VmAutomationUIToolkitAssetCommands;
using static VMUnityAutomation.Editor.VmAutomationUIToolkitElementUtility;

namespace VMUnityAutomation.Editor
{
    public static class VmAutomationUIBuilderPreviewCommands
    {
    public static void OpenUIBuilderPreview(Dictionary<string, object> args, Action<object> resolve)
    {
        string uxmlPath = NormalizeAssetPath(GetString(args, "uxmlPath"), "");
        if (string.IsNullOrEmpty(uxmlPath))
        {
            resolve(new { error = "uxmlPath is required" });
            return;
        }

        var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.VisualTreeAsset>(uxmlPath);
        if (asset == null)
        {
            resolve(new { error = $"VisualTreeAsset not found at '{uxmlPath}'" });
            return;
        }

        var previousFocus = EditorWindow.focusedWindow;
        bool opened = AssetDatabase.OpenAsset(asset);
        int waitFrames = Math.Max(1, GetInt(args, "waitFrames", 8));
        int stableFrames = Math.Max(1, GetInt(args, "stableFrames", 2));
        int timeoutMs = Math.Max(1000, GetInt(args, "timeoutMs", 10000));
        bool capture = GetBool(args, "capture", true);
        bool autoMatchGameView = GetBool(args, "autoMatchGameView", true);
        bool requireContentFit = GetBool(args, "requireContentFit", true);
        string screenshotPath = GetString(args, "screenshotPath");
        if (string.IsNullOrEmpty(screenshotPath))
        {
            string safeName = Path.GetFileNameWithoutExtension(uxmlPath).Replace(' ', '_');
            screenshotPath = VmAutomationSettings.CreateDefaultScreenshotPath(
                "UIBuilder_" + safeName);
        }

        int frame = 0;
        int readyFrameCount = 0;
        double startedAt = EditorApplication.timeSinceStartup;
        bool resolved = false;
        bool canvasAdjustmentAttempted = false;
        bool canvasAdjustmentApplied = false;
        bool initialMatchGameView = false;
        bool initialMatchGameViewKnown = false;
        float initialCanvasWidth = 0;
        float initialCanvasHeight = 0;
        float initialRequiredCanvasWidth = 0;
        float initialRequiredCanvasHeight = 0;
        int canvasAdjustmentFrame = -1;
        string canvasAdjustmentError = "";

        void Finish(Dictionary<string, object> result)
        {
            if (resolved)
                return;

            resolved = true;
            EditorApplication.update -= Tick;
            if (previousFocus != null && previousFocus != FindUIBuilderWindow())
            {
                try
                {
                    previousFocus.Focus();
                }
                catch
                {
                }
            }

            resolve(result);
        }

        void Tick()
        {
            frame++;
            int repainted = MarkAllUIToolkitDirty();
            var window = FindUIBuilderWindow();
            if (window != null)
            {
                window.Focus();
                window.rootVisualElement?.MarkDirtyRepaint();
                window.Repaint();
            }

            var previewState = InspectUIBuilderPreviewState(window, uxmlPath);
            bool editorIdle = EditorApplication.isCompiling == false && EditorApplication.isUpdating == false;
            if (frame >= waitFrames && editorIdle && previewState.Ready && autoMatchGameView &&
                previewState.CanvasTooSmall && canvasAdjustmentAttempted == false)
            {
                canvasAdjustmentAttempted = true;
                initialMatchGameView = previewState.MatchGameView;
                initialMatchGameViewKnown = previewState.MatchGameViewKnown;
                initialCanvasWidth = previewState.ConfiguredCanvasWidth;
                initialCanvasHeight = previewState.ConfiguredCanvasHeight;
                initialRequiredCanvasWidth = previewState.RequiredCanvasWidth;
                initialRequiredCanvasHeight = previewState.RequiredCanvasHeight;
                canvasAdjustmentFrame = frame;
                canvasAdjustmentApplied = TryEnableUIBuilderMatchGameView(window, out canvasAdjustmentError);
                readyFrameCount = 0;
                if (canvasAdjustmentApplied)
                {
                    EditorApplication.QueuePlayerLoopUpdate();
                    return;
                }
            }

            if (frame >= waitFrames && editorIdle && previewState.Ready)
                readyFrameCount++;
            else
                readyFrameCount = 0;

            double elapsedMs = (EditorApplication.timeSinceStartup - startedAt) * 1000d;
            bool timedOut = elapsedMs >= timeoutMs;
            if (readyFrameCount < stableFrames && timedOut == false)
            {
                EditorApplication.QueuePlayerLoopUpdate();
                return;
            }

            bool previewSettled = readyFrameCount >= stableFrames;
            bool contentFitAccepted = requireContentFit == false || previewState.CanvasTooSmall == false;
            var result = new Dictionary<string, object>
            {
                { "success", previewSettled && contentFitAccepted },
                { "uxmlPath", uxmlPath },
                { "opened", opened },
                { "waitFrames", waitFrames },
                { "stableFrames", stableFrames },
                { "readyFrameCount", readyFrameCount },
                { "elapsedMs", Math.Round(elapsedMs, 2) },
                { "timedOut", timedOut },
                { "repaintedRuntimeDocuments", repainted },
                { "windowFound", window != null },
                { "window", window == null ? null : BuildWindowInfo(window) },
                { "preview", previewState.ToDictionary() },
                { "canvasAdjustment", new Dictionary<string, object>
                    {
                        { "autoMatchGameView", autoMatchGameView },
                        { "requireContentFit", requireContentFit },
                        { "attempted", canvasAdjustmentAttempted },
                        { "applied", canvasAdjustmentApplied },
                        { "attemptedAtFrame", canvasAdjustmentFrame },
                        { "initialMatchGameView", initialMatchGameViewKnown
                            ? (object)initialMatchGameView
                            : null },
                        { "finalMatchGameView", previewState.MatchGameViewKnown
                            ? (object)previewState.MatchGameView
                            : null },
                        { "initialCanvasSize", new Dictionary<string, object>
                            {
                                { "width", initialCanvasWidth },
                                { "height", initialCanvasHeight },
                            }
                        },
                        { "initialRequiredCanvasSize", new Dictionary<string, object>
                            {
                                { "width", initialRequiredCanvasWidth },
                                { "height", initialRequiredCanvasHeight },
                            }
                        },
                        { "finalCanvasSize", new Dictionary<string, object>
                            {
                                { "width", previewState.ConfiguredCanvasWidth },
                                { "height", previewState.ConfiguredCanvasHeight },
                            }
                        },
                        { "finalRequiredCanvasSize", new Dictionary<string, object>
                            {
                                { "width", previewState.RequiredCanvasWidth },
                                { "height", previewState.RequiredCanvasHeight },
                            }
                        },
                        { "contentFitsCanvas", previewState.ContentFitsCanvas },
                        { "error", canvasAdjustmentError },
                    }
                },
            };

            if (args.ContainsKey("zoom"))
            {
                result["requestedZoom"] = GetFloat(args, "zoom", 1);
                result["zoomApplied"] = false;
                result["zoomNote"] = "UI Builder zoom is not exposed through a stable public Unity API; the window is opened and captured instead.";
            }

            if (capture)
            {
                var screenshot = VmAutomationScreenshotCommands.CaptureEditorWindow(new Dictionary<string, object>
                {
                    { "window", "UI Builder" },
                    { "path", screenshotPath },
                    { "maxDimension", GetInt(args, "maxDimension", 8192) },
                    { "captureMode", "screen" },
                });
                result["screenshot"] = screenshot;

                var screenshotResult = screenshot as Dictionary<string, object>;
                bool screenshotSucceeded = screenshotResult != null &&
                                           GetBool(screenshotResult, "success", false);
                var visualAnalysis = screenshotSucceeded && window != null
                    ? AnalyzeUIBuilderScreenshot(screenshotResult, window, previewState)
                    : new Dictionary<string, object>
                    {
                        { "visualValid", false },
                        { "documentVisuallyBlank", true },
                        { "conclusive", false },
                        { "reason", screenshotSucceeded ? "ui_builder_window_unavailable" : "screenshot_capture_failed" },
                    };
                result["visualAnalysis"] = visualAnalysis;

                bool visualValid = screenshotSucceeded && GetBool(visualAnalysis, "visualValid", false);
                result["visualValid"] = visualValid;
                if (visualValid == false)
                {
                    result["success"] = false;
                    string visualReason = GetString(visualAnalysis, "reason");
                    result["error"] = screenshotSucceeded == false
                        ? "UI Builder screenshot capture failed."
                        : string.Equals(visualReason, "document_matches_canvas_background",
                              StringComparison.Ordinal) ||
                          string.Equals(visualReason, "document_matches_checkerboard_or_blank_shell",
                              StringComparison.Ordinal)
                            ? "UI Builder document preview is visually indistinguishable from a checkerboard or blank canvas; preview evidence is invalid."
                            : $"UI Builder preview visual analysis was inconclusive ({visualReason}); preview evidence is invalid.";
                }
            }

            if (previewSettled && requireContentFit && previewState.CanvasTooSmall &&
                result.ContainsKey("error") == false)
            {
                result["success"] = false;
                result["error"] = canvasAdjustmentAttempted
                    ? "UI Builder canvas remains smaller than the visible document content after enabling Match Game View."
                    : "UI Builder canvas is smaller than the visible document content.";
            }

            if (previewSettled == false && result.ContainsKey("error") == false)
            {
                result["error"] = previewState.Error.Length > 0
                    ? previewState.Error
                    : "UI Builder did not load the requested UXML before timeout.";
            }

            Finish(result);
        }

        EditorApplication.update += Tick;
    }

    public static object OpenUIBuilderPreview(Dictionary<string, object> args)
    {
        return new { error = "uitoolkit/builder-preview must be executed through the deferred route." };
    }


    private sealed class UIBuilderPreviewState
    {
        public bool Ready;
        public bool DocumentPathMatches;
        public string ActiveUxmlPath = "";
        public int DocumentRootChildCount = -1;
        public int CanvasChildCount = -1;
        public float DocumentRootWidth;
        public float DocumentRootHeight;
        public float CanvasWidth;
        public float CanvasHeight;
        public float ConfiguredCanvasWidth;
        public float ConfiguredCanvasHeight;
        public float RequiredCanvasWidth;
        public float RequiredCanvasHeight;
        public Rect DocumentRootWorldBound;
        public Rect CanvasWorldBound;
        public Rect ViewportWorldBound;
        public Rect ContentWorldBound;
        public int ContentElementCount;
        public bool ContentFitsCanvas = true;
        public bool CanvasTooSmall;
        public float ContentOverflowLeft;
        public float ContentOverflowTop;
        public float ContentOverflowRight;
        public float ContentOverflowBottom;
        public bool MatchGameView;
        public bool MatchGameViewKnown;
        public UnityEngine.UIElements.VisualElement DocumentRoot;
        public UnityEngine.UIElements.VisualElement Canvas;
        public UnityEngine.UIElements.VisualElement Viewport;
        public string Error = "";

        public Dictionary<string, object> ToDictionary()
        {
            return new Dictionary<string, object>
            {
                { "ready", Ready },
                { "documentPathMatches", DocumentPathMatches },
                { "activeUxmlPath", ActiveUxmlPath },
                { "documentRootChildCount", DocumentRootChildCount },
                { "canvasChildCount", CanvasChildCount },
                { "documentRootSize", new Dictionary<string, object>
                    {
                        { "width", DocumentRootWidth },
                        { "height", DocumentRootHeight },
                    }
                },
                { "canvasSize", new Dictionary<string, object>
                    {
                        { "width", CanvasWidth },
                        { "height", CanvasHeight },
                    }
                },
                { "configuredCanvasSize", new Dictionary<string, object>
                    {
                        { "width", ConfiguredCanvasWidth },
                        { "height", ConfiguredCanvasHeight },
                    }
                },
                { "requiredCanvasSize", new Dictionary<string, object>
                    {
                        { "width", RequiredCanvasWidth },
                        { "height", RequiredCanvasHeight },
                    }
                },
                { "matchGameView", MatchGameViewKnown ? (object)MatchGameView : null },
                { "contentElementCount", ContentElementCount },
                { "contentFitsCanvas", ContentFitsCanvas },
                { "canvasTooSmall", CanvasTooSmall },
                { "contentOverflow", new Dictionary<string, object>
                    {
                        { "left", ContentOverflowLeft },
                        { "top", ContentOverflowTop },
                        { "right", ContentOverflowRight },
                        { "bottom", ContentOverflowBottom },
                    }
                },
                { "documentRootWorldBound", RectToDictionary(DocumentRootWorldBound) },
                { "canvasWorldBound", RectToDictionary(CanvasWorldBound) },
                { "viewportWorldBound", RectToDictionary(ViewportWorldBound) },
                { "contentWorldBound", RectToDictionary(ContentWorldBound) },
                { "error", Error },
            };
        }
    }

    private static UIBuilderPreviewState InspectUIBuilderPreviewState(EditorWindow window,
        string expectedUxmlPath)
    {
        var state = new UIBuilderPreviewState();
        if (window == null)
        {
            state.Error = "UI Builder window was not found.";
            return state;
        }

        try
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var windowType = window.GetType();
            var document = windowType.GetProperty("document", flags)?.GetValue(window);
            if (document == null)
            {
                state.Error = "UI Builder document is not initialized.";
                return state;
            }

            var documentType = document.GetType();
            state.ActiveUxmlPath = documentType.GetProperty("uxmlPath", flags)?.GetValue(document)?.ToString() ?? "";
            state.DocumentPathMatches = string.Equals(NormalizeAssetPath(state.ActiveUxmlPath, ""),
                NormalizeAssetPath(expectedUxmlPath, ""), StringComparison.OrdinalIgnoreCase);
            var documentSettings = documentType.GetProperty("settings", flags)?.GetValue(document);
            if (TryReadFloatMember(documentSettings, "CanvasWidth", out float configuredCanvasWidth))
                state.ConfiguredCanvasWidth = configuredCanvasWidth;
            if (TryReadFloatMember(documentSettings, "CanvasHeight", out float configuredCanvasHeight))
                state.ConfiguredCanvasHeight = configuredCanvasHeight;
            if (TryReadBoolMember(documentSettings, "MatchGameView", out bool settingsMatchGameView))
            {
                state.MatchGameView = settingsMatchGameView;
                state.MatchGameViewKnown = true;
            }

            var documentRoot = windowType.GetProperty("documentRootElement", flags)?.GetValue(window)
                as UnityEngine.UIElements.VisualElement;
            object canvasObject = windowType.GetProperty("canvas", flags)?.GetValue(window);
            var canvas = canvasObject as UnityEngine.UIElements.VisualElement;
            if (TryReadBoolMember(canvasObject, "matchGameView", out bool canvasMatchGameView))
            {
                state.MatchGameView = canvasMatchGameView;
                state.MatchGameViewKnown = true;
            }

            state.DocumentRoot = documentRoot;
            state.Canvas = canvas;
            var viewport = canvas;
            while (viewport != null &&
                   viewport.GetType().Name.IndexOf("BuilderViewport", StringComparison.Ordinal) < 0 &&
                   viewport.ClassListContains("unity-builder-viewport") == false)
            {
                viewport = viewport.parent;
            }
            state.Viewport = viewport;

            state.DocumentRootChildCount = documentRoot?.childCount ?? -1;
            state.CanvasChildCount = canvas?.childCount ?? -1;
            if (documentRoot != null)
            {
                state.DocumentRootWidth = documentRoot.layout.width;
                state.DocumentRootHeight = documentRoot.layout.height;
                state.DocumentRootWorldBound = documentRoot.worldBound;
            }

            if (IsPositiveFinite(state.ConfiguredCanvasWidth) == false)
                state.ConfiguredCanvasWidth = state.DocumentRootWidth;
            if (IsPositiveFinite(state.ConfiguredCanvasHeight) == false)
                state.ConfiguredCanvasHeight = state.DocumentRootHeight;

            if (canvas != null)
            {
                state.CanvasWidth = canvas.layout.width;
                state.CanvasHeight = canvas.layout.height;
                state.CanvasWorldBound = canvas.worldBound;
            }

            if (viewport != null)
                state.ViewportWorldBound = viewport.worldBound;

            MeasureUIBuilderContentBounds(state);
            state.Ready = state.DocumentPathMatches && state.DocumentRootChildCount > 0 &&
                          state.CanvasChildCount > 0 && IsPositiveFinite(state.DocumentRootWidth) &&
                          IsPositiveFinite(state.DocumentRootHeight) && IsPositiveFinite(state.CanvasWidth) &&
                          IsPositiveFinite(state.CanvasHeight);
            if (state.Ready == false)
                state.Error = "UI Builder document or canvas is not ready.";
        }
        catch (Exception ex)
        {
            state.Error = ex.Message;
        }

        return state;
    }

    private static bool TryEnableUIBuilderMatchGameView(EditorWindow window, out string error)
    {
        error = "";
        if (window == null)
        {
            error = "UI Builder window was not found.";
            return false;
        }

        try
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var windowType = window.GetType();
            object canvasObject = windowType.GetProperty("canvas", flags)?.GetValue(window);
            if (canvasObject == null)
            {
                error = "UI Builder canvas is not initialized.";
                return false;
            }

            bool matchGameViewSet = TryWriteBoolMember(canvasObject, "matchGameView", true);
            if (matchGameViewSet == false)
            {
                object document = windowType.GetProperty("document", flags)?.GetValue(window);
                object documentSettings = document?.GetType().GetProperty("settings", flags)?.GetValue(document);
                matchGameViewSet = TryWriteBoolMember(documentSettings, "MatchGameView", true);
            }

            if (matchGameViewSet == false)
            {
                error = "This Unity version does not expose a writable UI Builder Match Game View setting.";
                return false;
            }

            var updateRenderSize = canvasObject.GetType().GetMethod("UpdateRenderSize", flags, null,
                Type.EmptyTypes, null);
            updateRenderSize?.Invoke(canvasObject, null);
            if (canvasObject is UnityEngine.UIElements.VisualElement canvas)
                canvas.MarkDirtyRepaint();
            window.rootVisualElement?.MarkDirtyRepaint();
            window.Repaint();
            return true;
        }
        catch (TargetInvocationException ex)
        {
            error = ex.InnerException?.Message ?? ex.Message;
            return false;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static bool TryReadFloatMember(object target, string memberName, out float value)
    {
        value = 0;
        if (target == null)
            return false;

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        object rawValue = target.GetType().GetProperty(memberName, flags)?.GetValue(target);
        if (rawValue == null)
            rawValue = target.GetType().GetField(memberName, flags)?.GetValue(target);
        if (rawValue == null)
            return false;

        try
        {
            value = Convert.ToSingle(rawValue);
            return float.IsNaN(value) == false && float.IsInfinity(value) == false;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryReadBoolMember(object target, string memberName, out bool value)
    {
        value = false;
        if (target == null)
            return false;

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        object rawValue = target.GetType().GetProperty(memberName, flags)?.GetValue(target);
        if (rawValue == null)
            rawValue = target.GetType().GetField(memberName, flags)?.GetValue(target);
        if (rawValue == null)
            return false;

        try
        {
            value = Convert.ToBoolean(rawValue);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryWriteBoolMember(object target, string memberName, bool value)
    {
        if (target == null)
            return false;

        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var property = target.GetType().GetProperty(memberName, flags);
        if (property != null && property.CanWrite)
        {
            property.SetValue(target, value);
            return true;
        }

        var field = target.GetType().GetField(memberName, flags);
        if (field == null || field.IsInitOnly)
            return false;

        field.SetValue(target, value);
        return true;
    }

    private static void MeasureUIBuilderContentBounds(UIBuilderPreviewState state)
    {
        state.ContentFitsCanvas = true;
        state.CanvasTooSmall = false;
        state.RequiredCanvasWidth = state.ConfiguredCanvasWidth;
        state.RequiredCanvasHeight = state.ConfiguredCanvasHeight;
        if (state.DocumentRoot == null || IsUsableWorldRect(state.DocumentRootWorldBound) == false)
            return;

        bool hasContentBounds = false;
        Rect contentBounds = default;
        var pending = new Stack<UnityEngine.UIElements.VisualElement>();
        foreach (var child in state.DocumentRoot.Children())
            pending.Push(child);

        while (pending.Count > 0)
        {
            var element = pending.Pop();
            if (element == null)
                continue;

            bool visible = true;
            try
            {
                visible = element.resolvedStyle.display != UnityEngine.UIElements.DisplayStyle.None &&
                          element.resolvedStyle.visibility == UnityEngine.UIElements.Visibility.Visible;
            }
            catch
            {
            }

            if (visible && IsUsableWorldRect(element.worldBound))
            {
                contentBounds = hasContentBounds
                    ? Rect.MinMaxRect(
                        Math.Min(contentBounds.xMin, element.worldBound.xMin),
                        Math.Min(contentBounds.yMin, element.worldBound.yMin),
                        Math.Max(contentBounds.xMax, element.worldBound.xMax),
                        Math.Max(contentBounds.yMax, element.worldBound.yMax))
                    : element.worldBound;
                hasContentBounds = true;
                state.ContentElementCount++;
            }

            foreach (var child in element.Children())
                pending.Push(child);
        }

        if (hasContentBounds == false)
            return;

        state.ContentWorldBound = contentBounds;
        Rect canvasBounds = state.DocumentRootWorldBound;
        state.ContentOverflowLeft = Math.Max(0, canvasBounds.xMin - contentBounds.xMin);
        state.ContentOverflowTop = Math.Max(0, canvasBounds.yMin - contentBounds.yMin);
        state.ContentOverflowRight = Math.Max(0, contentBounds.xMax - canvasBounds.xMax);
        state.ContentOverflowBottom = Math.Max(0, contentBounds.yMax - canvasBounds.yMax);

        const float containmentTolerance = 0.5f;
        state.ContentFitsCanvas = state.ContentOverflowLeft <= containmentTolerance &&
                                  state.ContentOverflowTop <= containmentTolerance &&
                                  state.ContentOverflowRight <= containmentTolerance &&
                                  state.ContentOverflowBottom <= containmentTolerance;
        state.CanvasTooSmall = state.ContentFitsCanvas == false;

        float scaleX = IsPositiveFinite(state.DocumentRootWidth)
            ? canvasBounds.width / state.DocumentRootWidth
            : 0;
        float scaleY = IsPositiveFinite(state.DocumentRootHeight)
            ? canvasBounds.height / state.DocumentRootHeight
            : 0;
        if (IsPositiveFinite(scaleX))
        {
            state.RequiredCanvasWidth = state.ConfiguredCanvasWidth +
                                        (state.ContentOverflowLeft + state.ContentOverflowRight) / scaleX;
        }

        if (IsPositiveFinite(scaleY))
        {
            state.RequiredCanvasHeight = state.ConfiguredCanvasHeight +
                                         (state.ContentOverflowTop + state.ContentOverflowBottom) / scaleY;
        }
    }

    private static bool IsUsableWorldRect(Rect rect)
    {
        return IsPositiveFinite(rect.width) && IsPositiveFinite(rect.height) &&
               float.IsNaN(rect.x) == false && float.IsInfinity(rect.x) == false &&
               float.IsNaN(rect.y) == false && float.IsInfinity(rect.y) == false;
    }

    private static Dictionary<string, object> AnalyzeUIBuilderScreenshot(
        Dictionary<string, object> screenshot, EditorWindow window, UIBuilderPreviewState previewState)
    {
        string screenshotPath = GetString(screenshot, "path");
        string absolutePath = GetAbsoluteAssetPath(screenshotPath);
        if (string.IsNullOrEmpty(absolutePath) || File.Exists(absolutePath) == false)
        {
            return new Dictionary<string, object>
            {
                { "visualValid", false },
                { "documentVisuallyBlank", true },
                { "conclusive", false },
                { "reason", "screenshot_file_missing" },
                { "error", $"Screenshot file was not found at '{screenshotPath}'." },
            };
        }

        if (previewState.DocumentRoot == null || previewState.Canvas == null ||
            window.rootVisualElement == null)
        {
            return new Dictionary<string, object>
            {
                { "visualValid", false },
                { "documentVisuallyBlank", true },
                { "conclusive", false },
                { "reason", "preview_elements_unavailable" },
                { "error", "UI Builder document root or canvas is unavailable." },
            };
        }

        Texture2D texture = null;
        try
        {
            texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (texture.LoadImage(File.ReadAllBytes(absolutePath)) == false)
            {
                return new Dictionary<string, object>
                {
                    { "visualValid", false },
                    { "documentVisuallyBlank", true },
                    { "conclusive", false },
                    { "reason", "screenshot_decode_failed" },
                    { "error", "UI Builder screenshot PNG could not be decoded." },
                };
            }

            int width = texture.width;
            int height = texture.height;
            RectInt contentRect = ReadCaptureContentRect(screenshot, width, height);
            Rect rootWorldBound = window.rootVisualElement.worldBound;
            RectInt canvasRect = MapWorldRectToCapture(previewState.CanvasWorldBound, rootWorldBound,
                contentRect, width, height);
            if (previewState.Viewport != null)
            {
                RectInt viewportRect = MapWorldRectToCapture(previewState.ViewportWorldBound, rootWorldBound,
                    contentRect, width, height);
                canvasRect = IntersectRects(canvasRect, viewportRect);
            }
            RectInt documentRect = MapWorldRectToCapture(previewState.DocumentRootWorldBound, rootWorldBound,
                contentRect, width, height);
            documentRect = IntersectRects(documentRect, canvasRect);

            var analysis = AnalyzeUIBuilderPixels(texture.GetPixels32(), width, height, documentRect,
                canvasRect);
            analysis["mappingMode"] = "root-relative-content";
            analysis["contentRect"] = RectToDictionary(new Rect(contentRect.x, contentRect.y,
                contentRect.width, contentRect.height));
            analysis["documentWorldBound"] = RectToDictionary(previewState.DocumentRootWorldBound);
            analysis["canvasWorldBound"] = RectToDictionary(previewState.CanvasWorldBound);
            analysis["viewportWorldBound"] = RectToDictionary(previewState.ViewportWorldBound);
            return analysis;
        }
        catch (Exception ex)
        {
            return new Dictionary<string, object>
            {
                { "visualValid", false },
                { "documentVisuallyBlank", true },
                { "conclusive", false },
                { "reason", "visual_analysis_failed" },
                { "error", ex.Message },
            };
        }
        finally
        {
            if (texture != null)
                UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    private static Dictionary<string, object> AnalyzeUIBuilderPixels(Color32[] pixels, int width, int height,
        RectInt documentRect, RectInt canvasRect)
    {
        canvasRect = ClampRectToImage(canvasRect, width, height);
        documentRect = IntersectRects(ClampRectToImage(documentRect, width, height), canvasRect);
        int inset = Math.Min(4, Math.Max(0, (Math.Min(documentRect.width, documentRect.height) - 1) / 4));
        RectInt sampledDocumentRect = InsetRect(documentRect, inset);
        RectInt excludedBackgroundRect = ExpandRect(documentRect, 4, canvasRect);

        long canvasArea = Math.Max(1L, (long)canvasRect.width * canvasRect.height);
        int sampleStep = Math.Max(1, Mathf.CeilToInt(Mathf.Sqrt(canvasArea / 65536f)));
        if (sampleStep > 1 && sampleStep % 2 == 0)
            sampleStep++;

        var targetHistogram = SampleColorBuckets(pixels, width, height, sampledDocumentRect,
            default, false, sampleStep, out int targetSamples);
        var backgroundHistogram = SampleColorBuckets(pixels, width, height, canvasRect,
            excludedBackgroundRect, true, sampleStep, out int backgroundSamples);

        var backgroundPalette = new HashSet<int>(backgroundHistogram.Keys);

        int outOfPaletteSamples = 0;
        int backgroundOverlapSamples = 0;
        int neutralTargetSamples = 0;
        int dominantTargetBucketSamples = 0;
        foreach (var pair in targetHistogram)
        {
            if (backgroundPalette.Contains(pair.Key))
                backgroundOverlapSamples += pair.Value;
            else
                outOfPaletteSamples += pair.Value;

            int red = pair.Key >> 8 & 0xF;
            int green = pair.Key >> 4 & 0xF;
            int blue = pair.Key & 0xF;
            if (Math.Max(red, Math.Max(green, blue)) - Math.Min(red, Math.Min(green, blue)) <= 1)
                neutralTargetSamples += pair.Value;

            dominantTargetBucketSamples = Math.Max(dominantTargetBucketSamples, pair.Value);
        }

        double outOfPaletteRatio = targetSamples > 0
            ? outOfPaletteSamples / (double)targetSamples
            : 0d;
        double backgroundOverlapRatio = targetSamples > 0
            ? backgroundOverlapSamples / (double)targetSamples
            : 0d;
        double neutralTargetRatio = targetSamples > 0
            ? neutralTargetSamples / (double)targetSamples
            : 0d;
        double dominantTargetBucketRatio = targetSamples > 0
            ? dominantTargetBucketSamples / (double)targetSamples
            : 0d;
        double histogramDistance = CalculateHistogramDistance(targetHistogram, targetSamples,
            backgroundHistogram, backgroundSamples);
        int minimumOutOfPaletteSamples = Math.Max(8, Mathf.CeilToInt(targetSamples * 0.001f));
        bool targetSampled = targetSamples >= 64;
        bool backgroundComparable = backgroundSamples >= 64 && backgroundOverlapRatio >= 0.9d;
        bool hasOutOfPaletteEvidence = outOfPaletteSamples >= minimumOutOfPaletteSamples;
        bool hasDistributionEvidence = histogramDistance >= 0.12d;
        bool hasTargetColorEvidence = targetSamples - neutralTargetSamples >= minimumOutOfPaletteSamples;
        bool hasTargetComplexityEvidence = targetHistogram.Count >= 12 && dominantTargetBucketRatio <= 0.98d;
        bool visualValid = targetSampled && (backgroundComparable
            ? hasOutOfPaletteEvidence || hasDistributionEvidence
            : hasTargetColorEvidence || hasTargetComplexityEvidence);
        bool conclusive = targetSampled && (backgroundComparable || visualValid ||
            neutralTargetRatio >= 0.999d && targetHistogram.Count <= 8);
        string reason = targetSampled == false
            ? "insufficient_document_samples"
            : backgroundComparable
                ? visualValid
                    ? "document_differs_from_canvas_background"
                    : "document_matches_canvas_background"
                : visualValid
                    ? "document_contains_visual_content"
                    : conclusive
                        ? "document_matches_checkerboard_or_blank_shell"
                        : "visual_content_could_not_be_proven";

        return new Dictionary<string, object>
        {
            { "visualValid", visualValid },
            { "documentVisuallyBlank", visualValid == false },
            { "conclusive", conclusive },
            { "reason", reason },
            { "documentRect", RectToDictionary(new Rect(documentRect.x, documentRect.y,
                documentRect.width, documentRect.height)) },
            { "sampledDocumentRect", RectToDictionary(new Rect(sampledDocumentRect.x,
                sampledDocumentRect.y, sampledDocumentRect.width, sampledDocumentRect.height)) },
            { "canvasRect", RectToDictionary(new Rect(canvasRect.x, canvasRect.y,
                canvasRect.width, canvasRect.height)) },
            { "sampleStep", sampleStep },
            { "documentSamples", targetSamples },
            { "backgroundSamples", backgroundSamples },
            { "documentDistinctColorBuckets", targetHistogram.Count },
            { "backgroundDistinctColorBuckets", backgroundHistogram.Count },
            { "backgroundPaletteBucketCount", backgroundPalette.Count },
            { "backgroundComparable", backgroundComparable },
            { "backgroundOverlapSamples", backgroundOverlapSamples },
            { "backgroundOverlapRatio", Math.Round(backgroundOverlapRatio, 6) },
            { "outOfBackgroundPaletteSamples", outOfPaletteSamples },
            { "minimumOutOfPaletteSamples", minimumOutOfPaletteSamples },
            { "outOfBackgroundPaletteRatio", Math.Round(outOfPaletteRatio, 6) },
            { "histogramDistance", Math.Round(histogramDistance, 6) },
            { "hasOutOfPaletteEvidence", hasOutOfPaletteEvidence },
            { "hasDistributionEvidence", hasDistributionEvidence },
            { "neutralDocumentSamples", neutralTargetSamples },
            { "neutralDocumentRatio", Math.Round(neutralTargetRatio, 6) },
            { "dominantDocumentBucketRatio", Math.Round(dominantTargetBucketRatio, 6) },
            { "hasTargetColorEvidence", hasTargetColorEvidence },
            { "hasTargetComplexityEvidence", hasTargetComplexityEvidence },
        };
    }

    private static Dictionary<int, int> SampleColorBuckets(Color32[] pixels, int width, int height,
        RectInt region, RectInt excluded, bool useExclusion, int step, out int sampleCount)
    {
        var histogram = new Dictionary<int, int>();
        sampleCount = 0;
        if (pixels == null || pixels.Length < width * height || region.width <= 0 || region.height <= 0)
            return histogram;

        int startX = AlignSampleCoordinate(region.xMin, step);
        int startY = AlignSampleCoordinate(region.yMin, step);
        for (int y = startY; y < region.yMax; y += step)
        {
            for (int x = startX; x < region.xMax; x += step)
            {
                if (useExclusion && x >= excluded.xMin && x < excluded.xMax &&
                    y >= excluded.yMin && y < excluded.yMax)
                {
                    continue;
                }

                Color32 color = pixels[(height - 1 - y) * width + x];
                int bucket = (color.r >> 4) << 8 | (color.g >> 4) << 4 | (color.b >> 4);
                histogram.TryGetValue(bucket, out int count);
                histogram[bucket] = count + 1;
                sampleCount++;
            }
        }

        return histogram;
    }

    private static int AlignSampleCoordinate(int value, int step)
    {
        if (step <= 1)
            return value;

        int remainder = value % step;
        if (remainder < 0)
            remainder += step;
        return remainder == 0 ? value : value + step - remainder;
    }

    private static double CalculateHistogramDistance(Dictionary<int, int> first, int firstSamples,
        Dictionary<int, int> second, int secondSamples)
    {
        if (firstSamples <= 0 || secondSamples <= 0)
            return 0d;

        var buckets = new HashSet<int>(first.Keys);
        buckets.UnionWith(second.Keys);
        double distance = 0d;
        foreach (int bucket in buckets)
        {
            first.TryGetValue(bucket, out int firstCount);
            second.TryGetValue(bucket, out int secondCount);
            distance += Math.Abs(firstCount / (double)firstSamples - secondCount / (double)secondSamples);
        }

        return distance * 0.5d;
    }

    internal static RectInt ReadCaptureContentRect(Dictionary<string, object> capture, int width, int height)
    {
        var content = capture != null && capture.TryGetValue("contentRect", out object rawContent)
            ? AsDictionary(rawContent)
            : new Dictionary<string, object>();
        var rect = new RectInt(
            GetInt(content, "x", 0),
            GetInt(content, "y", 0),
            GetInt(content, "width", width),
            GetInt(content, "height", height));
        rect = ClampRectToImage(rect, width, height);
        return rect.width > 0 && rect.height > 0 ? rect : new RectInt(0, 0, width, height);
    }

    internal static RectInt MapWorldRectToCapture(Rect worldRect, Rect rootWorldRect, RectInt contentRect,
        int width, int height)
    {
        if (rootWorldRect.width <= 0 || rootWorldRect.height <= 0 ||
            contentRect.width <= 0 || contentRect.height <= 0)
        {
            return new RectInt();
        }

        float scaleX = contentRect.width / rootWorldRect.width;
        float scaleY = contentRect.height / rootWorldRect.height;
        var mapped = new RectInt(
            Mathf.FloorToInt(contentRect.x + (worldRect.x - rootWorldRect.x) * scaleX),
            Mathf.FloorToInt(contentRect.y + (worldRect.y - rootWorldRect.y) * scaleY),
            Mathf.Max(1, Mathf.CeilToInt(worldRect.width * scaleX)),
            Mathf.Max(1, Mathf.CeilToInt(worldRect.height * scaleY)));
        return ClampRectToImage(mapped, width, height);
    }

    internal static RectInt ClampRectToImage(RectInt rect, int width, int height)
    {
        int xMin = Mathf.Clamp(rect.xMin, 0, Math.Max(0, width));
        int yMin = Mathf.Clamp(rect.yMin, 0, Math.Max(0, height));
        int xMax = Mathf.Clamp(rect.xMax, xMin, Math.Max(0, width));
        int yMax = Mathf.Clamp(rect.yMax, yMin, Math.Max(0, height));
        return new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
    }

    private static RectInt IntersectRects(RectInt first, RectInt second)
    {
        int xMin = Math.Max(first.xMin, second.xMin);
        int yMin = Math.Max(first.yMin, second.yMin);
        int xMax = Math.Min(first.xMax, second.xMax);
        int yMax = Math.Min(first.yMax, second.yMax);
        return new RectInt(xMin, yMin, Math.Max(0, xMax - xMin), Math.Max(0, yMax - yMin));
    }

    private static RectInt InsetRect(RectInt rect, int inset)
    {
        int clampedInset = Math.Max(0, Math.Min(inset, Math.Min(rect.width, rect.height) / 2));
        return new RectInt(rect.x + clampedInset, rect.y + clampedInset,
            Math.Max(0, rect.width - clampedInset * 2), Math.Max(0, rect.height - clampedInset * 2));
    }

    private static RectInt ExpandRect(RectInt rect, int amount, RectInt bounds)
    {
        var expanded = new RectInt(rect.x - amount, rect.y - amount,
            rect.width + amount * 2, rect.height + amount * 2);
        return IntersectRects(expanded, bounds);
    }

    private static bool IsPositiveFinite(float value)
    {
        return float.IsNaN(value) == false && float.IsInfinity(value) == false && value > 0;
    }


    }
}
