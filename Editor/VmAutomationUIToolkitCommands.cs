using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

using static VMUnityAutomation.Editor.VmAutomationUICommandArguments;

using static VMUnityAutomation.Editor.VmAutomationAssetPathUtility;
using static VMUnityAutomation.Editor.VmAutomationUICaptureGeometry;
using static VMUnityAutomation.Editor.VmAutomationUIToolkitElementUtility;

namespace VMUnityAutomation.Editor
{
    public static class VmAutomationUIToolkitCommands
    {
    internal const int MaximumKeyboardEvents = 64;

    public static object DispatchRuntimePointer(Dictionary<string, object> args)
    {
        var document = VmObjectId.ToObject(GetString(args, "documentInstanceId")) as UIDocument;
        var root = document?.rootVisualElement;
        if (root?.panel == null)
            return VmAutomationResponse.Error("The exact runtime UIDocument must have an attached root.", "ui_pointer_document_unavailable");
        var position = new Vector2(GetFloat(args, "x", 0), GetFloat(args, "y", 0));
        string phase = GetString(args, "phase");
        if (HasForeignMouseCapture(root, out bool ownedCapture))
            return VmAutomationResponse.Error("Native mouse capture belongs outside the exact runtime UIDocument.", "ui_pointer_target_mismatch");
        bool capturedContinuation = ownedCapture && (phase == "Move" || phase == "Up");
        if (!capturedContinuation && !root.worldBound.Contains(position))
            return VmAutomationResponse.Error("The pointer must lie inside the selected document root.", "ui_pointer_outside_document");
        var target = root.panel.Pick(position);
        if (!capturedContinuation && (target == null || (target != root && !root.Contains(target))))
            return VmAutomationResponse.Error("Another document owns the hit-tested pointer position.", "ui_pointer_target_mismatch");
        string targetName = target?.name ?? "";
        string targetType = target?.GetType().FullName ?? "";
        DispatchNativePointer(capturedContinuation ? root : target, position, phase);
        return new Dictionary<string, object>
        {
            { "documentInstanceId", GetString(args, "documentInstanceId") },
            { "phase", phase }, { "pickedName", targetName }, { "pickedType", targetType },
            { "x", position.x }, { "y", position.y }, { "frame", Time.frameCount },
            { "dispatchedEvents", phase == "Click" ? 2 : 1 }
        };
    }

    internal static void DispatchNativePointer(VisualElement sender, Vector2 position, string phase)
    {
        if (phase == "Click")
        {
            var panel = sender.panel;
            try { DispatchNativePointer(sender, position, "Down"); }
            finally { DispatchNativePointer(panel.Pick(position), position, "Up"); }
            return;
        }
        var input = new Event { mousePosition = position, button = 0, clickCount = 1 };
        EventBase pointer;
        switch (phase)
        {
            case "Down": input.type = EventType.MouseDown; pointer = PointerDownEvent.GetPooled(input); break;
            case "Move": input.type = EventType.MouseDrag; pointer = PointerMoveEvent.GetPooled(input); break;
            case "Up": input.type = EventType.MouseUp; pointer = PointerUpEvent.GetPooled(input); break;
            default: throw new ArgumentException("Unknown native pointer phase.", nameof(phase));
        }
        using (pointer)
        {
            sender.SendEvent(pointer);
        }
    }

    public static object DispatchEditorKeyboard(Dictionary<string, object> args)
    {
        var window = VmObjectId.ToObject(GetString(args, "windowInstanceId")) as EditorWindow;
        var root = window == null ? null : window.rootVisualElement;
        if (root?.panel == null)
            return VmAutomationResponse.Error("The exact existing EditorWindow must have an attached root.", "ui_keyboard_window_unavailable");
        var inputs = ParseNativeKeyboardInputs((IList)args["events"]);
        var error = DispatchNativeKeyboard(root, inputs, out var initialFocus, out var finalFocus);
        if (error != null) return error;
        return new Dictionary<string, object>
        {
            { "windowInstanceId", GetString(args, "windowInstanceId") },
            { "dispatchedEvents", inputs.Length },
            { "initialFocusedName", initialFocus.name }, { "initialFocusedType", initialFocus.GetType().FullName },
            { "finalFocusedName", finalFocus == null ? string.Empty : finalFocus.name },
            { "finalFocusedType", finalFocus == null ? string.Empty : finalFocus.GetType().FullName }
        };
    }

