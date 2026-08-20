using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace VMUnityAutomation.Editor
{
    /// <summary>
    /// Commands for creating and managing Unity UI (UGUI) elements.
    /// </summary>
    public static class MCPUICommands
    {
        // ─── Create Canvas ───

        public static object CreateCanvas(Dictionary<string, object> args)
        {
            string name = args.ContainsKey("name") ? args["name"].ToString() : "Canvas";
            string renderMode = args.ContainsKey("renderMode") ? args["renderMode"].ToString().ToLower() : "overlay";

            var canvasGo = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(canvasGo, "Create Canvas");

            var canvas = canvasGo.AddComponent<Canvas>();
            switch (renderMode)
            {
                case "camera": canvas.renderMode = RenderMode.ScreenSpaceCamera; break;
                case "world": canvas.renderMode = RenderMode.WorldSpace; break;
                default: canvas.renderMode = RenderMode.ScreenSpaceOverlay; break;
            }

            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();

            // Ensure EventSystem exists
            if (MCPObjectSearch.Find<UnityEngine.EventSystems.EventSystem>().Length == 0)
            {
                var esGo = new GameObject("EventSystem");
                esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                Undo.RegisterCreatedObjectUndo(esGo, "Create EventSystem");
            }

            return new Dictionary<string, object>
            {
                { "success", true },
                { "name", canvasGo.name },
                { "renderMode", canvas.renderMode.ToString() },
                { "instanceId", VmObjectId.Get(canvasGo) },
            };
        }

        // ─── Create UI Element ───

        public static object CreateUIElement(Dictionary<string, object> args)
        {
            string type = args.ContainsKey("type") ? args["type"].ToString().ToLower() : "";
            if (string.IsNullOrEmpty(type))
                return new { error = "type is required (text, image, button, panel, slider, toggle, inputfield, dropdown, scrollview)" };

            string name = args.ContainsKey("name") ? args["name"].ToString() : type;
            string parent = args.ContainsKey("parent") ? args["parent"].ToString() : "";

            // Find or create canvas
            Transform parentTransform = null;
            if (!string.IsNullOrEmpty(parent))
            {
                var parentGo = GameObject.Find(parent);
                if (parentGo != null)
                    parentTransform = parentGo.transform;
            }
            if (parentTransform == null)
            {
                var canvas = MCPObjectSearch.Find<Canvas>().FirstOrDefault();
                if (canvas == null)
                    return new { error = "No Canvas found. Create a canvas first." };
                parentTransform = canvas.transform;
            }

            GameObject go = null;
            switch (type)
            {
                case "text":
                    go = CreateTextElement(name);
                    break;
                case "image":
                    go = CreateImageElement(name);
                    break;
                case "button":
                    go = CreateButtonElement(name, args);
                    break;
                case "panel":
                    go = CreatePanelElement(name);
                    break;
                case "slider":
                    go = new GameObject(name);
                    go.AddComponent<RectTransform>();
                    go.AddComponent<Slider>();
                    break;
                case "toggle":
                    go = new GameObject(name);
                    go.AddComponent<RectTransform>();
                    go.AddComponent<Toggle>();
                    break;
                case "inputfield":
                    go = CreateInputFieldElement(name);
                    break;
                default:
                    return new { error = $"Unknown UI type '{type}'. Use: text, image, button, panel, slider, toggle, inputfield" };
            }

            go.transform.SetParent(parentTransform, false);
            Undo.RegisterCreatedObjectUndo(go, "Create UI Element");

            // Apply position if provided
            var rt = go.GetComponent<RectTransform>();
            if (rt != null)
            {
                if (args.ContainsKey("anchoredPosition") && args["anchoredPosition"] is Dictionary<string, object> posDict)
                {
                    float x = posDict.ContainsKey("x") ? Convert.ToSingle(posDict["x"]) : 0;
                    float y = posDict.ContainsKey("y") ? Convert.ToSingle(posDict["y"]) : 0;
                    rt.anchoredPosition = new Vector2(x, y);
                }
                if (args.ContainsKey("sizeDelta") && args["sizeDelta"] is Dictionary<string, object> sizeDict)
                {
                    float w = sizeDict.ContainsKey("x") ? Convert.ToSingle(sizeDict["x"]) : rt.sizeDelta.x;
                    float h = sizeDict.ContainsKey("y") ? Convert.ToSingle(sizeDict["y"]) : rt.sizeDelta.y;
                    rt.sizeDelta = new Vector2(w, h);
                }
            }

            return new Dictionary<string, object>
            {
                { "success", true },
                { "name", go.name },
                { "type", type },
                { "instanceId", VmObjectId.Get(go) },
                { "parent", parentTransform.name },
            };
        }

        // ─── Get UI Info ───

        public static object GetUIInfo(Dictionary<string, object> args)
        {
            var canvases = MCPObjectSearch.Find<Canvas>(includeInactive: true);
            var canvasInfos = new List<Dictionary<string, object>>();

            foreach (var canvas in canvases)
            {
                int childCount = CountUIElements(canvas.transform);
                canvasInfos.Add(new Dictionary<string, object>
                {
                    { "name", canvas.gameObject.name },
                    { "renderMode", canvas.renderMode.ToString() },
                    { "sortingOrder", canvas.sortingOrder },
                    { "uiElementCount", childCount },
                    { "instanceId", VmObjectId.Get(canvas.gameObject) },
                });
            }

            int totalTexts = MCPObjectSearch.Find<Text>(includeInactive: true).Length;
            int totalImages = MCPObjectSearch.Find<Image>(includeInactive: true).Length;
            int totalButtons = MCPObjectSearch.Find<Button>(includeInactive: true).Length;

            return new Dictionary<string, object>
            {
                { "canvasCount", canvases.Length },
                { "canvases", canvasInfos },
                { "totalTexts", totalTexts },
                { "totalImages", totalImages },
                { "totalButtons", totalButtons },
            };
        }

        // ─── Set UI Text ───

        public static object SetUIText(Dictionary<string, object> args)
        {
            string path = args.ContainsKey("path") ? args["path"].ToString() : "";
            if (string.IsNullOrEmpty(path))
                return new { error = "path is required" };

            var go = GameObject.Find(path);
            if (go == null)
                return new { error = $"GameObject '{path}' not found" };

            var text = go.GetComponent<Text>();
            if (text == null)
                return new { error = $"No Text component on '{path}'" };

            Undo.RecordObject(text, "Set UI Text");

            if (args.ContainsKey("text"))
                text.text = args["text"].ToString();
            if (args.ContainsKey("fontSize"))
                text.fontSize = Convert.ToInt32(args["fontSize"]);
            if (args.ContainsKey("color") && args["color"] is Dictionary<string, object> colorDict)
            {
                float r = colorDict.ContainsKey("r") ? Convert.ToSingle(colorDict["r"]) : text.color.r;
                float g = colorDict.ContainsKey("g") ? Convert.ToSingle(colorDict["g"]) : text.color.g;
                float b = colorDict.ContainsKey("b") ? Convert.ToSingle(colorDict["b"]) : text.color.b;
                float a = colorDict.ContainsKey("a") ? Convert.ToSingle(colorDict["a"]) : text.color.a;
                text.color = new Color(r, g, b, a);
            }
            if (args.ContainsKey("alignment"))
            {
                string align = args["alignment"].ToString().ToLower();
                switch (align)
                {
                    case "upperleft": text.alignment = TextAnchor.UpperLeft; break;
                    case "uppercenter": text.alignment = TextAnchor.UpperCenter; break;
                    case "upperright": text.alignment = TextAnchor.UpperRight; break;
                    case "middleleft": text.alignment = TextAnchor.MiddleLeft; break;
                    case "middlecenter": text.alignment = TextAnchor.MiddleCenter; break;
                    case "middleright": text.alignment = TextAnchor.MiddleRight; break;
                    case "lowerleft": text.alignment = TextAnchor.LowerLeft; break;
                    case "lowercenter": text.alignment = TextAnchor.LowerCenter; break;
                    case "lowerright": text.alignment = TextAnchor.LowerRight; break;
                }
            }

            EditorUtility.SetDirty(text);

            return new Dictionary<string, object>
            {
                { "success", true },
                { "path", path },
                { "text", text.text },
                { "fontSize", text.fontSize },
                { "alignment", text.alignment.ToString() },
            };
        }

        // ─── Set UI Image ───

        public static object SetUIImage(Dictionary<string, object> args)
        {
            string path = args.ContainsKey("path") ? args["path"].ToString() : "";
            if (string.IsNullOrEmpty(path))
                return new { error = "path is required" };

            var go = GameObject.Find(path);
            if (go == null)
                return new { error = $"GameObject '{path}' not found" };

            var image = go.GetComponent<Image>();
            if (image == null)
                return new { error = $"No Image component on '{path}'" };

            Undo.RecordObject(image, "Set UI Image");

            if (args.ContainsKey("color") && args["color"] is Dictionary<string, object> colorDict)
            {
                float r = colorDict.ContainsKey("r") ? Convert.ToSingle(colorDict["r"]) : image.color.r;
                float g = colorDict.ContainsKey("g") ? Convert.ToSingle(colorDict["g"]) : image.color.g;
                float b = colorDict.ContainsKey("b") ? Convert.ToSingle(colorDict["b"]) : image.color.b;
                float a = colorDict.ContainsKey("a") ? Convert.ToSingle(colorDict["a"]) : image.color.a;
                image.color = new Color(r, g, b, a);
            }

            if (args.ContainsKey("sprite"))
            {
                string spritePath = args["sprite"].ToString();
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                if (sprite != null)
                    image.sprite = sprite;
            }

            if (args.ContainsKey("imageType"))
            {
                string imgType = args["imageType"].ToString().ToLower();
                switch (imgType)
                {
                    case "simple": image.type = Image.Type.Simple; break;
                    case "sliced": image.type = Image.Type.Sliced; break;
                    case "tiled": image.type = Image.Type.Tiled; break;
                    case "filled": image.type = Image.Type.Filled; break;
                }
            }

            if (args.ContainsKey("raycastTarget"))
                image.raycastTarget = Convert.ToBoolean(args["raycastTarget"]);

            EditorUtility.SetDirty(image);

            return new Dictionary<string, object>
            {
                { "success", true },
                { "path", path },
                { "hasSprite", image.sprite != null },
                { "imageType", image.type.ToString() },
            };
        }

        // ─── Editor UI Toolkit Windows ───

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

        public static object InspectUIToolkitAsset(Dictionary<string, object> args)
        {
            string uxmlPath = NormalizeAssetPath(GetString(args, "uxmlPath"), "");

            var ussPaths = GetStringList(args, "ussPaths", "ussPath")
                .Select(path => NormalizeAssetPath(path, uxmlPath))
                .Where(path => string.IsNullOrEmpty(path) == false)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            string requestedName = GetString(args, "name");
            var requestedNames = GetStringList(args, "names", "name")
                .Where(name => string.IsNullOrEmpty(name) == false)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            string className = GetString(args, "className");
            string typeName = GetString(args, "typeName");
            int maxResults = Math.Max(1, GetInt(args, "maxResults", 100));
            bool includeUss = GetBool(args, "includeUss", true);
            bool hasNamesQuery = args != null && args.ContainsKey("names");
            bool includeElements = GetBool(args, "includeElements", hasNamesQuery == false);
            bool targetedQuery = requestedNames.Count > 0 || string.IsNullOrEmpty(requestedName) == false ||
                                 string.IsNullOrEmpty(className) == false || string.IsNullOrEmpty(typeName) == false;
            bool includeAllUssClasses = GetBool(args, "includeAllUssClasses", targetedQuery == false);

            var elements = new List<UxmlElementInfo>();
            var styleReferences = new List<string>();
            string uxmlReadError = "";

            if (string.IsNullOrEmpty(uxmlPath) == false)
            {
                string absoluteUxmlPath = GetAbsoluteAssetPath(uxmlPath);
                if (!File.Exists(absoluteUxmlPath))
                    return new { error = $"UXML asset not found at '{uxmlPath}'" };

                try
                {
                    var document = XDocument.Load(absoluteUxmlPath, LoadOptions.SetLineInfo);
                    if (document.Root != null)
                        CollectUxmlElements(document.Root, "root", elements, styleReferences);
                }
                catch (Exception ex)
                {
                    uxmlReadError = ex.Message;
                }
            }

            foreach (string styleReference in styleReferences)
            {
                string resolvedPath = NormalizeAssetPath(styleReference, uxmlPath);
                if (string.IsNullOrEmpty(resolvedPath) == false &&
                    ussPaths.Contains(resolvedPath, StringComparer.OrdinalIgnoreCase) == false)
                {
                    ussPaths.Add(resolvedPath);
                }
            }

            var ussClassStyles = includeUss ? ReadUssClassStyles(ussPaths) : new Dictionary<string, UssClassStyle>();

            var requestedNameSet = new HashSet<string>(requestedNames, StringComparer.OrdinalIgnoreCase);
            var matchingElements = elements
                .Where(element => ElementMatches(element, requestedName, className, typeName))
                .Where(element => requestedNameSet.Count == 0 || requestedNameSet.Contains(element.Name))
                .ToList();
            var reportedElements = includeElements
                ? matchingElements.Take(maxResults).ToList()
                : new List<UxmlElementInfo>();
            var filteredElements = reportedElements
                .Select(element => BuildUxmlElementDictionary(element, ussClassStyles))
                .ToList();

            var nameChecks = new List<Dictionary<string, object>>();
            var reportedNameMatches = new List<UxmlElementInfo>();
            int remainingNameMatches = maxResults;
            bool nameMatchesTruncated = false;
            foreach (string name in requestedNames)
            {
                var matches = elements.Where(element => string.Equals(element.Name, name, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                bool typeMatches = string.IsNullOrEmpty(typeName) ||
                    matches.Any(element => TypeMatches(element.TypeName, typeName));
                var returnedMatches = matches.Take(remainingNameMatches).ToList();
                remainingNameMatches -= returnedMatches.Count;
                nameMatchesTruncated |= returnedMatches.Count < matches.Count;
                reportedNameMatches.AddRange(returnedMatches);
                nameChecks.Add(new Dictionary<string, object>
                {
                    { "name", name },
                    { "exists", matches.Count > 0 },
                    { "matchCount", matches.Count },
                    { "typeMatches", typeMatches },
                    { "reportedMatchCount", returnedMatches.Count },
                    { "matchesTruncated", returnedMatches.Count < matches.Count },
                    { "matches", returnedMatches.Select(element => BuildUxmlElementDictionary(element, ussClassStyles)).ToList() },
                });
            }

            var relevantClassNames = new HashSet<string>(reportedElements.Concat(reportedNameMatches)
                .SelectMany(element => element.Classes)
                .Where(name => string.IsNullOrEmpty(name) == false), StringComparer.OrdinalIgnoreCase);
            var returnedUssClasses = includeUss == false
                ? new Dictionary<string, object>()
                : ussClassStyles
                    .Where(pair => includeAllUssClasses || relevantClassNames.Contains(pair.Key))
                    .ToDictionary(pair => pair.Key, pair => (object)pair.Value.ToDictionary(),
                        StringComparer.OrdinalIgnoreCase);
            bool outputTruncated = includeElements && matchingElements.Count > reportedElements.Count ||
                                   nameMatchesTruncated;

            return new Dictionary<string, object>
            {
                { "success", string.IsNullOrEmpty(uxmlReadError) },
                { "uxmlPath", uxmlPath },
                { "uxmlReadError", uxmlReadError },
                { "ussPaths", ussPaths },
                { "elementCount", elements.Count },
                { "query", new Dictionary<string, object>
                    {
                        { "name", requestedName },
                        { "names", requestedNames },
                        { "className", className },
                        { "typeName", typeName },
                    }
                },
                { "valid", string.IsNullOrEmpty(uxmlReadError) &&
                    (requestedNames.Count == 0 || nameChecks.All(check =>
                        Convert.ToBoolean(check["exists"]) && Convert.ToBoolean(check["typeMatches"]))) },
                { "nameChecks", nameChecks },
                { "matchedCount", filteredElements.Count },
                { "totalMatchedCount", matchingElements.Count },
                { "outputTruncated", outputTruncated },
                { "includeElements", includeElements },
                { "includeAllUssClasses", includeAllUssClasses },
                { "elements", filteredElements },
                { "ussClasses", returnedUssClasses },
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
                outputPath = $"Temp/MCP_UIToolkitElement_{DateTime.Now:yyyyMMdd_HHmmss}.png";

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
                ? MCPGameViewCaptureCommands.CaptureGameViewRenderTexture(fullWindowPath)
                : MCPScreenshotCommands.CaptureEditorWindow(captureArgs) as Dictionary<string, object>;
            if (runtime && (capture == null || GetBool(capture, "success", false) == false))
            {
                string renderTextureError = capture == null
                    ? "Game View render-texture capture returned no result."
                    : GetString(capture, "error");
                capture = MCPScreenshotCommands.CaptureEditorWindow(captureArgs) as Dictionary<string, object>;
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
            var crop = MCPScreenshotCommands.CropImage(cropArgs);
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
                actualPath = $"Temp/MCP_UIToolkitCompare_{DateTime.Now:yyyyMMdd_HHmmss}.png";

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

            var comparison = MCPGraphicsCommands.CompareImages(compareArgs);
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
                    var screenshot = MCPScreenshotCommands.CaptureEditorWindow(new Dictionary<string, object>
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

        public static object AssertUIToolkitLayout(Dictionary<string, object> args)
        {
            var document = FindRuntimeUIDocument(args, out string error);
            if (document == null)
                return new { error };

            var root = document.rootVisualElement;
            if (root == null)
                return new { error = $"UIDocument '{document.name}' has no rootVisualElement" };

            var assertionArgs = GetObjectList(args, "assertions");
            if (assertionArgs.Count == 0)
                return new { error = "assertions array is required" };

            var results = new List<Dictionary<string, object>>();
            bool valid = true;

            for (int i = 0; i < assertionArgs.Count; i++)
            {
                var assertion = AsDictionary(assertionArgs[i]);
                var result = BuildLayoutAssertion(root, assertion, i);
                results.Add(result);
                if (!GetBool(result, "passed", false))
                    valid = false;
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

        // ─── Helpers ───

        private static GameObject CreateTextElement(string name)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(160, 30);
            var text = go.AddComponent<Text>();
            text.text = "New Text";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.color = Color.black;
            text.alignment = TextAnchor.MiddleCenter;
            return go;
        }

        private static GameObject CreateImageElement(string name)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(100, 100);
            go.AddComponent<Image>();
            return go;
        }

        private static GameObject CreateButtonElement(string name, Dictionary<string, object> args)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(160, 30);
            go.AddComponent<Image>();
            go.AddComponent<Button>();

            // Add label
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var textRt = textGo.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.sizeDelta = Vector2.zero;
            var text = textGo.AddComponent<Text>();
            text.text = args.ContainsKey("label") ? args["label"].ToString() : "Button";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.color = Color.black;
            text.alignment = TextAnchor.MiddleCenter;

            return go;
        }

        private static GameObject CreatePanelElement(string name)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            var image = go.AddComponent<Image>();
            image.color = new Color(1, 1, 1, 0.39f);
            return go;
        }

        private static GameObject CreateInputFieldElement(string name)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(160, 30);
            var image = go.AddComponent<Image>();
            image.color = Color.white;

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var textRt = textGo.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.sizeDelta = new Vector2(-10, -6);
            var text = textGo.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.color = Color.black;
            text.supportRichText = false;

            var placeholderGo = new GameObject("Placeholder");
            placeholderGo.transform.SetParent(go.transform, false);
            var phRt = placeholderGo.AddComponent<RectTransform>();
            phRt.anchorMin = Vector2.zero;
            phRt.anchorMax = Vector2.one;
            phRt.sizeDelta = new Vector2(-10, -6);
            var phText = placeholderGo.AddComponent<Text>();
            phText.text = "Enter text...";
            phText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            phText.fontStyle = FontStyle.Italic;
            phText.color = new Color(0, 0, 0, 0.5f);

            var inputField = go.AddComponent<InputField>();
            inputField.textComponent = text;
            inputField.placeholder = phText;

            return go;
        }

        private static int CountUIElements(Transform parent)
        {
            int count = 0;
            foreach (Transform child in parent)
            {
                count++;
                count += CountUIElements(child);
            }
            return count;
        }

        private static Dictionary<string, object> BuildWindowInfo(EditorWindow window)
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

        private static ResolvedUIToolkitElement ResolveUIToolkitElement(Dictionary<string, object> args, bool runtime)
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

        private static UnityEngine.UIElements.VisualElement FindEditorElement(
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

        private static List<Dictionary<string, object>> CollectElementResources(
            UnityEngine.UIElements.VisualElement root, UnityEngine.UIElements.VisualElement element,
            string elementPath, int maxDepth)
        {
            var results = new List<Dictionary<string, object>>();
            CollectElementResources(root, element, elementPath, 0, Math.Max(0, maxDepth), results);
            return results;
        }

        private static void CollectElementResources(
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

        private static List<string> BuildResourceWarnings(
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

        private static void CollectGeneratedChildren(
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

        private static List<string> GetGeneratedChildReasons(UnityEngine.UIElements.VisualElement element)
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

        private static List<string> GetGeneratedChildWarnings(UnityEngine.UIElements.VisualElement element,
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

        private static bool IsKnownGeneratedUIToolkitType(string typeName)
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

        private static bool IsKnownGeneratedIndicatorClass(string className)
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

        private static Dictionary<string, object> BuildUIDocumentInfo(UnityEngine.UIElements.UIDocument document)
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

        private static List<UnityEngine.UIElements.UIDocument> GetRuntimeUIDocuments(bool includeInactive)
        {
            return Resources.FindObjectsOfTypeAll<UnityEngine.UIElements.UIDocument>()
                .Where(document => document != null &&
                                   document.gameObject != null &&
                                   document.gameObject.scene.IsValid() &&
                                   (includeInactive || (document.enabled && document.gameObject.activeInHierarchy)))
                .OrderBy(document => GetGameObjectPath(document.transform), StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static UnityEngine.UIElements.UIDocument FindRuntimeUIDocument(
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

        private static UnityEngine.UIElements.VisualElement FindRuntimeElement(
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

        private static bool HasElementLocator(Dictionary<string, object> args)
        {
            return string.IsNullOrEmpty(GetString(args, "path")) == false ||
                   string.IsNullOrEmpty(GetString(args, "visualElementPath")) == false ||
                   GetStringList(args, "visualElementNames", "").Count > 0;
        }

        private static int MarkAllUIToolkitDirty()
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

        private static Dictionary<string, object> BuildUIToolkitRefreshResult(
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

        private static string GetGameObjectPath(Transform transform)
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

        private static string NormalizeGameObjectPath(string path)
        {
            return (path ?? "").Trim().Trim('/').Replace('\\', '/');
        }

        private static EditorWindow FindEditorWindow(Dictionary<string, object> args, out string error)
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

        private static EditorWindow FindUIBuilderWindow()
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

        private static RectInt ReadCaptureContentRect(Dictionary<string, object> capture, int width, int height)
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

        private static RectInt MapWorldRectToCapture(Rect worldRect, Rect rootWorldRect, RectInt contentRect,
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

        private static RectInt ClampRectToImage(RectInt rect, int width, int height)
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

        private static bool MatchesWindow(EditorWindow window, string windowQuery, string typeQuery, string titleQuery, bool exact)
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

        private static bool Matches(string value, string query, bool exact)
        {
            if (string.IsNullOrEmpty(query))
                return true;

            return exact
                ? string.Equals(value, query, StringComparison.OrdinalIgnoreCase)
                : value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Dictionary<string, object> BuildElementTree(
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

        private static Dictionary<string, object> BuildElementInfo(
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

        private static void QueryElements(
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

        private static bool MatchesElement(UnityEngine.UIElements.VisualElement element, string name,
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

        private static UnityEngine.UIElements.VisualElement GetElementByFlexiblePath(
            UnityEngine.UIElements.VisualElement root, string path)
        {
            var element = GetElementByPath(root, path);
            if (element != null)
                return element;

            var names = SplitVisualElementPath(path);
            return names.Count > 0 ? GetElementByVisualElementPath(root, names) : null;
        }

        private static UnityEngine.UIElements.VisualElement GetElementByVisualElementPath(
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

        private static UnityEngine.UIElements.VisualElement FindNamedElement(
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

        private static List<string> GetVisualElementPathNames(Dictionary<string, object> args, string prefix)
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

        private static void AddVisualElementPathNames(List<string> names, object value)
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

        private static List<string> SplitVisualElementPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return new List<string>();

            return path.Split(new[] { '/', '\\', '>', '.' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(part => part.Trim())
                .Where(part => string.IsNullOrEmpty(part) == false &&
                               string.Equals(part, "root", StringComparison.OrdinalIgnoreCase) == false)
                .ToList();
        }

        private static string GetElementPath(UnityEngine.UIElements.VisualElement root,
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

        private static bool TryBuildElementPath(UnityEngine.UIElements.VisualElement current,
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

        private static Dictionary<string, object> BuildLayoutAssertion(
            UnityEngine.UIElements.VisualElement root, Dictionary<string, object> assertion, int index)
        {
            string type = GetString(assertion, "type");
            if (string.IsNullOrEmpty(type))
                type = GetString(assertion, "kind");
            if (string.IsNullOrEmpty(type))
                type = "edge-touch";

            try
            {
                switch (type.ToLowerInvariant())
                {
                    case "edge-touch":
                    case "touch":
                    case "no-gap-no-overlap":
                        return BuildEdgeTouchAssertion(root, assertion, index, type);
                    case "same-edge":
                    case "align-edge":
                    case "edge-align":
                        return BuildEdgeAlignAssertion(root, assertion, index, type);
                    case "same-center":
                    case "align-center":
                    case "center-align":
                        return BuildCenterAlignAssertion(root, assertion, index, type);
                    case "inside":
                    case "contained":
                        return BuildInsideAssertion(root, assertion, index, type);
                    case "size":
                        return BuildSizeAssertion(root, assertion, index, type);
                    default:
                        return new Dictionary<string, object>
                        {
                            { "index", index },
                            { "type", type },
                            { "passed", false },
                            { "error", $"Unknown assertion type '{type}'" },
                        };
                }
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object>
                {
                    { "index", index },
                    { "type", type },
                    { "passed", false },
                    { "error", ex.Message },
                };
            }
        }

        private static Dictionary<string, object> BuildEdgeTouchAssertion(
            UnityEngine.UIElements.VisualElement root, Dictionary<string, object> assertion, int index, string type)
        {
            var first = FindAssertionElement(root, assertion, "first", out string firstPath, out string firstError);
            var second = FindAssertionElement(root, assertion, "second", out string secondPath, out string secondError);
            float tolerance = GetFloat(assertion, "tolerance", 0.5f);

            if (first == null || second == null)
            {
                return new Dictionary<string, object>
                {
                    { "index", index },
                    { "type", type },
                    { "passed", false },
                    { "error", first == null ? firstError : secondError },
                };
            }

            string axis = GetString(assertion, "axis").ToLowerInvariant();
            if (axis != "y")
                axis = "x";

            string firstEdge = GetString(assertion, "firstEdge");
            string secondEdge = GetString(assertion, "secondEdge");
            if (string.IsNullOrEmpty(firstEdge))
                firstEdge = axis == "x" ? "right" : "bottom";
            if (string.IsNullOrEmpty(secondEdge))
                secondEdge = axis == "x" ? "left" : "top";

            float firstValue = GetRectEdge(first.worldBound, firstEdge);
            float secondValue = GetRectEdge(second.worldBound, secondEdge);
            float delta = secondValue - firstValue;
            bool passed = Math.Abs(delta) <= tolerance;

            return new Dictionary<string, object>
            {
                { "index", index },
                { "type", type },
                { "passed", passed },
                { "axis", axis },
                { "firstPath", firstPath },
                { "secondPath", secondPath },
                { "firstEdge", firstEdge },
                { "secondEdge", secondEdge },
                { "firstValue", SafeFloat(firstValue) },
                { "secondValue", SafeFloat(secondValue) },
                { "delta", SafeFloat(delta) },
                { "gap", SafeFloat(delta > tolerance ? delta : 0) },
                { "overlap", SafeFloat(delta < -tolerance ? -delta : 0) },
                { "tolerance", SafeFloat(tolerance) },
                { "firstRect", RectToDictionary(first.worldBound) },
                { "secondRect", RectToDictionary(second.worldBound) },
            };
        }

        private static Dictionary<string, object> BuildEdgeAlignAssertion(
            UnityEngine.UIElements.VisualElement root, Dictionary<string, object> assertion, int index, string type)
        {
            var first = FindAssertionElement(root, assertion, "first", out string firstPath, out string firstError);
            var second = FindAssertionElement(root, assertion, "second", out string secondPath, out string secondError);
            float tolerance = GetFloat(assertion, "tolerance", 0.5f);

            if (first == null || second == null)
            {
                return new Dictionary<string, object>
                {
                    { "index", index },
                    { "type", type },
                    { "passed", false },
                    { "error", first == null ? firstError : secondError },
                };
            }

            string edge = GetString(assertion, "edge");
            if (string.IsNullOrEmpty(edge))
                edge = GetString(assertion, "firstEdge");
            if (string.IsNullOrEmpty(edge))
                edge = "bottom";
            string secondEdge = GetString(assertion, "secondEdge");
            if (string.IsNullOrEmpty(secondEdge))
                secondEdge = edge;

            float firstValue = GetRectEdge(first.worldBound, edge);
            float secondValue = GetRectEdge(second.worldBound, secondEdge);
            float delta = secondValue - firstValue;

            return new Dictionary<string, object>
            {
                { "index", index },
                { "type", type },
                { "passed", Math.Abs(delta) <= tolerance },
                { "firstPath", firstPath },
                { "secondPath", secondPath },
                { "firstEdge", edge },
                { "secondEdge", secondEdge },
                { "firstValue", SafeFloat(firstValue) },
                { "secondValue", SafeFloat(secondValue) },
                { "delta", SafeFloat(delta) },
                { "tolerance", SafeFloat(tolerance) },
                { "firstRect", RectToDictionary(first.worldBound) },
                { "secondRect", RectToDictionary(second.worldBound) },
            };
        }

        private static Dictionary<string, object> BuildCenterAlignAssertion(
            UnityEngine.UIElements.VisualElement root, Dictionary<string, object> assertion, int index, string type)
        {
            var first = FindAssertionElement(root, assertion, "first", out string firstPath, out string firstError);
            var second = FindAssertionElement(root, assertion, "second", out string secondPath, out string secondError);
            float tolerance = GetFloat(assertion, "tolerance", 0.5f);

            if (first == null || second == null)
            {
                return new Dictionary<string, object>
                {
                    { "index", index },
                    { "type", type },
                    { "passed", false },
                    { "error", first == null ? firstError : secondError },
                };
            }

            string axis = GetString(assertion, "axis").ToLowerInvariant();
            if (axis != "y")
                axis = "x";

            float firstValue = axis == "x" ? first.worldBound.center.x : first.worldBound.center.y;
            float secondValue = axis == "x" ? second.worldBound.center.x : second.worldBound.center.y;
            float delta = secondValue - firstValue;

            return new Dictionary<string, object>
            {
                { "index", index },
                { "type", type },
                { "passed", Math.Abs(delta) <= tolerance },
                { "axis", axis },
                { "firstPath", firstPath },
                { "secondPath", secondPath },
                { "firstValue", SafeFloat(firstValue) },
                { "secondValue", SafeFloat(secondValue) },
                { "delta", SafeFloat(delta) },
                { "tolerance", SafeFloat(tolerance) },
                { "firstRect", RectToDictionary(first.worldBound) },
                { "secondRect", RectToDictionary(second.worldBound) },
            };
        }

        private static Dictionary<string, object> BuildInsideAssertion(
            UnityEngine.UIElements.VisualElement root, Dictionary<string, object> assertion, int index, string type)
        {
            var inner = FindAssertionElement(root, assertion, "inner", out string innerPath, out string innerError);
            var outer = FindAssertionElement(root, assertion, "outer", out string outerPath, out string outerError);
            float tolerance = GetFloat(assertion, "tolerance", 0.5f);

            if (inner == null || outer == null)
            {
                return new Dictionary<string, object>
                {
                    { "index", index },
                    { "type", type },
                    { "passed", false },
                    { "error", inner == null ? innerError : outerError },
                };
            }

            Rect innerRect = inner.worldBound;
            Rect outerRect = outer.worldBound;
            bool passed = innerRect.xMin >= outerRect.xMin - tolerance &&
                          innerRect.yMin >= outerRect.yMin - tolerance &&
                          innerRect.xMax <= outerRect.xMax + tolerance &&
                          innerRect.yMax <= outerRect.yMax + tolerance;

            return new Dictionary<string, object>
            {
                { "index", index },
                { "type", type },
                { "passed", passed },
                { "innerPath", innerPath },
                { "outerPath", outerPath },
                { "tolerance", SafeFloat(tolerance) },
                { "leftOverflow", SafeFloat(Math.Max(0, outerRect.xMin - innerRect.xMin)) },
                { "topOverflow", SafeFloat(Math.Max(0, outerRect.yMin - innerRect.yMin)) },
                { "rightOverflow", SafeFloat(Math.Max(0, innerRect.xMax - outerRect.xMax)) },
                { "bottomOverflow", SafeFloat(Math.Max(0, innerRect.yMax - outerRect.yMax)) },
                { "innerRect", RectToDictionary(innerRect) },
                { "outerRect", RectToDictionary(outerRect) },
            };
        }

        private static Dictionary<string, object> BuildSizeAssertion(
            UnityEngine.UIElements.VisualElement root, Dictionary<string, object> assertion, int index, string type)
        {
            var element = FindAssertionElement(root, assertion, "", out string path, out string error);
            float expectedWidth = GetFloat(assertion, "width", float.NaN);
            if (float.IsNaN(expectedWidth))
                expectedWidth = GetFloat(assertion, "expectedWidth", float.NaN);
            float expectedHeight = GetFloat(assertion, "height", float.NaN);
            if (float.IsNaN(expectedHeight))
                expectedHeight = GetFloat(assertion, "expectedHeight", float.NaN);
            float tolerance = GetFloat(assertion, "tolerance", 0.5f);

            if (element == null)
            {
                return new Dictionary<string, object>
                {
                    { "index", index },
                    { "type", type },
                    { "passed", false },
                    { "error", error },
                };
            }

            Rect rect = element.worldBound;
            float widthDelta = float.IsNaN(expectedWidth) ? 0 : rect.width - expectedWidth;
            float heightDelta = float.IsNaN(expectedHeight) ? 0 : rect.height - expectedHeight;
            bool widthPassed = float.IsNaN(expectedWidth) || Math.Abs(widthDelta) <= tolerance;
            bool heightPassed = float.IsNaN(expectedHeight) || Math.Abs(heightDelta) <= tolerance;

            return new Dictionary<string, object>
            {
                { "index", index },
                { "type", type },
                { "passed", widthPassed && heightPassed },
                { "path", path },
                { "expectedWidth", float.IsNaN(expectedWidth) ? null : (object)expectedWidth },
                { "expectedHeight", float.IsNaN(expectedHeight) ? null : (object)expectedHeight },
                { "actualWidth", SafeFloat(rect.width) },
                { "actualHeight", SafeFloat(rect.height) },
                { "widthDelta", SafeFloat(widthDelta) },
                { "heightDelta", SafeFloat(heightDelta) },
                { "tolerance", SafeFloat(tolerance) },
                { "rect", RectToDictionary(rect) },
            };
        }

        private static UnityEngine.UIElements.VisualElement FindAssertionElement(
            UnityEngine.UIElements.VisualElement root, Dictionary<string, object> assertion,
            string prefix, out string path, out string error)
        {
            path = "";
            error = "";

            string pathKey = string.IsNullOrEmpty(prefix) ? "path" : $"{prefix}Path";
            string requestedPath = GetString(assertion, pathKey);
            if (string.IsNullOrEmpty(requestedPath) && string.IsNullOrEmpty(prefix))
                requestedPath = GetString(assertion, "elementPath");
            if (string.IsNullOrEmpty(requestedPath) == false)
            {
                var element = GetElementByFlexiblePath(root, requestedPath);
                if (element != null)
                {
                    path = GetElementPath(root, element);
                    return element;
                }

                error = $"Element path '{requestedPath}' was not found";
                return null;
            }

            var names = GetVisualElementPathNames(assertion, prefix);
            if (names.Count > 0)
            {
                var element = GetElementByVisualElementPath(root, names);
                if (element != null)
                {
                    path = GetElementPath(root, element);
                    return element;
                }

                error = $"VisualElementPath '{string.Join("/", names)}' was not found";
                return null;
            }

            string nameKey = string.IsNullOrEmpty(prefix) ? "name" : $"{prefix}Name";
            string name = GetString(assertion, nameKey);
            if (string.IsNullOrEmpty(name) == false)
            {
                var element = FindNamedElement(root, name, true);
                if (element != null)
                {
                    path = GetElementPath(root, element);
                    return element;
                }

                error = $"Element name '{name}' was not found";
                return null;
            }

            error = $"No element locator was supplied for prefix '{prefix}'";
            return null;
        }

        private static float GetRectEdge(Rect rect, string edge)
        {
            switch ((edge ?? "").ToLowerInvariant())
            {
                case "left":
                case "xmin":
                    return rect.xMin;
                case "right":
                case "xmax":
                    return rect.xMax;
                case "top":
                case "ymin":
                    return rect.yMin;
                case "bottom":
                case "ymax":
                    return rect.yMax;
                case "centerx":
                    return rect.center.x;
                case "centery":
                    return rect.center.y;
                default:
                    throw new ArgumentException($"Unknown rect edge '{edge}'");
            }
        }

        private static void CollectUxmlElements(XElement element, string path, List<UxmlElementInfo> elements,
            List<string> styleReferences)
        {
            string typeName = element.Name.LocalName;
            if (string.Equals(typeName, "Style", StringComparison.OrdinalIgnoreCase))
            {
                string styleSource = GetAttributeValue(element, "src");
                if (string.IsNullOrEmpty(styleSource) == false)
                    styleReferences.Add(styleSource);
            }

            var info = new UxmlElementInfo
            {
                Path = path,
                TypeName = typeName,
                FullTypeName = element.Name.ToString(),
                Name = GetAttributeValue(element, "name"),
                Classes = SplitClasses(GetAttributeValue(element, "class")),
                InlineStyle = GetAttributeValue(element, "style"),
                LineNumber = element is IXmlLineInfo lineInfo && lineInfo.HasLineInfo() ? lineInfo.LineNumber : 0,
            };
            elements.Add(info);

            int childIndex = 0;
            foreach (var child in element.Elements())
            {
                CollectUxmlElements(child, $"{path}/{childIndex}", elements, styleReferences);
                childIndex++;
            }
        }

        private static bool ElementMatches(UxmlElementInfo element, string name, string className, string typeName)
        {
            if (string.IsNullOrEmpty(name) == false &&
                !string.Equals(element.Name, name, StringComparison.OrdinalIgnoreCase))
                return false;

            if (string.IsNullOrEmpty(className) == false &&
                !element.Classes.Any(item => string.Equals(item, className, StringComparison.OrdinalIgnoreCase)))
                return false;

            if (string.IsNullOrEmpty(typeName) == false && !TypeMatches(element.TypeName, typeName) &&
                !TypeMatches(element.FullTypeName, typeName))
                return false;

            return true;
        }

        private static bool TypeMatches(string actualType, string expectedType)
        {
            return string.IsNullOrEmpty(expectedType) ||
                (!string.IsNullOrEmpty(actualType) &&
                 actualType.IndexOf(expectedType, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static Dictionary<string, object> BuildUxmlElementDictionary(UxmlElementInfo element,
            Dictionary<string, UssClassStyle> ussClassStyles)
        {
            var resolvedDeclarations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var matchedClasses = new List<string>();

            foreach (string className in element.Classes)
            {
                if (!ussClassStyles.TryGetValue(className, out var style))
                    continue;

                matchedClasses.Add(className);
                foreach (var pair in style.Declarations)
                    resolvedDeclarations[pair.Key] = pair.Value;
            }

            return new Dictionary<string, object>
            {
                { "path", element.Path },
                { "type", element.TypeName },
                { "fullType", element.FullTypeName },
                { "name", element.Name },
                { "classes", element.Classes },
                { "inlineStyle", element.InlineStyle },
                { "line", element.LineNumber },
                { "ussMatchedClasses", matchedClasses },
                { "ussDefaultSize", BuildDefaultSizeDictionary(resolvedDeclarations) },
                { "ussResolvedDeclarations", resolvedDeclarations },
            };
        }

        private static Dictionary<string, object> BuildDefaultSizeDictionary(Dictionary<string, string> declarations)
        {
            string[] keys =
            {
                "width", "height", "min-width", "min-height", "max-width", "max-height",
                "left", "top", "right", "bottom"
            };

            var result = new Dictionary<string, object>();
            foreach (string key in keys)
            {
                if (declarations.TryGetValue(key, out string value))
                    result[key] = value;
            }

            return result;
        }

        private static Dictionary<string, UssClassStyle> ReadUssClassStyles(List<string> ussPaths)
        {
            var styles = new Dictionary<string, UssClassStyle>(StringComparer.OrdinalIgnoreCase);
            foreach (string ussPath in ussPaths)
            {
                string absolutePath = GetAbsoluteAssetPath(ussPath);
                if (!File.Exists(absolutePath))
                    continue;

                string text = Regex.Replace(File.ReadAllText(absolutePath), @"/\*.*?\*/", "", RegexOptions.Singleline);
                foreach (Match ruleMatch in Regex.Matches(text, @"(?<selector>[^{}]+)\{(?<body>[^{}]*)\}",
                             RegexOptions.Singleline))
                {
                    string selectorList = ruleMatch.Groups["selector"].Value;
                    string body = ruleMatch.Groups["body"].Value;
                    var declarations = ParseUssDeclarations(body);
                    if (declarations.Count == 0)
                        continue;

                    foreach (string selector in SplitUssSelectorList(selectorList))
                    {
                        var targetClassNames = GetTargetClassNames(selector);
                        if (targetClassNames.Count == 0)
                            continue;

                        var pseudoStates = GetSelectorPseudoStates(selector);
                        string standaloneClassName = "";
                        bool isUnconditionalClassSelector = pseudoStates.Count == 0 &&
                                                            TryGetStandaloneClassSelector(selector,
                                                                out standaloneClassName);
                        foreach (string className in targetClassNames)
                        {
                            if (!styles.TryGetValue(className, out var style))
                            {
                                style = new UssClassStyle { ClassName = className };
                                styles[className] = style;
                            }

                            if (!style.SourcePaths.Contains(ussPath, StringComparer.OrdinalIgnoreCase))
                                style.SourcePaths.Add(ussPath);

                            if (isUnconditionalClassSelector &&
                                string.Equals(className, standaloneClassName, StringComparison.OrdinalIgnoreCase))
                            {
                                if (!style.DefaultSelectors.Contains(selector, StringComparer.Ordinal))
                                    style.DefaultSelectors.Add(selector);

                                foreach (var pair in declarations)
                                    style.Declarations[pair.Key] = pair.Value;
                                continue;
                            }

                            var rule = new UssSelectorRule
                            {
                                Selector = selector,
                                SourcePath = ussPath,
                            };
                            rule.PseudoStates.AddRange(pseudoStates);
                            foreach (var pair in declarations)
                                rule.Declarations[pair.Key] = pair.Value;

                            if (pseudoStates.Count > 0)
                                style.StateRules.Add(rule);
                            else
                                style.ContextRules.Add(rule);
                        }
                    }
                }
            }

            return styles;
        }

        private static List<string> SplitUssSelectorList(string selectorList)
        {
            var selectors = new List<string>();
            if (string.IsNullOrWhiteSpace(selectorList))
                return selectors;

            int start = 0;
            int parenthesisDepth = 0;
            int bracketDepth = 0;
            char quote = '\0';
            for (int index = 0; index < selectorList.Length; index++)
            {
                char character = selectorList[index];
                if (quote != '\0')
                {
                    if (character == quote && (index == 0 || selectorList[index - 1] != '\\'))
                        quote = '\0';
                    continue;
                }

                if (character == '"' || character == '\'')
                {
                    quote = character;
                    continue;
                }

                switch (character)
                {
                    case '(':
                        parenthesisDepth++;
                        break;
                    case ')':
                        parenthesisDepth = Math.Max(0, parenthesisDepth - 1);
                        break;
                    case '[':
                        bracketDepth++;
                        break;
                    case ']':
                        bracketDepth = Math.Max(0, bracketDepth - 1);
                        break;
                    case ',' when parenthesisDepth == 0 && bracketDepth == 0:
                        AddUssSelector(selectorList, start, index - start, selectors);
                        start = index + 1;
                        break;
                }
            }

            AddUssSelector(selectorList, start, selectorList.Length - start, selectors);
            return selectors;
        }

        private static void AddUssSelector(string selectorList, int start, int length, List<string> selectors)
        {
            string selector = selectorList.Substring(start, length).Trim();
            if (selector.Length > 0)
                selectors.Add(selector);
        }

        private static List<string> GetTargetClassNames(string selector)
        {
            string targetCompound = GetRightmostSelectorCompound(selector);
            return Regex.Matches(targetCompound, @"\.([A-Za-z_][A-Za-z0-9_-]*)")
                .Cast<Match>()
                .Select(match => match.Groups[1].Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string GetRightmostSelectorCompound(string selector)
        {
            int parenthesisDepth = 0;
            int bracketDepth = 0;
            char quote = '\0';
            int compoundStart = 0;
            for (int index = 0; index < selector.Length; index++)
            {
                char character = selector[index];
                if (quote != '\0')
                {
                    if (character == quote && (index == 0 || selector[index - 1] != '\\'))
                        quote = '\0';
                    continue;
                }

                if (character == '"' || character == '\'')
                {
                    quote = character;
                    continue;
                }

                switch (character)
                {
                    case '(':
                        parenthesisDepth++;
                        continue;
                    case ')':
                        parenthesisDepth = Math.Max(0, parenthesisDepth - 1);
                        continue;
                    case '[':
                        bracketDepth++;
                        continue;
                    case ']':
                        bracketDepth = Math.Max(0, bracketDepth - 1);
                        continue;
                }

                if (parenthesisDepth == 0 && bracketDepth == 0 &&
                    (char.IsWhiteSpace(character) || character == '>' || character == '+' || character == '~'))
                {
                    while (index + 1 < selector.Length &&
                           (char.IsWhiteSpace(selector[index + 1]) || selector[index + 1] == '>' ||
                            selector[index + 1] == '+' || selector[index + 1] == '~'))
                    {
                        index++;
                    }

                    compoundStart = index + 1;
                }
            }

            return selector.Substring(Math.Min(compoundStart, selector.Length)).Trim();
        }

        private static List<string> GetSelectorPseudoStates(string selector)
        {
            return Regex.Matches(selector, @":{1,2}([A-Za-z_][A-Za-z0-9_-]*)")
                .Cast<Match>()
                .Select(match => match.Groups[1].Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static bool TryGetStandaloneClassSelector(string selector, out string className)
        {
            var match = Regex.Match(selector.Trim(), @"^\.([A-Za-z_][A-Za-z0-9_-]*)$");
            className = match.Success ? match.Groups[1].Value : "";
            return match.Success;
        }

        private static Dictionary<string, string> ParseUssDeclarations(string body)
        {
            var declarations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string rawDeclaration in body.Split(';'))
            {
                int separatorIndex = rawDeclaration.IndexOf(':');
                if (separatorIndex <= 0)
                    continue;

                string key = rawDeclaration.Substring(0, separatorIndex).Trim();
                string value = rawDeclaration.Substring(separatorIndex + 1).Trim();
                if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value))
                    continue;

                declarations[key] = value;
            }

            return declarations;
        }

        private static string GetAttributeValue(XElement element, string attributeName)
        {
            foreach (var attribute in element.Attributes())
            {
                if (string.Equals(attribute.Name.LocalName, attributeName, StringComparison.OrdinalIgnoreCase))
                    return attribute.Value;
            }

            return "";
        }

        private static List<string> SplitClasses(string classValue)
        {
            if (string.IsNullOrWhiteSpace(classValue))
                return new List<string>();

            return classValue.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .ToList();
        }

        private static string NormalizeAssetPath(string rawPath, string relativeToAssetPath)
        {
            if (string.IsNullOrWhiteSpace(rawPath))
                return "";

            string path = rawPath.Trim().Replace('\\', '/');
            int queryIndex = path.IndexOf('?');
            if (queryIndex >= 0)
                path = path.Substring(0, queryIndex);

            int fragmentIndex = path.IndexOf('#');
            if (fragmentIndex >= 0)
                path = path.Substring(0, fragmentIndex);

            const string projectPrefix = "project://database/";
            if (path.StartsWith(projectPrefix, StringComparison.OrdinalIgnoreCase))
                path = path.Substring(projectPrefix.Length);

            path = Uri.UnescapeDataString(path);

            if (path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase) ||
                Path.IsPathRooted(path))
            {
                return path;
            }

            if (string.IsNullOrEmpty(relativeToAssetPath) == false)
            {
                string directory = Path.GetDirectoryName(relativeToAssetPath)?.Replace('\\', '/');
                if (string.IsNullOrEmpty(directory) == false)
                    return $"{directory}/{path}";
            }

            return path;
        }

        private static string GetAbsoluteAssetPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return "";

            if (Path.IsPathRooted(assetPath))
                return Path.GetFullPath(assetPath);

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
        }

        private static UnityEngine.UIElements.VisualElement FindElement(
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

        private static UnityEngine.UIElements.VisualElement GetElementByPath(
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

        private static Dictionary<string, object> BuildPixelInfo(
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

        private static Dictionary<string, object> BuildBackgroundScaleInfo(
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

        private static void AddPixelGridResult(Dictionary<string, object> result,
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

        private static void AddBackgroundScaleResult(Dictionary<string, object> result,
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

        private static void AddRuntimeSizeResult(Dictionary<string, object> result,
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

        private static bool IsOnPixelGrid(float value, float pixelScale, float tolerance)
        {
            if (pixelScale <= 0)
                return true;

            return Math.Abs(GetPixelGridDelta(value, pixelScale)) <= tolerance;
        }

        private static float GetPixelGridDelta(float value, float pixelScale)
        {
            if (pixelScale <= 0)
                return 0;

            return value - Mathf.Round(value / pixelScale) * pixelScale;
        }

        private static UnityEngine.Object GetBackgroundObject(UnityEngine.UIElements.VisualElement element)
        {
            object styleValue = GetPropertyValue(element.resolvedStyle, "backgroundImage");
            object background = GetPropertyValue(styleValue, "value") ?? styleValue;

            return GetPropertyValue(background, "sprite") as UnityEngine.Object
                   ?? GetPropertyValue(background, "texture") as UnityEngine.Object
                   ?? GetPropertyValue(background, "renderTexture") as UnityEngine.Object
                   ?? GetPropertyValue(background, "vectorImage") as UnityEngine.Object;
        }

        private static Vector2 GetBackgroundObjectSize(UnityEngine.Object backgroundObject)
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

        private static Dictionary<string, object> BuildUnityObjectInfo(UnityEngine.Object unityObject)
        {
            return new Dictionary<string, object>
            {
                { "name", unityObject != null ? unityObject.name : "" },
                { "type", unityObject != null ? unityObject.GetType().Name : "" },
                { "instanceId", unityObject != null ? VmObjectId.Get(unityObject) : "0" },
                { "assetPath", unityObject != null ? AssetDatabase.GetAssetPath(unityObject) : "" },
            };
        }

        private static Dictionary<string, object> BuildInlineStyleInfo(UnityEngine.UIElements.VisualElement element)
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

        private static Dictionary<string, object> BuildResolvedStyleInfo(UnityEngine.UIElements.VisualElement element)
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

        private static Dictionary<string, object> BuildBackgroundInfo(UnityEngine.UIElements.VisualElement element)
        {
            return new Dictionary<string, object>
            {
                { "inline", BuildBackgroundValueInfo(GetPropertyValue(element.style, "backgroundImage")) },
                { "resolved", BuildBackgroundValueInfo(GetPropertyValue(element.resolvedStyle, "backgroundImage")) },
            };
        }

        private static Dictionary<string, object> BuildBackgroundValueInfo(object styleValue)
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

        private static void AddBackgroundObjectInfo(Dictionary<string, object> info, object background, string propertyName)
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

        private static object GetPropertyValue(object target, string propertyName)
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

        private static Dictionary<string, object> RectToDictionary(Rect rect)
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

        private static object SafeFloat(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? null : (object)value;
        }

        private static string GetElementText(UnityEngine.UIElements.VisualElement element)
        {
            if (element is UnityEngine.UIElements.TextElement textElement)
                return textElement.text ?? "";

            return "";
        }

        private static string GetString(Dictionary<string, object> args, string key)
        {
            return args != null && args.ContainsKey(key) && args[key] != null ? args[key].ToString() : "";
        }

        private static bool GetBool(Dictionary<string, object> args, string key, bool defaultValue)
        {
            if (args == null || !args.ContainsKey(key) || args[key] == null)
                return defaultValue;

            if (args[key] is bool value)
                return value;

            return bool.TryParse(args[key].ToString(), out bool parsed) ? parsed : defaultValue;
        }

        private static int GetInt(Dictionary<string, object> args, string key, int defaultValue)
        {
            if (args == null || !args.ContainsKey(key) || args[key] == null)
                return defaultValue;

            return int.TryParse(args[key].ToString(), out int parsed) ? parsed : defaultValue;
        }

        private static bool TryGetObjectId(Dictionary<string, object> args, string key, out object id)
        {
            id = null;
            if (args == null || !args.ContainsKey(key) || args[key] == null)
                return false;

            string text = args[key].ToString();
            if (!IsObjectIdString(text))
                return false;

            id = args[key];
            return true;
        }

        private static bool IsObjectIdString(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            int digitStart = value[0] == '-' ? 1 : 0;
            if (digitStart == value.Length)
                return false;

            bool hasNonZeroDigit = false;
            for (int index = digitStart; index < value.Length; index++)
            {
                char character = value[index];
                if (character < '0' || character > '9')
                    return false;

                hasNonZeroDigit |= character != '0';
            }

            return hasNonZeroDigit;
        }

        private static float GetFloat(Dictionary<string, object> args, string key, float defaultValue)
        {
            if (args == null || !args.ContainsKey(key) || args[key] == null)
                return defaultValue;

            return float.TryParse(args[key].ToString(), out float parsed) ? parsed : defaultValue;
        }

        private static List<object> GetObjectList(Dictionary<string, object> args, string key)
        {
            if (args == null || args.TryGetValue(key, out object value) == false || value == null)
                return new List<object>();

            if (value is List<object> list)
                return list;

            return new List<object> { value };
        }

        private static Dictionary<string, object> AsDictionary(object value)
        {
            return value as Dictionary<string, object> ?? new Dictionary<string, object>();
        }

        private static List<string> GetStringList(Dictionary<string, object> args, string arrayKey, string singleKey)
        {
            var results = new List<string>();
            if (args == null)
                return results;

            if (args.TryGetValue(arrayKey, out object arrayValue) &&
                arrayValue is System.Collections.IEnumerable enumerable && arrayValue is string == false)
            {
                foreach (object item in enumerable)
                {
                    if (item != null)
                        results.Add(item.ToString());
                }

                if (string.Equals(arrayKey, singleKey, StringComparison.Ordinal))
                    return results;
            }

            string singleValue = GetString(args, singleKey);
            if (string.IsNullOrEmpty(singleValue) == false &&
                results.Contains(singleValue, StringComparer.OrdinalIgnoreCase) == false)
            {
                results.Add(singleValue);
            }

            return results;
        }

        private sealed class UxmlElementInfo
        {
            public string Path;
            public string TypeName;
            public string FullTypeName;
            public string Name;
            public List<string> Classes = new List<string>();
            public string InlineStyle;
            public int LineNumber;
        }

        private sealed class UssClassStyle
        {
            public string ClassName;
            public readonly List<string> SourcePaths = new List<string>();
            public readonly List<string> DefaultSelectors = new List<string>();
            public readonly Dictionary<string, string> Declarations =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            public readonly List<UssSelectorRule> ContextRules = new List<UssSelectorRule>();
            public readonly List<UssSelectorRule> StateRules = new List<UssSelectorRule>();

            public Dictionary<string, object> ToDictionary()
            {
                return new Dictionary<string, object>
                {
                    { "className", ClassName },
                    { "sourcePaths", SourcePaths },
                    { "defaultSelectors", DefaultSelectors },
                    { "declarations", Declarations },
                    { "defaultSize", BuildDefaultSizeDictionary(Declarations) },
                    { "contextRules", ContextRules.Select(rule => rule.ToDictionary()).ToList() },
                    { "stateRules", StateRules.Select(rule => rule.ToDictionary()).ToList() },
                };
            }
        }

        private sealed class UssSelectorRule
        {
            public string Selector;
            public string SourcePath;
            public readonly List<string> PseudoStates = new List<string>();
            public readonly Dictionary<string, string> Declarations =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            public Dictionary<string, object> ToDictionary()
            {
                return new Dictionary<string, object>
                {
                    { "selector", Selector },
                    { "sourcePath", SourcePath },
                    { "pseudoStates", PseudoStates },
                    { "declarations", Declarations },
                    { "defaultSize", BuildDefaultSizeDictionary(Declarations) },
                };
            }
        }
    }
}
