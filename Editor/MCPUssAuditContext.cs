#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using UnityEngine.UIElements;
using static VMUnityAutomation.Editor.MCPUssCascadeAuditor;
using static VMUnityAutomation.Editor.MCPUssStyleSheetParser;

namespace VMUnityAutomation.Editor
{
    internal static class MCPUssAuditContext
    {
        internal sealed class UssRule
        {
            public string AssetPath;
            public int Line;
            public List<string> Selectors = new List<string>();
            public Dictionary<string, string> Declarations =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            public string SuppressionReason;
            public string RedundantDeclarationSuppressionReason;
            public string AncestorDefaultResetSuppressionReason;
            public string PixelGridSuppressionReason;
            public string TextStyleContractSuppressionReason;
        }

        internal sealed class UssSimpleSelector
        {
            public string Text;
            public string TypeName;
            public string Id;
            public int Specificity;
            public readonly List<string> ClassNames = new List<string>();

            public bool Matches(UssAuthoredElement element)
            {
                if (string.IsNullOrWhiteSpace(TypeName) == false &&
                    string.Equals(TypeName, element.TypeName,
                        StringComparison.Ordinal) == false)
                {
                    return false;
                }

                if (string.IsNullOrWhiteSpace(Id) == false &&
                    string.Equals(Id, element.Name, StringComparison.Ordinal) == false)
                {
                    return false;
                }

                return ClassNames.All(element.Classes.Contains);
            }
        }

        internal sealed class UssAuthoredElement
        {
            public string TypeName;
            public string ComponentTypeName;
            public string Name;
            public string Text;
            public int Line;
            public int Column;
            public UssAuthoredElement Parent;
            public readonly HashSet<string> Classes =
                new HashSet<string>(StringComparer.Ordinal);
            public readonly HashSet<string> ImplicitClasses =
                new HashSet<string>(StringComparer.Ordinal);
            public readonly HashSet<string> AuthoredClasses =
                new HashSet<string>(StringComparer.Ordinal);
            public readonly Dictionary<string, string> InlineDeclarations =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            public readonly List<UssAuthoredElement> Children =
                new List<UssAuthoredElement>();
        }

        internal sealed class UssAuthoredElementUsage
        {
            public readonly UssCascadeDocument Document;
            public readonly UssAuthoredElement Element;

            public UssAuthoredElementUsage(UssCascadeDocument document,
                UssAuthoredElement element)
            {
                Document = document;
                Element = element;
            }
        }

        internal sealed class UssAuthoredDocument
        {
            public readonly string AssetPath;
            public readonly List<string> StylePaths = new List<string>();
            public readonly List<UssAuthoredElement> Elements =
                new List<UssAuthoredElement>();

            public UssAuthoredDocument(string assetPath, XDocument document)
            {
                AssetPath = assetPath;
                foreach (var styleElement in document.Descendants()
                             .Where(element => string.Equals(element.Name.LocalName, "Style",
                                 StringComparison.OrdinalIgnoreCase)))
                {
                    var stylePath = ResolveStyleReference(
                        GetAttributeValue(styleElement, "src"), assetPath);
                    if (string.IsNullOrWhiteSpace(stylePath) == false)
                    {
                        StylePaths.Add(stylePath);
                    }
                }

                var authoredByElement = new Dictionary<XElement, UssAuthoredElement>();
                foreach (var element in document.Descendants().Where(element =>
                             IsAuthoredVisualElement(element)))
                {
                    var authored = new UssAuthoredElement
                    {
                        TypeName = element.Name.LocalName,
                        Name = GetAttributeValue(element, "name"),
                        Text = GetAttributeValue(element, "text"),
                        Line = GetLineNumber(element),
                        Column = GetColumnNumber(element)
                    };
                    var elementIdentity = MCPUIToolkitElementStyleBaseline.Resolve(
                        element.Name.NamespaceName, element.Name.LocalName);
                    authored.ComponentTypeName = elementIdentity.ComponentTypeName;
                    foreach (var implicitClass in elementIdentity.ImplicitClasses)
                    {
                        authored.Classes.Add(implicitClass);
                        authored.ImplicitClasses.Add(implicitClass);
                    }

                    foreach (var className in SplitWhitespace(
                                 GetAttributeValue(element, "class")))
                    {
                        authored.Classes.Add(className);
                        authored.AuthoredClasses.Add(className);
                    }

                    foreach (var declaration in ParseDeclarations(
                                 GetAttributeValue(element, "style")))
                    {
                        authored.InlineDeclarations[declaration.Key] = declaration.Value;
                    }

                    Elements.Add(authored);
                    authoredByElement[element] = authored;
                }

                foreach (var pair in authoredByElement)
                {
                    var parentElement = pair.Key.Parent;
                    while (parentElement != null)
                    {
                        if (authoredByElement.TryGetValue(parentElement,
                                out var authoredParent))
                        {
                            pair.Value.Parent = authoredParent;
                            authoredParent.Children.Add(pair.Value);
                            break;
                        }

                        parentElement = parentElement.Parent;
                    }
                }
            }

