using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static VMUnityAutomation.Editor.VmAutomationUICommandArguments;
using static VMUnityAutomation.Editor.VmAutomationUIBuilderPreviewCommands;
using static VMUnityAutomation.Editor.VmAutomationUIToolkitAssetCommands;
using static VMUnityAutomation.Editor.VmAutomationUIToolkitElementUtility;

namespace VMUnityAutomation.Editor
{
    public static class VmAutomationUIToolkitCommands
    {
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
        var window = FindEditorWindow(args, out string error);
        if (window == null)
            return new { error };

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
        var window = FindEditorWindow(args, out string error);
        if (window == null)
            return new { error };

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
        var window = FindEditorWindow(args, out string error);
        if (window == null)
            return new { error };

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
        var window = FindEditorWindow(args, out string error);
        if (window == null)
            return new { error };

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

    public static object DiagnoseRuntimeUI(Dictionary<string, object> args)
    {
        var document = FindRuntimeUIDocument(args, out string error);
        if (document == null)
            return new { error };

        var root = document.rootVisualElement;
        if (root == null)
            return new { error = $"UIDocument '{document.name}' has no rootVisualElement" };

        var checks = new List<Dictionary<string, object>>();
        var queryObjects = GetObjectList(args, "queries");
        if (queryObjects.Count == 0)
            queryObjects.Add(args);

        for (int i = 0; i < queryObjects.Count; i++)
        {
            var query = AsDictionary(queryObjects[i]);
            var element = FindRuntimeElement(query, document, out string elementPath, out string elementError);
            var check = new Dictionary<string, object>
            {
                { "index", i },
                { "query", query },
                { "found", element != null },
                { "path", elementPath },
                { "error", element == null ? elementError : "" },
            };

            if (element != null)
            {
                check["element"] = BuildElementInfo(element, elementPath, true);
                check["parent"] = element.parent == null
                    ? null
                    : BuildElementInfo(element.parent, GetElementPath(root, element.parent), false);
                check["children"] = element.Children()
                    .Select(child => BuildElementInfo(child, GetElementPath(root, child), false))
                    .ToList();
                check["pixel"] = BuildPixelInfo(element, GetFloat(query, "pixelScale",
                    GetFloat(args, "pixelScale", 1f)));
                check["backgroundScale"] = BuildBackgroundScaleInfo(element);
            }

            checks.Add(check);
        }

        return new Dictionary<string, object>
        {
            { "success", true },
            { "document", BuildUIDocumentInfo(document) },
            { "valid", checks.All(check => GetBool(check, "found", false)) },
            { "count", checks.Count },
            { "checks", checks },
        };
    }

    public static object VisualCheckRuntimeUI(Dictionary<string, object> args)
    {
        var document = FindRuntimeUIDocument(args, out string error);
        if (document == null)
            return new { error };

        var root = document.rootVisualElement;
        if (root == null)
            return new { error = $"UIDocument '{document.name}' has no rootVisualElement" };

        var checkArgs = GetObjectList(args, "checks");
        if (checkArgs.Count == 0)
            checkArgs.Add(args);

        float defaultPixelScale = GetFloat(args, "pixelScale", 1f);
        float defaultTolerance = GetFloat(args, "tolerance", 0.01f);
        var results = new List<Dictionary<string, object>>();
        bool valid = true;

        for (int i = 0; i < checkArgs.Count; i++)
        {
            var check = AsDictionary(checkArgs[i]);
            string kind = GetString(check, "type");
            if (string.IsNullOrEmpty(kind))
                kind = GetString(check, "kind");
            if (string.IsNullOrEmpty(kind))
                kind = "pixel-grid";

            var element = FindAssertionElement(root, check, "", out string path, out string elementError);
            var result = new Dictionary<string, object>
            {
                { "index", i },
                { "type", kind },
                { "path", path },
            };

            if (element == null)
            {
                result["passed"] = false;
                result["error"] = elementError;
                results.Add(result);
                valid = false;
                continue;
            }

            switch (kind.ToLowerInvariant())
            {
                case "pixel-grid":
                case "pixel":
                    AddPixelGridResult(result, element,
                        GetFloat(check, "pixelScale", defaultPixelScale),
                        GetFloat(check, "tolerance", defaultTolerance));
                    break;
                case "background-scale":
                case "sprite-scale":
                case "texture-scale":
                    AddBackgroundScaleResult(result, element,
                        GetFloat(check, "expectedScale", GetFloat(check, "scale", defaultPixelScale)),
                        GetFloat(check, "tolerance", defaultTolerance));
                    break;
                case "size":
                    AddRuntimeSizeResult(result, element, check, defaultTolerance);
                    break;
                default:
                    result["passed"] = false;
                    result["error"] = $"Unknown visual check type '{kind}'";
                    break;
            }

            if (GetBool(result, "passed", false) == false)
                valid = false;

            results.Add(result);
        }

        return new Dictionary<string, object>
        {
            { "success", true },
            { "valid", valid },
            { "document", BuildUIDocumentInfo(document) },
            { "count", results.Count },
            { "results", results },
        };
    }

