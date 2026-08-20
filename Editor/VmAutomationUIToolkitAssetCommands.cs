using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using UnityEngine;
using static VMUnityAutomation.Editor.VmAutomationUICommandArguments;
using static VMUnityAutomation.Editor.VmAutomationUIToolkitElementUtility;

namespace VMUnityAutomation.Editor
{
    public static class VmAutomationUIToolkitAssetCommands
    {
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