    public static object DispatchRuntimeKeyboard(Dictionary<string, object> args)
    {
        var document = VmObjectId.ToObject(GetString(args, "documentInstanceId")) as UIDocument;
        var inputs = ParseNativeKeyboardInputs((IList)args["events"]);
        var error = DispatchNativeKeyboard(document?.rootVisualElement, inputs,
            out var initialFocus, out var finalFocus);
        if (error != null) return error;
        string initialName = initialFocus.name;
        string initialType = initialFocus.GetType().FullName;
        string finalName = finalFocus == null ? string.Empty : finalFocus.name;
        string finalType = finalFocus == null ? string.Empty : finalFocus.GetType().FullName;
        return new Dictionary<string, object>
        {
            { "documentInstanceId", GetString(args, "documentInstanceId") },
            { "dispatchedEvents", inputs.Length },
            { "initialFocusedName", initialName }, { "initialFocusedType", initialType },
            { "finalFocusedName", finalName }, { "finalFocusedType", finalType },
            { "frame", Time.frameCount }
        };
    }

    internal readonly struct NativeKeyboardInput
    {
        internal readonly bool IsDown;
        internal readonly KeyCode Key;
        internal readonly char Character;
        internal readonly EventModifiers Modifiers;

        internal NativeKeyboardInput(bool isDown, KeyCode key, char character, EventModifiers modifiers)
        {
            IsDown = isDown;
            Key = key;
            Character = character;
            Modifiers = modifiers;
        }
    }

    internal static NativeKeyboardInput[] ParseNativeKeyboardInputs(IList events)
    {
        var inputs = new NativeKeyboardInput[events.Count];
        for (int index = 0; index < events.Count; index++)
        {
            var entry = (Dictionary<string, object>)events[index];
            string character = (string)entry["character"];
            var modifiers = EventModifiers.None;
            foreach (string modifier in (IList)entry["modifiers"])
                modifiers |= (EventModifiers)Enum.Parse(typeof(EventModifiers), modifier);
            inputs[index] = new NativeKeyboardInput((string)entry["phase"] == "Down",
                (KeyCode)Enum.Parse(typeof(KeyCode), (string)entry["keyCode"]),
                character.Length == 0 ? '\0' : character[0], modifiers);
        }
        return inputs;
    }

    internal static Dictionary<string, object> DispatchNativeKeyboard(VisualElement root,
        IReadOnlyList<NativeKeyboardInput> inputs, out VisualElement initialFocus, out VisualElement finalFocus)
    {
        initialFocus = null;
        finalFocus = null;
        for (int index = 0; index < inputs.Count; index++)
        {
            if (root?.panel == null)
                return KeyboardDispatchError("The exact UI root must be attached.",
                    "ui_keyboard_document_unavailable", index);
            var focus = root.focusController.focusedElement as VisualElement;
            if (focus == null)
                return KeyboardDispatchError("The selected panel has no focused UI Toolkit element.",
                    "ui_keyboard_focus_unavailable", index);
            if (focus != root && !root.Contains(focus))
                return KeyboardDispatchError("The focused element belongs outside the exact UI root.",
                    "ui_keyboard_focus_mismatch", index);
            if (HasForeignMouseCapture(root, out _))
                return KeyboardDispatchError("Native mouse capture belongs outside the exact UI root.",
                    "ui_keyboard_capture_mismatch", index);
            if (index == 0) initialFocus = focus;
            var input = inputs[index];
            EventBase key = input.IsDown
                ? (EventBase)KeyDownEvent.GetPooled(input.Character, input.Key, input.Modifiers)
                : KeyUpEvent.GetPooled(input.Character, input.Key, input.Modifiers);
            using (key)
            {
                root.SendEvent(key);
            }
        }
        finalFocus = root.focusController?.focusedElement as VisualElement;
        return null;
    }