    public static object LocateUIToolkitElement(Dictionary<string, object> args)
    {
        bool runtime = GetBool(args, "runtime", false);
        var resolved = ResolveUIToolkitElement(args, runtime);
        if (resolved.Error != null)
            return new { error = resolved.Error };

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
            var window = FindEditorWindow(args, out error);
            if (window == null)
                return new { error };

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
            : VmAutomationScreenshotCommands.CaptureEditorWindow(captureArgs) as Dictionary<string, object>;
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

    public static object CompareUIToolkitElement(Dictionary<string, object> args)
    {
        string referencePath = GetString(args, "referencePath");
        if (string.IsNullOrEmpty(referencePath))
            return new { error = "referencePath is required" };

        string actualPath = GetString(args, "actualPath");
        if (string.IsNullOrEmpty(actualPath))
            actualPath = $"Temp/VmAutomation_UIToolkitCompare_{DateTime.Now:yyyyMMdd_HHmmss}.png";

        var captureArgs = new Dictionary<string, object>(args)
        {
            ["outputPath"] = actualPath,
        };
        var capture = CaptureUIToolkitElement(captureArgs) as Dictionary<string, object>;
        if (capture == null || GetBool(capture, "success", false) == false)
            return capture ?? new Dictionary<string, object> { { "success", false }, { "error", "Element capture failed" } };

        var compareArgs = new Dictionary<string, object>
        {
            { "referencePath", referencePath },
            { "actualPath", actualPath },
            { "tolerance", GetFloat(args, "tolerance", 0) },
            { "maxSamples", GetInt(args, "maxSamples", 20) },
        };

        string diffOutputPath = GetString(args, "diffOutputPath");
        if (string.IsNullOrEmpty(diffOutputPath) == false)
            compareArgs["diffOutputPath"] = diffOutputPath;

        if (args.TryGetValue("referenceRect", out object referenceRect))
            compareArgs["referenceRect"] = referenceRect;
        if (args.TryGetValue("expectedRect", out object expectedRect))
            compareArgs["expectedRect"] = expectedRect;
        if (args.TryGetValue("actualRect", out object actualRect))
            compareArgs["actualRect"] = actualRect;

        var comparison = VmAutomationGraphicsCommands.CompareImages(compareArgs);
        return new Dictionary<string, object>
        {
            { "success", true },
            { "referencePath", referencePath },
            { "actualPath", actualPath },
            { "capture", capture },
            { "comparison", comparison },
        };
    }

    public static object InspectUIToolkitGeneratedChildren(Dictionary<string, object> args)
    {
        bool runtime = GetBool(args, "runtime", false);
        var resolved = ResolveUIToolkitElement(args, runtime);
        if (resolved.Error != null)
            return new { error = resolved.Error };

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

    public static object AuditUIToolkitResources(Dictionary<string, object> args)
    {
        bool runtime = GetBool(args, "runtime", false);
        int maxDepth = GetInt(args, "maxDepth", 3);
        var queryObjects = GetObjectList(args, "queries");
        if (queryObjects.Count == 0)
            queryObjects.Add(args);

        UnityEngine.UIElements.VisualElement root;
        Dictionary<string, object> context;
        string setupError;
        UnityEngine.UIElements.UIDocument document = null;
        if (runtime)
        {
            document = FindRuntimeUIDocument(args, out setupError);
            if (document == null)
                return new { error = setupError };

            root = document.rootVisualElement;
            if (root == null)
                return new { error = $"UIDocument '{document.name}' has no rootVisualElement" };

            context = BuildUIDocumentInfo(document);
        }
        else
        {
            var window = FindEditorWindow(args, out setupError);
            if (window == null)
                return new { error = setupError };

            root = window.rootVisualElement;
            if (root == null)
                return new { error = $"EditorWindow '{window.GetType().FullName}' has no rootVisualElement" };

            context = BuildWindowInfo(window);
        }

        var audits = new List<Dictionary<string, object>>();
        bool valid = true;
        for (int i = 0; i < queryObjects.Count; i++)
        {
            var query = AsDictionary(queryObjects[i]);
            UnityEngine.UIElements.VisualElement element;
            string path;
            string error;
            if (runtime)
                element = FindRuntimeElement(query, document, out path, out error);
            else
                element = FindEditorElement(root, query, out path, out error);

            var audit = new Dictionary<string, object>
            {
                { "index", i },
                { "query", query },
                { "found", element != null },
                { "path", path },
                { "error", element == null ? error : "" },
            };

            if (element == null)
            {
                valid = false;
                audits.Add(audit);
                continue;
            }

            audit["element"] = BuildElementInfo(element, path, true);
            audit["resources"] = CollectElementResources(root, element, path, maxDepth);
            audit["warnings"] = BuildResourceWarnings(element, query);
            if (((List<string>)audit["warnings"]).Count > 0)
                valid = false;

            audits.Add(audit);
        }

        return new Dictionary<string, object>
        {
            { "success", true },
            { "valid", valid },
            { "runtime", runtime },
            { "context", context },
            { "count", audits.Count },
            { "audits", audits },
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