            private static bool IsAuthoredVisualElement(XElement element)
            {
                switch (element.Name.LocalName)
                {
                    case "UXML":
                    case "Style":
                    case "Template":
                    case "AttributeOverrides":
                        return false;
                    default:
                        return true;
                }
            }
        }

        internal sealed class UssCascadeRule
        {
            public UssRule Rule;
            public string SelectorText;
            public UssSimpleSelector Selector;
            public int Origin;
            public int SourceOrder;
        }

        internal sealed class UssResolvedDeclaration
        {
            public UssRule Rule;
            public string SelectorText;
            public string Value;
            public int Origin;
            public int Specificity;
            public int SourceOrder;
        }

        internal sealed class UssCascadeDocument
        {
            private int sourceOrder;

            public readonly UssAuthoredDocument AuthoredDocument;
            public readonly List<UssCascadeRule> Rules = new List<UssCascadeRule>();
            public readonly HashSet<string> LoadedAssetPaths =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            public UssCascadeDocument(UssAuthoredDocument authoredDocument)
            {
                AuthoredDocument = authoredDocument;
            }

            public int NextSourceOrder()
            {
                return sourceOrder++;
            }

            public UssResolvedDeclaration Resolve(UssAuthoredElement element,
                string property, UssRule excludedRule)
            {
                UssResolvedDeclaration winner = null;
                foreach (var contextualRule in Rules)
                {
                    if (contextualRule.Selector == null ||
                        ReferenceEquals(contextualRule.Rule, excludedRule) ||
                        contextualRule.Selector.Matches(element) == false ||
                        contextualRule.Rule.Declarations.TryGetValue(property,
                            out var value) == false)
                    {
                        continue;
                    }

                    if (winner != null &&
                        (winner.Origin > contextualRule.Origin ||
                         winner.Origin == contextualRule.Origin &&
                         winner.Specificity > contextualRule.Selector.Specificity ||
                         winner.Origin == contextualRule.Origin &&
                         winner.Specificity == contextualRule.Selector.Specificity &&
                         winner.SourceOrder > contextualRule.SourceOrder))
                    {
                        continue;
                    }

                    winner = new UssResolvedDeclaration
                    {
                        Rule = contextualRule.Rule,
                        SelectorText = contextualRule.SelectorText,
                        Value = value,
                        Origin = contextualRule.Origin,
                        Specificity = contextualRule.Selector.Specificity,
                        SourceOrder = contextualRule.SourceOrder
                    };
                }

                return winner;
            }

            public bool HasUnsupportedCompetingDeclaration(string property,
                string targetValue)
            {
                return Rules.Any(contextualRule =>
                    contextualRule.Selector == null &&
                    IsDynamicStateSelector(contextualRule.SelectorText) == false &&
                    contextualRule.Rule.Declarations.TryGetValue(property, out var value) &&
                    StyleValuesEqual(value, targetValue) == false);
            }
        }

        internal sealed class UssCascadeIndex
        {
            public readonly List<UssCascadeDocument> Documents =
                new List<UssCascadeDocument>();
        }

        internal sealed class UssUsageIndex
        {
            private readonly Dictionary<string, List<UssUsageLocation>> classUsages =
                new Dictionary<string, List<UssUsageLocation>>(StringComparer.Ordinal);

            private readonly Dictionary<string, List<UssUsageLocation>> idUsages =
                new Dictionary<string, List<UssUsageLocation>>(StringComparer.Ordinal);

            private readonly Dictionary<string, List<UssUsageLocation>> runtimeClassReferences =
                new Dictionary<string, List<UssUsageLocation>>(StringComparer.Ordinal);