    internal static bool HasForeignMouseCapture(VisualElement root, out bool ownedCapture)
    {
        var capture = root.panel.GetCapturingElement(PointerId.mousePointerId);
        ownedCapture = capture != null && (capture == root ||
            (capture is VisualElement capturedElement && root.Contains(capturedElement)));
        return capture != null && !ownedCapture;
    }

    private static Dictionary<string, object> KeyboardDispatchError(string message, string errorCode,
        int eventIndex)
    {
        return VmAutomationResponse.Error(message, errorCode, extra: new Dictionary<string, object>
        {
            { "eventIndex", eventIndex }, { "dispatchedEvents", eventIndex }
        });
    }

    public static object ListEditorUIWindows(Dictionary<string, object> args)
    {
        var windows = Resources.FindObjectsOfTypeAll<EditorWindow>()
            .Where(window => window != null)
            .OrderBy(window => window.GetType().FullName)
            .Select(BuildWindowInfo)
            .ToList();

        return new Dictionary<string, object>
        {
            { "success", true },
            { "count", windows.Count },
            { "windows", windows },
        };
    }

    public static object GetEditorUITree(Dictionary<string, object> args)
    {
        var window = FindEditorWindow(args, out string error, out string selectionCode);
        if (window == null)
            return VmAutomationResponse.Error(error, selectionCode);

        var root = window.rootVisualElement;
        if (root == null)
            return new { error = $"Window '{window.titleContent?.text}' has no rootVisualElement" };

        int maxDepth = GetInt(args, "maxDepth", 8);
        int maxNodes = GetInt(args, "maxNodes", 300);
        bool includeStyle = GetBool(args, "includeStyle", false);
        int count = 0;
        bool truncated = false;
        var tree = BuildElementTree(root, "root", 0, maxDepth, maxNodes, includeStyle, ref count, ref truncated);

        return new Dictionary<string, object>
        {
            { "success", true },
            { "window", BuildWindowInfo(window) },
            { "nodeCount", count },
            { "truncated", truncated },
            { "tree", tree },
        };
    }

    public static object QueryEditorUI(Dictionary<string, object> args)
    {
        var window = FindEditorWindow(args, out string error, out string selectionCode);
        if (window == null)
            return VmAutomationResponse.Error(error, selectionCode);

        string name = GetString(args, "name");
        string className = GetString(args, "className");
        string typeName = GetString(args, "typeName");
        string text = GetString(args, "text");
        if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(className) &&
            string.IsNullOrEmpty(typeName) && string.IsNullOrEmpty(text))
            return new { error = "At least one query filter is required: name, className, typeName, or text" };

        int maxResults = GetInt(args, "maxResults", 50);
        bool includeStyle = GetBool(args, "includeStyle", false);
        var results = new List<Dictionary<string, object>>();
        QueryElements(window.rootVisualElement, "root", name, className, typeName, text, includeStyle, maxResults, results);

        return new Dictionary<string, object>
        {
            { "success", true },
            { "window", BuildWindowInfo(window) },
            { "count", results.Count },
            { "results", results },
        };
    }

    public static object GetEditorUIStyle(Dictionary<string, object> args)
    {
        var window = FindEditorWindow(args, out string error, out string selectionCode);
        if (window == null)
            return VmAutomationResponse.Error(error, selectionCode);

        var element = FindElement(args, window, out error);
        if (element == null)
            return new { error };

        return new Dictionary<string, object>
        {
            { "success", true },
            { "window", BuildWindowInfo(window) },
            { "element", BuildElementInfo(element, GetString(args, "path"), false) },
            { "inlineStyle", BuildInlineStyleInfo(element) },
            { "resolvedStyle", BuildResolvedStyleInfo(element) },
        };
    }

    public static object RepaintEditorUI(Dictionary<string, object> args)
    {
        var window = FindEditorWindow(args, out string error, out string selectionCode);
        if (window == null)
            return VmAutomationResponse.Error(error, selectionCode);

