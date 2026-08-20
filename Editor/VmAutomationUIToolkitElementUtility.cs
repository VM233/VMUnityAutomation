using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using static VMUnityAutomation.Editor.VmAutomationUICommandArguments;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationUIToolkitElementUtility
    {
    internal static Dictionary<string, object> BuildWindowInfo(EditorWindow window)
    {
        var root = window.rootVisualElement;
        return new Dictionary<string, object>
        {
            { "instanceId", VmObjectId.Get(window) },
            { "title", window.titleContent?.text ?? "" },
            { "type", window.GetType().Name },
            { "fullType", window.GetType().FullName },
            { "hasRootVisualElement", root != null },
            { "rootChildCount", root?.childCount ?? 0 },
        };
    }

    private sealed class ResolvedUIToolkitElement
    {
        public UnityEngine.UIElements.VisualElement Root;
        public UnityEngine.UIElements.VisualElement Element;
        public string ElementPath;
        public string WindowName;
        public Dictionary<string, object> Context;
        public string Error;
    }

    internal static ResolvedUIToolkitElement ResolveUIToolkitElement(Dictionary<string, object> args, bool runtime)
    {
        var resolved = new ResolvedUIToolkitElement();
        if (runtime)
        {
            var document = FindRuntimeUIDocument(args, out string error);
            if (document == null)
            {
                resolved.Error = error;
                return resolved;
            }

            resolved.Root = document.rootVisualElement;
            if (resolved.Root == null)
            {
                resolved.Error = $"UIDocument '{document.name}' has no rootVisualElement";
                return resolved;
            }

            resolved.Element = FindRuntimeElement(args, document, out resolved.ElementPath, out error);
            if (resolved.Element == null)
            {
                resolved.Error = error;
                return resolved;
            }

            resolved.WindowName = GetString(args, "window");
            if (string.IsNullOrEmpty(resolved.WindowName))
                resolved.WindowName = "Game";
            resolved.Context = BuildUIDocumentInfo(document);
            return resolved;
        }

        var window = FindEditorWindow(args, out string editorError);
        if (window == null)
        {
            resolved.Error = editorError;
            return resolved;
        }

        resolved.Root = window.rootVisualElement;
        if (resolved.Root == null)
        {
            resolved.Error = $"EditorWindow '{window.GetType().FullName}' has no rootVisualElement";
            return resolved;
        }

        resolved.Element = FindEditorElement(resolved.Root, args, out resolved.ElementPath, out editorError);
        if (resolved.Element == null)
        {
            resolved.Error = editorError;
            return resolved;
        }

        resolved.WindowName = window.GetType().FullName;
        resolved.Context = BuildWindowInfo(window);
        return resolved;
    }

    internal static UnityEngine.UIElements.VisualElement FindEditorElement(
        UnityEngine.UIElements.VisualElement root, Dictionary<string, object> args,
        out string elementPath, out string error)
    {
        elementPath = "root";
        error = "";

        if (root == null)
        {
            error = "EditorWindow has no rootVisualElement";
            return null;
        }

        string path = GetString(args, "path");
        if (string.IsNullOrEmpty(path) == false)
        {
            var element = GetElementByFlexiblePath(root, path);
            if (element != null)
            {
                elementPath = GetElementPath(root, element);
                return element;
            }

            error = $"UI Toolkit element path '{path}' was not found";
            return null;
        }

        var visualElementPath = GetVisualElementPathNames(args, "");
        if (visualElementPath.Count > 0)
        {
            var element = GetElementByVisualElementPath(root, visualElementPath);
            if (element != null)
            {
                elementPath = GetElementPath(root, element);
                return element;
            }

            error = $"VisualElementPath '{string.Join("/", visualElementPath)}' was not found";
            return null;
        }

        string name = GetString(args, "name");
        string className = GetString(args, "className");
        string typeName = GetString(args, "typeName");
        string text = GetString(args, "text");
        if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(className) &&
            string.IsNullOrEmpty(typeName) && string.IsNullOrEmpty(text))
            return root;

        var results = new List<Dictionary<string, object>>();
        QueryElements(root, "root", name, className, typeName, text, false, 1, results);
        if (results.Count == 0)
        {
            error = "No UI Toolkit element matched the supplied query filters";
            return null;
        }

        elementPath = results[0]["path"].ToString();
        return GetElementByPath(root, elementPath);
    }

    internal static List<Dictionary<string, object>> CollectElementResources(
        UnityEngine.UIElements.VisualElement root, UnityEngine.UIElements.VisualElement element,
        string elementPath, int maxDepth)
    {
        var results = new List<Dictionary<string, object>>();
        CollectElementResources(root, element, elementPath, 0, Math.Max(0, maxDepth), results);
        return results;
    }

    internal static void CollectElementResources(
        UnityEngine.UIElements.VisualElement root, UnityEngine.UIElements.VisualElement element,
        string elementPath, int depth, int maxDepth, List<Dictionary<string, object>> results)
    {
        if (element == null)
            return;

        var backgroundObject = GetBackgroundObject(element);
        bool hasBackground = backgroundObject != null;
        if (hasBackground || depth == 0)
        {
            results.Add(new Dictionary<string, object>
            {
                { "path", string.IsNullOrEmpty(elementPath) ? GetElementPath(root, element) : elementPath },
                { "name", element.name ?? "" },
                { "type", element.GetType().Name },
                { "classes", element.GetClasses().ToList() },
                { "text", GetElementText(element) },
                { "worldBound", RectToDictionary(element.worldBound) },
                { "hasBackground", hasBackground },
                { "background", hasBackground ? BuildUnityObjectInfo(backgroundObject) : null },
                { "backgroundScale", BuildBackgroundScaleInfo(element) },
                { "pickingMode", element.pickingMode.ToString() },
                { "display", element.resolvedStyle.display.ToString() },
                { "visibility", element.resolvedStyle.visibility.ToString() },
                { "opacity", SafeFloat(element.resolvedStyle.opacity) },
            });
        }

        if (depth >= maxDepth)
            return;

        int childIndex = 0;
        foreach (var child in element.Children())
        {
            string childPath = string.IsNullOrEmpty(elementPath)
                ? GetElementPath(root, child)
                : $"{elementPath}/{childIndex}";
            CollectElementResources(root, child, childPath, depth + 1, maxDepth, results);
            childIndex++;
        }
    }

    internal static List<string> BuildResourceWarnings(
        UnityEngine.UIElements.VisualElement element, Dictionary<string, object> args)
    {
        var warnings = new List<string>();
        var backgroundObject = GetBackgroundObject(element);
        string backgroundPath = backgroundObject != null ? AssetDatabase.GetAssetPath(backgroundObject) : "";
        string backgroundName = backgroundObject != null ? backgroundObject.name : "";

        string expectedContains = GetString(args, "expectedBackgroundContains");
        if (string.IsNullOrEmpty(expectedContains) == false &&
            backgroundPath.IndexOf(expectedContains, StringComparison.OrdinalIgnoreCase) < 0 &&
            backgroundName.IndexOf(expectedContains, StringComparison.OrdinalIgnoreCase) < 0)
        {
            warnings.Add($"Background does not contain expected text '{expectedContains}'. Actual='{backgroundPath}' '{backgroundName}'");
        }

        foreach (string forbidden in GetStringList(args, "forbiddenBackgroundContains", "forbiddenBackgroundContains"))
        {
            if (string.IsNullOrEmpty(forbidden))
                continue;

            if (backgroundPath.IndexOf(forbidden, StringComparison.OrdinalIgnoreCase) >= 0 ||
                backgroundName.IndexOf(forbidden, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                warnings.Add($"Background contains forbidden text '{forbidden}'. Actual='{backgroundPath}' '{backgroundName}'");
            }
        }

        bool requireBackground = GetBool(args, "requireBackground", false);
        if (requireBackground && backgroundObject == null)
            warnings.Add("Element has no resolved background image.");

        if (GetBool(args, "warnHighlighted", true) &&
            (backgroundPath.IndexOf("highlight", StringComparison.OrdinalIgnoreCase) >= 0 ||
             backgroundName.IndexOf("highlight", StringComparison.OrdinalIgnoreCase) >= 0))
        {
            warnings.Add($"Element appears to use a highlighted background in normal state: '{backgroundPath}' '{backgroundName}'");
        }

        return warnings;
    }

    internal static void CollectGeneratedChildren(
        UnityEngine.UIElements.VisualElement root,
        UnityEngine.UIElements.VisualElement element,
        string elementPath,
        int depth,
        int maxDepth,
        bool includeAll,
        List<string> forbiddenClassContains,
        List<string> forbiddenTypeContains,
        List<Dictionary<string, object>> results)
    {
        if (element == null || depth >= maxDepth)
            return;

        int childIndex = 0;
        foreach (var child in element.Children())
        {
            string childPath = $"{elementPath}/{childIndex}";
            var generatedReasons = GetGeneratedChildReasons(child);
            var warnings = GetGeneratedChildWarnings(child, forbiddenClassContains, forbiddenTypeContains);
            bool generated = generatedReasons.Count > 0;

            if (includeAll || generated || warnings.Count > 0)
            {
                var info = BuildElementInfo(child, childPath, true);
                info["depth"] = depth + 1;
                info["generated"] = generated;
                info["generatedReasons"] = generatedReasons;
                info["warnings"] = warnings;
                info["resources"] = CollectElementResources(root, child, childPath, 1);
                results.Add(info);
            }

            CollectGeneratedChildren(root, child, childPath, depth + 1, maxDepth, includeAll,
                forbiddenClassContains, forbiddenTypeContains, results);
            childIndex++;
        }
    }

    internal static List<string> GetGeneratedChildReasons(UnityEngine.UIElements.VisualElement element)
    {
        var reasons = new List<string>();
        var classes = element.GetClasses().ToList();
        string typeName = element.GetType().Name;
        string fullTypeName = element.GetType().FullName ?? "";

        if (string.IsNullOrEmpty(element.name) && classes.Any(className =>
                className.StartsWith("unity-", StringComparison.OrdinalIgnoreCase)))
        {
            reasons.Add("unnamed-unity-class");
        }

        if (classes.Any(className => className.Contains("__")))
            reasons.Add("unity-subpart-class");

        if (fullTypeName.StartsWith("UnityEngine.UIElements.", StringComparison.Ordinal) &&
            IsKnownGeneratedUIToolkitType(typeName))
        {
            reasons.Add("known-generated-type");
        }

        if (classes.Any(IsKnownGeneratedIndicatorClass))
            reasons.Add("known-generated-indicator-class");

        return reasons.Distinct().ToList();
    }

    internal static List<string> GetGeneratedChildWarnings(UnityEngine.UIElements.VisualElement element,
        List<string> forbiddenClassContains, List<string> forbiddenTypeContains)
    {
        var warnings = new List<string>();
        var classes = element.GetClasses().ToList();
        string typeName = element.GetType().Name;
        string fullTypeName = element.GetType().FullName ?? "";

        foreach (string forbidden in forbiddenClassContains)
        {
            if (classes.Any(className => className.IndexOf(forbidden, StringComparison.OrdinalIgnoreCase) >= 0))
                warnings.Add($"Class contains forbidden text '{forbidden}'");
        }

        foreach (string forbidden in forbiddenTypeContains)
        {
            if (typeName.IndexOf(forbidden, StringComparison.OrdinalIgnoreCase) >= 0 ||
                fullTypeName.IndexOf(forbidden, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                warnings.Add($"Type contains forbidden text '{forbidden}'");
            }
        }

        return warnings;
    }

    internal static bool IsKnownGeneratedUIToolkitType(string typeName)
    {
        if (string.IsNullOrEmpty(typeName))
            return false;

        string[] fragments =
        {
            "Scroller",
            "Slider",
            "Tab",
            "Toggle",
            "Dropdown",
            "Popup",
            "Foldout",
            "ScrollView",
        };

        return fragments.Any(fragment => typeName.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    internal static bool IsKnownGeneratedIndicatorClass(string className)
    {
        if (string.IsNullOrEmpty(className))
            return false;

        string[] fragments =
        {
            "arrow",
            "checkmark",
            "input",
            "dragger",
            "low-button",
            "high-button",
            "unity-scroller",
            "unity-tab",
        };

        return fragments.Any(fragment => className.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    internal static Dictionary<string, object> BuildUIDocumentInfo(UnityEngine.UIElements.UIDocument document)
    {
        var root = document != null ? document.rootVisualElement : null;
        var visualTreeAsset = document != null ? document.visualTreeAsset : null;
        var panelSettings = document != null ? document.panelSettings : null;

        return new Dictionary<string, object>
        {
            { "instanceId", document != null ? VmObjectId.Get(document) : "0" },
            { "name", document != null ? document.name : "" },
            { "enabled", document != null && document.enabled },
            { "gameObjectName", document != null ? document.gameObject.name : "" },
            { "gameObjectPath", document != null ? GetGameObjectPath(document.transform) : "" },
            { "gameObjectActive", document != null && document.gameObject.activeInHierarchy },
            { "visualTreeAsset", visualTreeAsset != null ? visualTreeAsset.name : "" },
            { "visualTreeAssetPath", visualTreeAsset != null ? AssetDatabase.GetAssetPath(visualTreeAsset) : "" },
            { "panelSettings", panelSettings != null ? panelSettings.name : "" },
            { "panelSettingsPath", panelSettings != null ? AssetDatabase.GetAssetPath(panelSettings) : "" },
            { "hasRootVisualElement", root != null },
            { "rootChildCount", root?.childCount ?? 0 },
            { "rootWorldBound", root != null ? RectToDictionary(root.worldBound) : null },
        };
    }

    internal static List<UnityEngine.UIElements.UIDocument> GetRuntimeUIDocuments(bool includeInactive)
    {
        return Resources.FindObjectsOfTypeAll<UnityEngine.UIElements.UIDocument>()
            .Where(document => document != null &&
                               document.gameObject != null &&
                               document.gameObject.scene.IsValid() &&
                               (includeInactive || (document.enabled && document.gameObject.activeInHierarchy)))
            .OrderBy(document => GetGameObjectPath(document.transform), StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    internal static UnityEngine.UIElements.UIDocument FindRuntimeUIDocument(
        Dictionary<string, object> args, out string error)
    {
        error = "";
        bool includeInactive = GetBool(args, "includeInactive", true);
        object instanceId = null;
        TryGetObjectId(args, "documentInstanceId", out instanceId);

        string documentName = GetString(args, "documentName");
        string gameObjectPath = GetString(args, "gameObjectPath");
        string gameObjectName = GetString(args, "gameObjectName");

        if (instanceId != null)
        {
            var obj = VmObjectId.ToObject(instanceId);
            if (obj is UnityEngine.UIElements.UIDocument directDocument)
                return directDocument;
            if (obj is GameObject go)
            {
                var component = go.GetComponent<UnityEngine.UIElements.UIDocument>();
                if (component != null)
                    return component;
            }

            error = $"UIDocument or GameObject instanceId '{instanceId}' was not found";
            return null;
        }

        var documents = GetRuntimeUIDocuments(includeInactive);
        if (string.IsNullOrEmpty(gameObjectPath) == false)
        {
            string normalizedPath = NormalizeGameObjectPath(gameObjectPath);
            documents = documents
                .Where(document => string.Equals(GetGameObjectPath(document.transform), normalizedPath,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (string.IsNullOrEmpty(gameObjectName) == false)
        {
            documents = documents
                .Where(document => string.Equals(document.gameObject.name, gameObjectName,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (string.IsNullOrEmpty(documentName) == false)
        {
            documents = documents
                .Where(document => string.Equals(document.name, documentName, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (documents.Count == 1)
            return documents[0];

        if (documents.Count == 0)
        {
            error = "No runtime UIDocument matched the supplied filters";
            return null;
        }

        if (string.IsNullOrEmpty(gameObjectPath) && string.IsNullOrEmpty(gameObjectName) &&
            string.IsNullOrEmpty(documentName))
        {
            var activeDocuments = documents
                .Where(document => document.enabled && document.gameObject.activeInHierarchy)
                .ToList();
            if (activeDocuments.Count > 0)
                return activeDocuments[0];
        }

        error = $"Multiple runtime UIDocuments matched ({documents.Count}). Pass gameObjectPath, documentName, or documentInstanceId.";
        return null;
    }

    internal static UnityEngine.UIElements.VisualElement FindRuntimeElement(
        Dictionary<string, object> args, UnityEngine.UIElements.UIDocument document,
        out string elementPath, out string error)
    {
        elementPath = "root";
        error = "";

        var root = document.rootVisualElement;
        if (root == null)
        {
            error = $"UIDocument '{document.name}' has no rootVisualElement";
            return null;
        }

        string path = GetString(args, "path");
        if (string.IsNullOrEmpty(path) == false)
        {
            var element = GetElementByFlexiblePath(root, path);
            if (element != null)
            {
                elementPath = GetElementPath(root, element);
                return element;
            }

            error = $"UI Toolkit element path '{path}' was not found";
            return null;
        }

        var visualElementPath = GetVisualElementPathNames(args, "");
        if (visualElementPath.Count > 0)
        {
            var element = GetElementByVisualElementPath(root, visualElementPath);
            if (element != null)
            {
                elementPath = GetElementPath(root, element);
                return element;
            }

            error = $"VisualElementPath '{string.Join("/", visualElementPath)}' was not found";
            return null;
        }

        string name = GetString(args, "name");
        string className = GetString(args, "className");
        string typeName = GetString(args, "typeName");
        string text = GetString(args, "text");
        if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(className) &&
            string.IsNullOrEmpty(typeName) && string.IsNullOrEmpty(text))
            return root;

        var results = new List<Dictionary<string, object>>();
        QueryElements(root, "root", name, className, typeName, text, false, 1, results);
        if (results.Count == 0)
        {
            error = "No UI Toolkit element matched the supplied query filters";
            return null;
        }

        elementPath = results[0]["path"].ToString();
        return GetElementByPath(root, elementPath);
    }

    internal static bool HasElementLocator(Dictionary<string, object> args)
    {
        return string.IsNullOrEmpty(GetString(args, "path")) == false ||
               string.IsNullOrEmpty(GetString(args, "visualElementPath")) == false ||
               GetStringList(args, "visualElementNames", "").Count > 0;
    }

    internal static int MarkAllUIToolkitDirty()
    {
        int documentCount = 0;
        foreach (var document in GetRuntimeUIDocuments(true))
        {
            if (document.rootVisualElement == null)
                continue;

            document.rootVisualElement.MarkDirtyRepaint();
            documentCount++;
        }

        foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>().Where(window => window != null))
        {
            window.rootVisualElement?.MarkDirtyRepaint();
            window.Repaint();
        }

        SceneView.RepaintAll();
        return documentCount;
    }

    internal static Dictionary<string, object> BuildUIToolkitRefreshResult(
        bool success, bool timedOut, double elapsedMs, int frameCount, int documentCount)
    {
        return new Dictionary<string, object>
        {
            { "success", success },
            { "timedOut", timedOut },
            { "elapsedMs", Math.Round(elapsedMs, 2) },
            { "frameCount", frameCount },
            { "repaintedRuntimeDocuments", documentCount },
            { "isCompiling", EditorApplication.isCompiling },
            { "isUpdating", EditorApplication.isUpdating },
        };
    }

    internal static string GetGameObjectPath(Transform transform)
    {
        if (transform == null)
            return "";

        var names = new List<string>();
        var current = transform;
        while (current != null)
        {
            names.Add(current.name);
            current = current.parent;
        }

        names.Reverse();
        return string.Join("/", names);
    }

    internal static string NormalizeGameObjectPath(string path)
    {
        return (path ?? "").Trim().Trim('/').Replace('\\', '/');
    }

    internal static EditorWindow FindEditorWindow(Dictionary<string, object> args, out string error)
    {
        error = "";
        TryGetObjectId(args, "instanceId", out object instanceId);
        string windowQuery = GetString(args, "window");
        string typeQuery = GetString(args, "windowType");
        string titleQuery = GetString(args, "title");
        if (string.IsNullOrEmpty(typeQuery))
            typeQuery = GetString(args, "type");

        if (instanceId == null && IsObjectIdString(windowQuery))
            instanceId = windowQuery;

        if (instanceId != null)
        {
            var obj = VmObjectId.ToObject(instanceId) as EditorWindow;
            if (obj != null)
                return obj;

            error = $"EditorWindow instanceId '{instanceId}' was not found";
            return null;
        }

        if (string.IsNullOrEmpty(windowQuery) && string.IsNullOrEmpty(typeQuery) && string.IsNullOrEmpty(titleQuery))
        {
            if (EditorWindow.focusedWindow != null)
                return EditorWindow.focusedWindow;

            error = "No focused EditorWindow. Pass instanceId, window, windowType, or title.";
            return null;
        }

        var windows = Resources.FindObjectsOfTypeAll<EditorWindow>().Where(window => window != null).ToList();
        foreach (var window in windows)
        {
            if (MatchesWindow(window, windowQuery, typeQuery, titleQuery, true))
                return window;
        }

        foreach (var window in windows)
        {
            if (MatchesWindow(window, windowQuery, typeQuery, titleQuery, false))
                return window;
        }

        error = $"No EditorWindow matched window='{windowQuery}', windowType='{typeQuery}', title='{titleQuery}'";
        return null;
    }

    internal static EditorWindow FindUIBuilderWindow()
    {
        var windows = Resources.FindObjectsOfTypeAll<EditorWindow>().Where(window => window != null).ToList();
        foreach (var window in windows)
        {
            string title = window.titleContent?.text ?? "";
            if (string.Equals(title, "UI Builder", StringComparison.OrdinalIgnoreCase))
                return window;
        }

        foreach (var window in windows)
        {
            string title = window.titleContent?.text ?? "";
            string typeName = window.GetType().FullName ?? window.GetType().Name;
            if (title.IndexOf("UI Builder", StringComparison.OrdinalIgnoreCase) >= 0 ||
                typeName.IndexOf("UIBuilder", StringComparison.OrdinalIgnoreCase) >= 0 ||
                typeName.IndexOf("Builder", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return window;
            }
        }

        return null;
    }


    internal static bool MatchesWindow(EditorWindow window, string windowQuery, string typeQuery, string titleQuery, bool exact)
    {
        string title = window.titleContent?.text ?? "";
        string typeName = window.GetType().Name;
        string fullTypeName = window.GetType().FullName ?? "";

        if (!string.IsNullOrEmpty(windowQuery) &&
            Matches(title, windowQuery, exact) == false &&
            Matches(typeName, windowQuery, exact) == false &&
            Matches(fullTypeName, windowQuery, exact) == false)
            return false;

        if (!string.IsNullOrEmpty(typeQuery) &&
            Matches(typeName, typeQuery, exact) == false &&
            Matches(fullTypeName, typeQuery, exact) == false)
            return false;

        if (!string.IsNullOrEmpty(titleQuery) && Matches(title, titleQuery, exact) == false)
            return false;

        return true;
    }

    internal static bool Matches(string value, string query, bool exact)
    {
        if (string.IsNullOrEmpty(query))
            return true;

        return exact
            ? string.Equals(value, query, StringComparison.OrdinalIgnoreCase)
            : value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    internal static Dictionary<string, object> BuildElementTree(
        UnityEngine.UIElements.VisualElement element, string path, int depth, int maxDepth, int maxNodes,
        bool includeStyle, ref int count, ref bool truncated)
    {
        count++;
        var info = BuildElementInfo(element, path, includeStyle);
        if (depth >= maxDepth)
            return info;

        var children = new List<Dictionary<string, object>>();
        int childIndex = 0;
        foreach (var child in element.Children())
        {
            if (count >= maxNodes)
            {
                truncated = true;
                break;
            }

            children.Add(BuildElementTree(child, $"{path}/{childIndex}", depth + 1, maxDepth,
                maxNodes, includeStyle, ref count, ref truncated));
            childIndex++;
        }

        info["children"] = children;
        return info;
    }

    internal static Dictionary<string, object> BuildElementInfo(
        UnityEngine.UIElements.VisualElement element, string path, bool includeStyle)
    {
        var info = new Dictionary<string, object>
        {
            { "path", string.IsNullOrEmpty(path) ? "root" : path },
            { "name", element.name ?? "" },
            { "type", element.GetType().Name },
            { "fullType", element.GetType().FullName },
            { "classes", element.GetClasses().ToList() },
            { "text", GetElementText(element) },
            { "tooltip", element.tooltip ?? "" },
            { "visible", element.visible },
            { "enabledSelf", element.enabledSelf },
            { "enabledInHierarchy", element.enabledInHierarchy },
            { "pickingMode", element.pickingMode.ToString() },
            { "childCount", element.childCount },
            { "layout", RectToDictionary(element.layout) },
            { "worldBound", RectToDictionary(element.worldBound) },
        };

        if (includeStyle)
        {
            info["inlineStyle"] = BuildInlineStyleInfo(element);
            info["resolvedStyle"] = BuildResolvedStyleInfo(element);
            info["background"] = BuildBackgroundInfo(element);
        }

        return info;
    }

    internal static void QueryElements(
        UnityEngine.UIElements.VisualElement element, string path, string name, string className,
        string typeName, string text, bool includeStyle, int maxResults, List<Dictionary<string, object>> results)
    {
        if (results.Count >= maxResults)
            return;

        if (MatchesElement(element, name, className, typeName, text))
            results.Add(BuildElementInfo(element, path, includeStyle));

        int childIndex = 0;
        foreach (var child in element.Children())
        {
            QueryElements(child, $"{path}/{childIndex}", name, className, typeName, text,
                includeStyle, maxResults, results);
            if (results.Count >= maxResults)
                return;
            childIndex++;
        }
    }

    internal static bool MatchesElement(UnityEngine.UIElements.VisualElement element, string name,
        string className, string typeName, string text)
    {
        if (!string.IsNullOrEmpty(name) && !string.Equals(element.name, name, StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.IsNullOrEmpty(className) && !element.ClassListContains(className))
            return false;

        if (!string.IsNullOrEmpty(typeName) &&
            !Matches(element.GetType().Name, typeName, false) &&
            !Matches(element.GetType().FullName ?? "", typeName, false))
            return false;

        if (!string.IsNullOrEmpty(text) &&
            GetElementText(element).IndexOf(text, StringComparison.OrdinalIgnoreCase) < 0)
            return false;

        return true;
    }

    internal static UnityEngine.UIElements.VisualElement GetElementByFlexiblePath(
        UnityEngine.UIElements.VisualElement root, string path)
    {
        var element = GetElementByPath(root, path);
        if (element != null)
            return element;

        var names = SplitVisualElementPath(path);
        return names.Count > 0 ? GetElementByVisualElementPath(root, names) : null;
    }

    internal static UnityEngine.UIElements.VisualElement GetElementByVisualElementPath(
        UnityEngine.UIElements.VisualElement root, List<string> names)
    {
        if (root == null || names == null || names.Count == 0)
            return null;

        var current = FindNamedElement(root, names[0], true);
        for (int i = 1; i < names.Count && current != null; i++)
        {
            current = FindNamedElement(current, names[i], false);
        }

        return current;
    }

    internal static UnityEngine.UIElements.VisualElement FindNamedElement(
        UnityEngine.UIElements.VisualElement root, string name, bool includeRoot)
    {
        if (root == null || string.IsNullOrEmpty(name))
            return null;

        if (includeRoot && string.Equals(root.name, name, StringComparison.Ordinal))
            return root;

        foreach (var child in root.Children())
        {
            var result = FindNamedElement(child, name, true);
            if (result != null)
                return result;
        }

        return null;
    }

    internal static List<string> GetVisualElementPathNames(Dictionary<string, object> args, string prefix)
    {
        var names = new List<string>();
        if (args == null)
            return names;

        string visualElementPathKey = string.IsNullOrEmpty(prefix) ? "visualElementPath" : $"{prefix}VisualElementPath";
        if (args.TryGetValue(visualElementPathKey, out object pathValue))
            AddVisualElementPathNames(names, pathValue);

        string namesKey = string.IsNullOrEmpty(prefix) ? "visualElementNames" : $"{prefix}Names";
        foreach (string name in GetStringList(args, namesKey, ""))
            AddVisualElementPathNames(names, name);

        return names.Where(name => string.IsNullOrEmpty(name) == false).ToList();
    }

    internal static void AddVisualElementPathNames(List<string> names, object value)
    {
        if (value == null)
            return;

        if (value is List<object> list)
        {
            foreach (object item in list)
                AddVisualElementPathNames(names, item);
            return;
        }

        if (value is Dictionary<string, object> dictionary)
        {
            foreach (string name in GetStringList(dictionary, "names", "name"))
                AddVisualElementPathNames(names, name);
            return;
        }

        foreach (string name in SplitVisualElementPath(value.ToString()))
        {
            if (names.Contains(name, StringComparer.Ordinal) == false)
                names.Add(name);
        }
    }

    internal static List<string> SplitVisualElementPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return new List<string>();

        return path.Split(new[] { '/', '\\', '>', '.' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Trim())
            .Where(part => string.IsNullOrEmpty(part) == false &&
                           string.Equals(part, "root", StringComparison.OrdinalIgnoreCase) == false)
            .ToList();
    }

    internal static string GetElementPath(UnityEngine.UIElements.VisualElement root,
        UnityEngine.UIElements.VisualElement target)
    {
        if (root == null || target == null)
            return "";

        if (root == target)
            return "root";

        var indexes = new List<int>();
        if (TryBuildElementPath(root, target, indexes) == false)
            return "";

        return "root/" + string.Join("/", indexes);
    }

    internal static bool TryBuildElementPath(UnityEngine.UIElements.VisualElement current,
        UnityEngine.UIElements.VisualElement target, List<int> indexes)
    {
        int childIndex = 0;
        foreach (var child in current.Children())
        {
            indexes.Add(childIndex);
            if (child == target || TryBuildElementPath(child, target, indexes))
                return true;

            indexes.RemoveAt(indexes.Count - 1);
            childIndex++;
        }

        return false;
    }


    internal static UnityEngine.UIElements.VisualElement FindElement(
        Dictionary<string, object> args, EditorWindow window, out string error)
    {
        error = "";
        string path = GetString(args, "path");
        if (!string.IsNullOrEmpty(path))
        {
            var element = GetElementByPath(window.rootVisualElement, path);
            if (element != null)
                return element;

            error = $"UI Toolkit element path '{path}' was not found";
            return null;
        }

        string name = GetString(args, "name");
        string className = GetString(args, "className");
        string typeName = GetString(args, "typeName");
        string text = GetString(args, "text");
        if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(className) &&
            string.IsNullOrEmpty(typeName) && string.IsNullOrEmpty(text))
            return window.rootVisualElement;

        var results = new List<Dictionary<string, object>>();
        QueryElements(window.rootVisualElement, "root", name, className, typeName, text, false, 1, results);
        if (results.Count == 0)
        {
            error = "No UI Toolkit element matched the supplied query filters";
            return null;
        }

        return GetElementByPath(window.rootVisualElement, results[0]["path"].ToString());
    }

    internal static UnityEngine.UIElements.VisualElement GetElementByPath(
        UnityEngine.UIElements.VisualElement root, string path)
    {
        if (root == null)
            return null;

        if (string.IsNullOrEmpty(path) || string.Equals(path, "root", StringComparison.OrdinalIgnoreCase))
            return root;

        var parts = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        var current = root;
        foreach (var part in parts)
        {
            if (string.Equals(part, "root", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!int.TryParse(part, out int index))
                return null;

            int currentIndex = 0;
            UnityEngine.UIElements.VisualElement next = null;
            foreach (var child in current.Children())
            {
                if (currentIndex == index)
                {
                    next = child;
                    break;
                }

                currentIndex++;
            }

            if (next == null)
                return null;

            current = next;
        }

        return current;
    }

    internal static Dictionary<string, object> BuildPixelInfo(
        UnityEngine.UIElements.VisualElement element, float pixelScale)
    {
        var rect = element.worldBound;
        return new Dictionary<string, object>
        {
            { "pixelScale", SafeFloat(pixelScale) },
            { "worldBound", RectToDictionary(rect) },
            { "xOnGrid", IsOnPixelGrid(rect.x, pixelScale, 0.01f) },
            { "yOnGrid", IsOnPixelGrid(rect.y, pixelScale, 0.01f) },
            { "widthOnGrid", IsOnPixelGrid(rect.width, pixelScale, 0.01f) },
            { "heightOnGrid", IsOnPixelGrid(rect.height, pixelScale, 0.01f) },
        };
    }

    internal static Dictionary<string, object> BuildBackgroundScaleInfo(
        UnityEngine.UIElements.VisualElement element)
    {
        var result = new Dictionary<string, object>
        {
            { "hasBackground", false },
        };

        var backgroundObject = GetBackgroundObject(element);
        if (backgroundObject == null)
            return result;

        var sourceSize = GetBackgroundObjectSize(backgroundObject);
        var rect = element.worldBound;
        result["hasBackground"] = true;
        result["background"] = BuildUnityObjectInfo(backgroundObject);
        result["sourceWidth"] = SafeFloat(sourceSize.x);
        result["sourceHeight"] = SafeFloat(sourceSize.y);
        result["renderedWidth"] = SafeFloat(rect.width);
        result["renderedHeight"] = SafeFloat(rect.height);
        result["scaleX"] = sourceSize.x > 0 ? SafeFloat(rect.width / sourceSize.x) : null;
        result["scaleY"] = sourceSize.y > 0 ? SafeFloat(rect.height / sourceSize.y) : null;
        result["uniformScale"] = sourceSize.x > 0 && sourceSize.y > 0
            ? SafeFloat(Math.Abs(rect.width / sourceSize.x - rect.height / sourceSize.y))
            : null;
        return result;
    }

    internal static void AddPixelGridResult(Dictionary<string, object> result,
        UnityEngine.UIElements.VisualElement element, float pixelScale, float tolerance)
    {
        var rect = element.worldBound;
        bool xPassed = IsOnPixelGrid(rect.x, pixelScale, tolerance);
        bool yPassed = IsOnPixelGrid(rect.y, pixelScale, tolerance);
        bool widthPassed = IsOnPixelGrid(rect.width, pixelScale, tolerance);
        bool heightPassed = IsOnPixelGrid(rect.height, pixelScale, tolerance);

        result["passed"] = xPassed && yPassed && widthPassed && heightPassed;
        result["pixelScale"] = SafeFloat(pixelScale);
        result["tolerance"] = SafeFloat(tolerance);
        result["xDelta"] = SafeFloat(GetPixelGridDelta(rect.x, pixelScale));
        result["yDelta"] = SafeFloat(GetPixelGridDelta(rect.y, pixelScale));
        result["widthDelta"] = SafeFloat(GetPixelGridDelta(rect.width, pixelScale));
        result["heightDelta"] = SafeFloat(GetPixelGridDelta(rect.height, pixelScale));
        result["rect"] = RectToDictionary(rect);
    }

    internal static void AddBackgroundScaleResult(Dictionary<string, object> result,
        UnityEngine.UIElements.VisualElement element, float expectedScale, float tolerance)
    {
        var backgroundObject = GetBackgroundObject(element);
        if (backgroundObject == null)
        {
            result["passed"] = false;
            result["error"] = "Element has no resolved background image";
            return;
        }

        var sourceSize = GetBackgroundObjectSize(backgroundObject);
        if (sourceSize.x <= 0 || sourceSize.y <= 0)
        {
            result["passed"] = false;
            result["error"] = "Could not determine background source size";
            result["background"] = BuildUnityObjectInfo(backgroundObject);
            return;
        }

        var rect = element.worldBound;
        float scaleX = rect.width / sourceSize.x;
        float scaleY = rect.height / sourceSize.y;
        bool passed = Math.Abs(scaleX - expectedScale) <= tolerance &&
                      Math.Abs(scaleY - expectedScale) <= tolerance;

        result["passed"] = passed;
        result["expectedScale"] = SafeFloat(expectedScale);
        result["tolerance"] = SafeFloat(tolerance);
        result["scaleX"] = SafeFloat(scaleX);
        result["scaleY"] = SafeFloat(scaleY);
        result["background"] = BuildUnityObjectInfo(backgroundObject);
        result["sourceWidth"] = SafeFloat(sourceSize.x);
        result["sourceHeight"] = SafeFloat(sourceSize.y);
        result["renderedWidth"] = SafeFloat(rect.width);
        result["renderedHeight"] = SafeFloat(rect.height);
    }

    internal static void AddRuntimeSizeResult(Dictionary<string, object> result,
        UnityEngine.UIElements.VisualElement element, Dictionary<string, object> args, float defaultTolerance)
    {
        float expectedWidth = GetFloat(args, "width", float.NaN);
        if (float.IsNaN(expectedWidth))
            expectedWidth = GetFloat(args, "expectedWidth", float.NaN);
        float expectedHeight = GetFloat(args, "height", float.NaN);
        if (float.IsNaN(expectedHeight))
            expectedHeight = GetFloat(args, "expectedHeight", float.NaN);
        float tolerance = GetFloat(args, "tolerance", defaultTolerance);

        var rect = element.worldBound;
        float widthDelta = float.IsNaN(expectedWidth) ? 0 : rect.width - expectedWidth;
        float heightDelta = float.IsNaN(expectedHeight) ? 0 : rect.height - expectedHeight;
        bool widthPassed = float.IsNaN(expectedWidth) || Math.Abs(widthDelta) <= tolerance;
        bool heightPassed = float.IsNaN(expectedHeight) || Math.Abs(heightDelta) <= tolerance;

        result["passed"] = widthPassed && heightPassed;
        result["expectedWidth"] = float.IsNaN(expectedWidth) ? null : (object)expectedWidth;
        result["expectedHeight"] = float.IsNaN(expectedHeight) ? null : (object)expectedHeight;
        result["actualWidth"] = SafeFloat(rect.width);
        result["actualHeight"] = SafeFloat(rect.height);
        result["widthDelta"] = SafeFloat(widthDelta);
        result["heightDelta"] = SafeFloat(heightDelta);
        result["tolerance"] = SafeFloat(tolerance);
        result["rect"] = RectToDictionary(rect);
    }

    internal static bool IsOnPixelGrid(float value, float pixelScale, float tolerance)
    {
        if (pixelScale <= 0)
            return true;

        return Math.Abs(GetPixelGridDelta(value, pixelScale)) <= tolerance;
    }

    internal static float GetPixelGridDelta(float value, float pixelScale)
    {
        if (pixelScale <= 0)
            return 0;

        return value - Mathf.Round(value / pixelScale) * pixelScale;
    }

    internal static UnityEngine.Object GetBackgroundObject(UnityEngine.UIElements.VisualElement element)
    {
        object styleValue = GetPropertyValue(element.resolvedStyle, "backgroundImage");
        object background = GetPropertyValue(styleValue, "value") ?? styleValue;

        return GetPropertyValue(background, "sprite") as UnityEngine.Object
               ?? GetPropertyValue(background, "texture") as UnityEngine.Object
               ?? GetPropertyValue(background, "renderTexture") as UnityEngine.Object
               ?? GetPropertyValue(background, "vectorImage") as UnityEngine.Object;
    }

    internal static Vector2 GetBackgroundObjectSize(UnityEngine.Object backgroundObject)
    {
        switch (backgroundObject)
        {
            case Sprite sprite:
                return sprite.rect.size;
            case Texture texture:
                return new Vector2(texture.width, texture.height);
            default:
                return Vector2.zero;
        }
    }

    internal static Dictionary<string, object> BuildUnityObjectInfo(UnityEngine.Object unityObject)
    {
        return new Dictionary<string, object>
        {
            { "name", unityObject != null ? unityObject.name : "" },
            { "type", unityObject != null ? unityObject.GetType().Name : "" },
            { "instanceId", unityObject != null ? VmObjectId.Get(unityObject) : "0" },
            { "assetPath", unityObject != null ? AssetDatabase.GetAssetPath(unityObject) : "" },
        };
    }

    internal static Dictionary<string, object> BuildInlineStyleInfo(UnityEngine.UIElements.VisualElement element)
    {
        var style = element.style;
        return new Dictionary<string, object>
        {
            { "display", style.display.ToString() },
            { "visibility", style.visibility.ToString() },
            { "position", style.position.ToString() },
            { "left", style.left.ToString() },
            { "top", style.top.ToString() },
            { "right", style.right.ToString() },
            { "bottom", style.bottom.ToString() },
            { "width", style.width.ToString() },
            { "height", style.height.ToString() },
            { "minWidth", style.minWidth.ToString() },
            { "minHeight", style.minHeight.ToString() },
            { "maxWidth", style.maxWidth.ToString() },
            { "maxHeight", style.maxHeight.ToString() },
            { "flexGrow", style.flexGrow.ToString() },
            { "flexShrink", style.flexShrink.ToString() },
            { "flexBasis", style.flexBasis.ToString() },
            { "flexDirection", style.flexDirection.ToString() },
            { "alignItems", style.alignItems.ToString() },
            { "alignSelf", style.alignSelf.ToString() },
            { "justifyContent", style.justifyContent.ToString() },
            { "marginLeft", style.marginLeft.ToString() },
            { "marginTop", style.marginTop.ToString() },
            { "marginRight", style.marginRight.ToString() },
            { "marginBottom", style.marginBottom.ToString() },
            { "paddingLeft", style.paddingLeft.ToString() },
            { "paddingTop", style.paddingTop.ToString() },
            { "paddingRight", style.paddingRight.ToString() },
            { "paddingBottom", style.paddingBottom.ToString() },
            { "backgroundColor", style.backgroundColor.ToString() },
            { "unityBackgroundImageTintColor", style.unityBackgroundImageTintColor.ToString() },
            { "color", style.color.ToString() },
            { "opacity", style.opacity.ToString() },
        };
    }

    internal static Dictionary<string, object> BuildResolvedStyleInfo(UnityEngine.UIElements.VisualElement element)
    {
        var style = element.resolvedStyle;
        return new Dictionary<string, object>
        {
            { "display", style.display.ToString() },
            { "visibility", style.visibility.ToString() },
            { "position", style.position.ToString() },
            { "left", SafeFloat(style.left) },
            { "top", SafeFloat(style.top) },
            { "right", SafeFloat(style.right) },
            { "bottom", SafeFloat(style.bottom) },
            { "width", SafeFloat(style.width) },
            { "height", SafeFloat(style.height) },
            { "minWidth", style.minWidth.ToString() },
            { "minHeight", style.minHeight.ToString() },
            { "maxWidth", style.maxWidth.ToString() },
            { "maxHeight", style.maxHeight.ToString() },
            { "flexGrow", SafeFloat(style.flexGrow) },
            { "flexShrink", SafeFloat(style.flexShrink) },
            { "flexBasis", style.flexBasis.ToString() },
            { "flexDirection", style.flexDirection.ToString() },
            { "alignItems", style.alignItems.ToString() },
            { "alignSelf", style.alignSelf.ToString() },
            { "justifyContent", style.justifyContent.ToString() },
            { "marginLeft", SafeFloat(style.marginLeft) },
            { "marginTop", SafeFloat(style.marginTop) },
            { "marginRight", SafeFloat(style.marginRight) },
            { "marginBottom", SafeFloat(style.marginBottom) },
            { "paddingLeft", SafeFloat(style.paddingLeft) },
            { "paddingTop", SafeFloat(style.paddingTop) },
            { "paddingRight", SafeFloat(style.paddingRight) },
            { "paddingBottom", SafeFloat(style.paddingBottom) },
            { "backgroundColor", style.backgroundColor.ToString() },
            { "unityBackgroundImageTintColor", style.unityBackgroundImageTintColor.ToString() },
            { "color", style.color.ToString() },
            { "opacity", SafeFloat(style.opacity) },
        };
    }

    internal static Dictionary<string, object> BuildBackgroundInfo(UnityEngine.UIElements.VisualElement element)
    {
        return new Dictionary<string, object>
        {
            { "inline", BuildBackgroundValueInfo(GetPropertyValue(element.style, "backgroundImage")) },
            { "resolved", BuildBackgroundValueInfo(GetPropertyValue(element.resolvedStyle, "backgroundImage")) },
        };
    }

    internal static Dictionary<string, object> BuildBackgroundValueInfo(object styleValue)
    {
        var info = new Dictionary<string, object>
        {
            { "text", styleValue != null ? styleValue.ToString() : "" },
        };

        object background = GetPropertyValue(styleValue, "value");
        if (background == null)
            background = styleValue;

        AddBackgroundObjectInfo(info, background, "texture");
        AddBackgroundObjectInfo(info, background, "sprite");
        AddBackgroundObjectInfo(info, background, "renderTexture");
        AddBackgroundObjectInfo(info, background, "vectorImage");

        return info;
    }

    internal static void AddBackgroundObjectInfo(Dictionary<string, object> info, object background, string propertyName)
    {
        object value = GetPropertyValue(background, propertyName);
        if (value is UnityEngine.Object unityObject)
        {
            info[propertyName] = new Dictionary<string, object>
            {
                { "name", unityObject.name },
                { "type", unityObject.GetType().Name },
                { "instanceId", VmObjectId.Get(unityObject) },
                { "assetPath", AssetDatabase.GetAssetPath(unityObject) },
            };
        }
    }

    internal static object GetPropertyValue(object target, string propertyName)
    {
        if (target == null || string.IsNullOrEmpty(propertyName))
            return null;

        try
        {
            var property = target.GetType().GetProperty(propertyName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            return property != null ? property.GetValue(target, null) : null;
        }
        catch
        {
            return null;
        }
    }

    internal static Dictionary<string, object> RectToDictionary(Rect rect)
    {
        return new Dictionary<string, object>
        {
            { "x", SafeFloat(rect.x) },
            { "y", SafeFloat(rect.y) },
            { "width", SafeFloat(rect.width) },
            { "height", SafeFloat(rect.height) },
            { "xMin", SafeFloat(rect.xMin) },
            { "yMin", SafeFloat(rect.yMin) },
            { "xMax", SafeFloat(rect.xMax) },
            { "yMax", SafeFloat(rect.yMax) },
        };
    }

    internal static object SafeFloat(float value)
    {
        return float.IsNaN(value) || float.IsInfinity(value) ? null : (object)value;
    }

    internal static string GetElementText(UnityEngine.UIElements.VisualElement element)
    {
        if (element is UnityEngine.UIElements.TextElement textElement)
            return textElement.text ?? "";

        return "";
    }


    }
}