            private readonly Dictionary<string, List<UssUsageLocation>> runtimeClassAssignments =
                new Dictionary<string, List<UssUsageLocation>>(StringComparer.Ordinal);

            private readonly Dictionary<string, List<UssUsageLocation>> runtimeClassSemanticReferences =
                new Dictionary<string, List<UssUsageLocation>>(StringComparer.Ordinal);

            private readonly Dictionary<string, HashSet<string>> classUsageNames =
                new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

            public readonly HashSet<string> AllClassTokens =
                new HashSet<string>(StringComparer.Ordinal);
            public readonly HashSet<string> AllIdTokens =
                new HashSet<string>(StringComparer.Ordinal);
            public readonly HashSet<string> ComplexClassTokens =
                new HashSet<string>(StringComparer.Ordinal);
            public readonly HashSet<string> ComplexIdTokens =
                new HashSet<string>(StringComparer.Ordinal);
            public readonly HashSet<string> PseudoIdTokens =
                new HashSet<string>(StringComparer.Ordinal);
            public readonly List<UssAuthoredDocument> Documents =
                new List<UssAuthoredDocument>();
            public int IndexedUxmlCount;
            public int IndexedRuntimeSourceCount;

            public void AddDocument(string assetPath, XDocument document)
            {
                Documents.Add(new UssAuthoredDocument(assetPath, document));
            }

            public void AddClassUsage(string token, string path, int line, string elementName = "")
            {
                AddClassUsage(token, path, line, 0, elementName);
            }

            public void AddClassUsage(string token, string path, int line, int column,
                string elementName = "")
            {
                AddLocation(classUsages, token, path, line, column);
                if (string.IsNullOrWhiteSpace(elementName))
                {
                    return;
                }

                if (classUsageNames.TryGetValue(token, out var names) == false)
                {
                    names = new HashSet<string>(StringComparer.Ordinal);
                    classUsageNames[token] = names;
                }

                names.Add(elementName);
            }

            public void AddIdUsage(string token, string path, int line, int column = 0)
            {
                AddLocation(idUsages, token, path, line, column);
            }

            public void AddRuntimeClassReference(string token, string path, int line,
                int column = 0)
            {
                AddLocation(runtimeClassReferences, token, path, line, column);
            }

            public void AddRuntimeClassAssignment(string token, string path, int line,
                int column = 0)
            {
                AddLocation(runtimeClassAssignments, token, path, line, column);
            }

            public void AddRuntimeClassSemanticReference(string token, string path, int line,
                int column = 0)
            {
                AddLocation(runtimeClassSemanticReferences, token, path, line, column);
            }

            public IReadOnlyList<UssUsageLocation> GetClassUsages(string token)
            {
                return GetLocations(classUsages, token);
            }

            public IReadOnlyList<UssUsageLocation> GetIdUsages(string token)
            {
                return GetLocations(idUsages, token);
            }

            public IReadOnlyList<UssUsageLocation> GetRuntimeClassReferences(string token)
            {
                return GetLocations(runtimeClassReferences, token);
            }

            public IReadOnlyList<UssUsageLocation> GetRuntimeClassAssignments(string token)
            {
                return GetLocations(runtimeClassAssignments, token);
            }

            public IReadOnlyList<UssUsageLocation> GetRuntimeClassSemanticReferences(string token)
            {
                return GetLocations(runtimeClassSemanticReferences, token);
            }

            public string GetSingleClassUsageName(string token)
            {
                if (GetClassUsages(token).Count != 1 ||
                    classUsageNames.TryGetValue(token, out var names) == false ||
                    names.Count != 1)
                {
                    return "";
                }

                return names.First();
            }

            private static void AddLocation(IDictionary<string, List<UssUsageLocation>> locations, string token,
                string path, int line, int column)
            {
                if (locations.TryGetValue(token, out var values) == false)
                {
                    values = new List<UssUsageLocation>();
                    locations[token] = values;
                }

                if (values.Any(value => value.Path == path && value.Line == line &&
                                        value.Column == column) == false)
                {
                    values.Add(new UssUsageLocation(path, line, column));
                }
            }

            private static IReadOnlyList<UssUsageLocation> GetLocations(
                IReadOnlyDictionary<string, List<UssUsageLocation>> locations, string token)
            {
                return locations.TryGetValue(token, out var values)
                    ? values
                    : Array.Empty<UssUsageLocation>();
            }
        }
    }
}
#endif