        string path = GetString(args, "path");
        if (!string.IsNullOrEmpty(path))
        {
            var element = GetElementByPath(window.rootVisualElement, path);
            if (element == null)
                return new { error = $"UI Toolkit element path '{path}' was not found" };

            element.MarkDirtyRepaint();
        }

        window.rootVisualElement?.MarkDirtyRepaint();
        window.Repaint();

        return new Dictionary<string, object>
        {
            { "success", true },
            { "window", BuildWindowInfo(window) },
            { "repaintedPath", string.IsNullOrEmpty(path) ? "root" : path },
        };
    }

    public static object ListRuntimeUIDocuments(Dictionary<string, object> args)
    {
        bool includeInactive = GetBool(args, "includeInactive", true);
        var documents = GetRuntimeUIDocuments(includeInactive)
            .Select(BuildUIDocumentInfo)
            .ToList();

        return new Dictionary<string, object>
        {
            { "success", true },
            { "count", documents.Count },
            { "documents", documents },
        };
    }

    public static object GetRuntimeUITree(Dictionary<string, object> args)
    {
        var document = FindRuntimeUIDocument(args, out string error);
        if (document == null)
            return new { error };

        var root = document.rootVisualElement;
        if (root == null)
            return new { error = $"UIDocument '{document.name}' has no rootVisualElement" };

        int maxDepth = GetInt(args, "maxDepth", 8);
        int maxNodes = GetInt(args, "maxNodes", 300);
        bool includeStyle = GetBool(args, "includeStyle", false);
        int count = 0;
        bool truncated = false;
        var tree = BuildElementTree(root, "root", 0, maxDepth, maxNodes, includeStyle, ref count, ref truncated);

        return new Dictionary<string, object>
        {
            { "success", true },
            { "document", BuildUIDocumentInfo(document) },
            { "nodeCount", count },
            { "truncated", truncated },
            { "tree", tree },
        };
    }

    public static object QueryRuntimeUI(Dictionary<string, object> args)
    {
        var document = FindRuntimeUIDocument(args, out string error);
        if (document == null)
            return new { error };

        var root = document.rootVisualElement;
        if (root == null)
            return new { error = $"UIDocument '{document.name}' has no rootVisualElement" };

        bool includeStyle = GetBool(args, "includeStyle", false);
        var element = FindRuntimeElement(args, document, out string elementPath, out error);
        if (element != null && HasElementLocator(args))
        {
            return new Dictionary<string, object>
            {
                { "success", true },
                { "document", BuildUIDocumentInfo(document) },
                { "count", 1 },
                { "results", new List<Dictionary<string, object>>
                    {
                        BuildElementInfo(element, elementPath, includeStyle),
                    }
                },
            };
        }

        string name = GetString(args, "name");
        string className = GetString(args, "className");
        string typeName = GetString(args, "typeName");
        string text = GetString(args, "text");
        if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(className) &&
            string.IsNullOrEmpty(typeName) && string.IsNullOrEmpty(text))
            return new { error = string.IsNullOrEmpty(error) ? "At least one query filter or path is required" : error };

        int maxResults = GetInt(args, "maxResults", 50);
        var results = new List<Dictionary<string, object>>();
        QueryElements(root, "root", name, className, typeName, text, includeStyle, maxResults, results);

        return new Dictionary<string, object>
        {
            { "success", true },
            { "document", BuildUIDocumentInfo(document) },
            { "count", results.Count },
            { "results", results },
        };
    }

    public static object GetRuntimeUIStyle(Dictionary<string, object> args)
    {
        var document = FindRuntimeUIDocument(args, out string error);
        if (document == null)
            return new { error };

        var element = FindRuntimeElement(args, document, out string elementPath, out error);
        if (element == null)
            return new { error };

        return new Dictionary<string, object>
        {
            { "success", true },
            { "document", BuildUIDocumentInfo(document) },
            { "element", BuildElementInfo(element, elementPath, false) },
            { "inlineStyle", BuildInlineStyleInfo(element) },
            { "resolvedStyle", BuildResolvedStyleInfo(element) },
            { "background", BuildBackgroundInfo(element) },
        };
    }

    public static object LocateUIToolkitElement(Dictionary<string, object> args)
    {
        bool runtime = GetBool(args, "runtime", false);
        var resolved = ResolveUIToolkitElement(args, runtime);
        if (resolved.Error != null)
            return VmAutomationResponse.Error(resolved.Error, resolved.ErrorCode ?? "error");

        float pixelScale = GetFloat(args, "pixelScale", EditorGUIUtility.pixelsPerPoint);
        int padding = GetInt(args, "padding", 0);
        Rect rect = resolved.Element.worldBound;
        var cropRect = new RectInt(
            Mathf.Max(0, Mathf.FloorToInt(rect.x * pixelScale) - padding),
            Mathf.Max(0, Mathf.FloorToInt(rect.y * pixelScale) - padding),
            Mathf.Max(1, Mathf.CeilToInt(rect.width * pixelScale) + padding * 2),
            Mathf.Max(1, Mathf.CeilToInt(rect.height * pixelScale) + padding * 2));

        return new Dictionary<string, object>
        {
            { "success", true },
            { "runtime", runtime },
            { "context", resolved.Context },
            { "element", BuildElementInfo(resolved.Element, resolved.ElementPath, true) },
            { "pixelScale", SafeFloat(pixelScale) },
            { "padding", padding },
            { "cropRect", RectToDictionary(new Rect(cropRect.x, cropRect.y, cropRect.width, cropRect.height)) },
            { "panelRect", RectToDictionary(resolved.Root.worldBound) },
            { "window", resolved.WindowName },
        };
    }

    public static object CaptureUIToolkitElement(Dictionary<string, object> args)
    {
        bool runtime = GetBool(args, "runtime", false);
        string outputPath = GetString(args, "outputPath");
        if (string.IsNullOrEmpty(outputPath))
            outputPath = $"Temp/VmAutomation_UIToolkitElement_{DateTime.Now:yyyyMMdd_HHmmss}.png";

        string fullWindowPath = GetString(args, "windowOutputPath");
        if (string.IsNullOrEmpty(fullWindowPath))
            fullWindowPath = Path.Combine(Path.GetDirectoryName(outputPath) ?? "Temp",
                Path.GetFileNameWithoutExtension(outputPath) + "_window.png").Replace('\\', '/');

        UnityEngine.UIElements.VisualElement root;
        UnityEngine.UIElements.VisualElement element;
        string elementPath;
        string error;
        string windowName;
        EditorWindow captureWindow = null;
        Dictionary<string, object> context;

        if (runtime)
        {
            var document = FindRuntimeUIDocument(args, out error);
            if (document == null)
                return new { error };

            root = document.rootVisualElement;
            if (root == null)
                return new { error = $"UIDocument '{document.name}' has no rootVisualElement" };

            element = FindRuntimeElement(args, document, out elementPath, out error);
            if (element == null)
                return new { error };

            windowName = GetString(args, "window");
            if (string.IsNullOrEmpty(windowName))
                windowName = "Game";
            context = BuildUIDocumentInfo(document);
        }
        else
        {
            var window = FindEditorWindow(args, out error, out string selectionCode);
            captureWindow = window;
            if (window == null)
                return VmAutomationResponse.Error(error, selectionCode);

            root = window.rootVisualElement;
            if (root == null)
                return new { error = $"EditorWindow '{window.GetType().FullName}' has no rootVisualElement" };

            element = FindEditorElement(root, args, out elementPath, out error);
            if (element == null)
                return new { error };

            windowName = window.GetType().FullName;
            context = BuildWindowInfo(window);
        }

        Rect rect = element.worldBound;
        Rect rootRect = root.worldBound;
        float pixelScale = GetFloat(args, "pixelScale", EditorGUIUtility.pixelsPerPoint);
        int padding = GetInt(args, "padding", 0);

        var captureArgs = new Dictionary<string, object>
        {
            { "window", windowName },
            { "path", fullWindowPath },
            { "captureMode", runtime ? "screen" : "auto" },
        };
        Dictionary<string, object> capture = runtime
            ? VmAutomationGameViewCaptureCommands.CaptureGameViewRenderTexture(fullWindowPath)
            : VmAutomationScreenshotCommands.CaptureEditorWindow(captureWindow, captureArgs) as Dictionary<string, object>;
        if (runtime && (capture == null || GetBool(capture, "success", false) == false))
        {
            string renderTextureError = capture == null
                ? "Game View render-texture capture returned no result."
                : GetString(capture, "error");
            capture = VmAutomationScreenshotCommands.CaptureEditorWindow(captureArgs) as Dictionary<string, object>;
            if (capture != null)
            {
                capture["fallbackFrom"] = "game-view-render-texture";
                capture["fallbackReason"] = renderTextureError;
            }
        }

        if (capture == null || GetBool(capture, "success", false) == false)
            return capture ?? new Dictionary<string, object> { { "success", false }, { "error", "Window capture failed" } };

        int captureWidth = GetInt(capture, "width", 0);
        int captureHeight = GetInt(capture, "height", 0);
        RectInt contentRect = ReadCaptureContentRect(capture, captureWidth, captureHeight);
        RectInt cropRect;
        string cropMode;
        if (captureWidth > 0 && captureHeight > 0 && rootRect.width > 0 && rootRect.height > 0)
        {
            cropRect = MapWorldRectToCapture(rect, rootRect, contentRect, captureWidth, captureHeight);
            cropRect = ClampRectToImage(new RectInt(
                cropRect.x - padding,
                cropRect.y - padding,
                cropRect.width + padding * 2,
                cropRect.height + padding * 2), captureWidth, captureHeight);
            cropMode = "root-relative-content";
        }
        else
        {
            cropRect = new RectInt(
                Mathf.Max(0, Mathf.FloorToInt(rect.x * pixelScale) - padding),
                Mathf.Max(0, Mathf.FloorToInt(rect.y * pixelScale) - padding),
                Mathf.Max(1, Mathf.CeilToInt(rect.width * pixelScale) + padding * 2),
                Mathf.Max(1, Mathf.CeilToInt(rect.height * pixelScale) + padding * 2));
            cropMode = "absolute";
        }

        var cropArgs = new Dictionary<string, object>
        {
            { "sourcePath", fullWindowPath },
            { "outputPath", outputPath },
            { "originTopLeft", true },
            { "rect", new Dictionary<string, object>
                {
                    { "x", cropRect.x },
                    { "y", cropRect.y },
                    { "width", cropRect.width },
                    { "height", cropRect.height },
                }
            },
        };
        var crop = VmAutomationScreenshotCommands.CropImage(cropArgs);
        bool cropSucceeded = crop is Dictionary<string, object> cropDictionary &&
                             GetBool(cropDictionary, "success", false);

        return new Dictionary<string, object>
        {
            { "success", cropSucceeded },
            { "runtime", runtime },
            { "context", context },
            { "element", BuildElementInfo(element, elementPath, true) },
            { "pixelScale", SafeFloat(pixelScale) },
            { "padding", padding },
            { "cropMode", cropMode },
            { "cropRect", RectToDictionary(new Rect(cropRect.x, cropRect.y, cropRect.width, cropRect.height)) },
            { "windowCapture", capture },
            { "elementCapture", crop },
            { "error", cropSucceeded ? "" : "Element crop failed. See elementCapture for details." },
            { "warning", runtime && capture.ContainsKey("fallbackFrom")
                ? "Game View render-texture capture was unavailable; used an on-screen window fallback."
                : "" },
        };
    }

    public static object InspectUIToolkitGeneratedChildren(Dictionary<string, object> args)
    {
        bool runtime = GetBool(args, "runtime", false);
        var resolved = ResolveUIToolkitElement(args, runtime);
        if (resolved.Error != null)
            return VmAutomationResponse.Error(resolved.Error, resolved.ErrorCode ?? "error");

        int maxDepth = Math.Max(1, GetInt(args, "maxDepth", 4));
        bool includeAll = GetBool(args, "includeAll", false);
        var forbiddenClassContains = GetStringList(args, "forbiddenClassContains", "forbiddenClassContains")
            .Where(value => string.IsNullOrEmpty(value) == false)
            .ToList();
        var forbiddenTypeContains = GetStringList(args, "forbiddenTypeContains", "forbiddenTypeContains")
            .Where(value => string.IsNullOrEmpty(value) == false)
            .ToList();

        var children = new List<Dictionary<string, object>>();
        CollectGeneratedChildren(resolved.Root, resolved.Element, resolved.ElementPath, 0, maxDepth,
            includeAll, forbiddenClassContains, forbiddenTypeContains, children);

        int generatedCount = children.Count(child => GetBool(child, "generated", false));
        int warningCount = children.Count(child => ((List<string>)child["warnings"]).Count > 0);

        return new Dictionary<string, object>
        {
            { "success", true },
            { "valid", warningCount == 0 },
            { "runtime", runtime },
            { "context", resolved.Context },
            { "element", BuildElementInfo(resolved.Element, resolved.ElementPath, true) },
            { "maxDepth", maxDepth },
            { "includeAll", includeAll },
            { "childCount", children.Count },
            { "generatedCount", generatedCount },
            { "warningCount", warningCount },
            { "children", children },
        };
    }

    public static object RepaintRuntimeUI(Dictionary<string, object> args)
    {
        var document = FindRuntimeUIDocument(args, out string error);
        if (document == null)
            return new { error };

        var element = FindRuntimeElement(args, document, out string elementPath, out error);
        if (element == null && HasElementLocator(args))
            return new { error };

        if (element != null)
            element.MarkDirtyRepaint();

        document.rootVisualElement?.MarkDirtyRepaint();
        EditorApplication.QueuePlayerLoopUpdate();

        return new Dictionary<string, object>
        {
            { "success", true },
            { "document", BuildUIDocumentInfo(document) },
            { "repaintedPath", element == null ? "root" : elementPath },
        };
    }

    public static void RefreshUIToolkit(Dictionary<string, object> args, Action<object> resolve)
    {
        bool refreshAssets = GetBool(args, "refreshAssets", true);
        bool forceSynchronousImport = GetBool(args, "forceSynchronousImport", true);
        if (refreshAssets)
        {
            var options = forceSynchronousImport
                ? ImportAssetOptions.ForceSynchronousImport
                : ImportAssetOptions.Default;
            AssetDatabase.Refresh(options);
        }

        int timeoutMs = Math.Max(1, GetInt(args, "timeoutMs", 10000));
        int stableFrames = Math.Max(1, GetInt(args, "stableFrames", 2));
        double startTime = EditorApplication.timeSinceStartup;
        int frameCount = 0;
        int stableFrameCount = 0;
        bool resolved = false;

        void Resolve(object result)
        {
            if (resolved)
                return;

            resolved = true;
            resolve(result);
        }

        void Tick()
        {
            frameCount++;
            int documentCount = MarkAllUIToolkitDirty();
            bool idle = !EditorApplication.isCompiling && !EditorApplication.isUpdating;
            stableFrameCount = idle ? stableFrameCount + 1 : 0;
            double elapsedMs = (EditorApplication.timeSinceStartup - startTime) * 1000d;

            if (stableFrameCount >= stableFrames)
            {
                EditorApplication.update -= Tick;
                Resolve(BuildUIToolkitRefreshResult(true, false, elapsedMs, frameCount, documentCount));
                return;
            }

            if (elapsedMs >= timeoutMs)
            {
                EditorApplication.update -= Tick;
                Resolve(BuildUIToolkitRefreshResult(false, true, elapsedMs, frameCount, documentCount));
            }
        }

        Tick();
        if (!resolved)
            EditorApplication.update += Tick;
    }


    }
}
