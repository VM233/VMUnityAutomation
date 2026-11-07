#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine.UIElements;
using static VMUnityAutomation.Editor.VmAutomationUxmlLayoutAuditor;
using static VMUnityAutomation.Editor.VmAutomationUxmlLayoutModels;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationUxmlInteractionStructureAuditor
    {
        internal const string Marker = "ui-structure:";
        private const int MaximumElements = 16384, MaximumDepth = 128;
        private const int MaximumContracts = 1024, MaximumTypes = 128;

        private sealed class Node
        {
            internal XElement Element;
            internal Node Parent, Scope, Context, Group, Field;
            internal string Role = "";
            internal Dictionary<string, string> Contract = new Dictionary<string, string>();
            internal readonly List<Node> Buttons = new List<Node>();
            internal readonly List<Node> Sliders = new List<Node>();
            internal readonly List<Node> Captions = new List<Node>();
            internal readonly List<Node> Values = new List<Node>();
            internal readonly Dictionary<string, List<Node>> Names = new Dictionary<string, List<Node>>();
            internal bool Button, Slider;
            internal int Depth, UngroupedButtons;
            internal string Get(string key) => Contract.TryGetValue(key, out var value) ? value : "";
        }

        internal static void Audit(string path, XDocument document,
            UxmlInlineStyleContractIndex styles, IEnumerable<string> requiredScopes,
            VmAutomationUxmlLayoutAuditReport report)
        {
            var elements = document.Descendants().Take(MaximumElements + 1).ToArray();
            if (elements.Length > MaximumElements)
            {
                Issue(path, document.Root, report, "interaction-structure-budget", "XML element budget exceeded.");
                return;
            }
            var contracts = ReadContracts(path, document, report);
            var requirements = requiredScopes.Where(value => value.StartsWith(path + "#", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (contracts.Count == 0 && requirements.Length == 0) return;
            if (contracts.Count > MaximumContracts || styles.RuleCount > 4096)
            {
                Issue(path, document.Root, report, "interaction-structure-budget", "Contract or stylesheet-rule budget exceeded.");
                return;
            }
            var indexed = new Dictionary<XElement, Node>();
            var globalNames = new Dictionary<string, List<Node>>(StringComparer.Ordinal);
            var types = new HashSet<string>(StringComparer.Ordinal);
            var groups = new List<Node>();
            var fields = new List<Node>();
            var panels = new List<Node>();
            var nodes = new List<Node>();
            foreach (var element in elements)
            {
                Node parent = element.Parent == null ? null : indexed[element.Parent];
                var node = new Node { Element = element, Parent = parent,
                    Scope = parent?.Scope, Context = parent?.Context, Group = parent?.Group,
                    Field = parent?.Field, Depth = (parent?.Depth ?? 0) + 1 };
                if (node.Depth > MaximumDepth)
                {
                    Issue(path, element, report, "interaction-structure-budget", "XML ancestry budget exceeded.");
                    return;
                }
                indexed.Add(element, node); nodes.Add(node);
                string globalName = AttributeValue(element, "name");
                if (!string.IsNullOrEmpty(globalName))
                {
                    if (!globalNames.TryGetValue(globalName, out var named)) globalNames.Add(globalName, named = new List<Node>());
                    named.Add(node);
                }
                if (contracts.TryGetValue(element, out var contract))
                {
                    node.Contract = contract; node.Role = node.Get("role");
                }
                if (node.Role == "scope")
                {
                    node.Scope = node; node.Group = node.Field = null;
                    node.Context = node.Get("state") == "" ? null : node;
                    report.InteractionScopes++;
                }
                if (node.Scope == null)
                {
                    if (node.Role != "") Issue(path, element, report, "interaction-outside-scope", "Declare a task scope around this contract.");
                    continue;
                }
                if ((AttributeValue(element, "style") ?? "").Length > 4096)
                {
                    Issue(path, element, report, "interaction-structure-budget", "A scoped inline style exceeds 4096 characters.");
                    return;
                }
                if (node.Role == "context") node.Context = node;
                if (IsGroup(node.Role)) { node.Group = node; groups.Add(node); report.InteractionGroups++; }
                if (node.Role == "field") { node.Field = node; fields.Add(node); report.InteractionFields++; }
                if (node.Role == "panel" || node.Role == "publication") panels.Add(node);
                string name = AttributeValue(element, "name");
                if (!string.IsNullOrEmpty(name))
                {
                    if (!node.Scope.Names.TryGetValue(name, out var named)) node.Scope.Names.Add(name, named = new List<Node>());
                    named.Add(node);
                }
                string xmlType = element.Name.ToString();
                types.Add(xmlType);
                if (types.Count > MaximumTypes)
                {
                    Issue(path, element, report, "interaction-structure-budget", "Scoped XML type budget exceeded.");
                    return;
                }
                Type type = VmAutomationUIToolkitElementStyleBaseline.ResolveType(element.Name.NamespaceName, element.Name.LocalName);
                node.Button = type != null && typeof(Button).IsAssignableFrom(type);
                node.Slider = type != null && (typeof(Slider).IsAssignableFrom(type) || typeof(SliderInt).IsAssignableFrom(type));
                if (node.Button) { report.InteractionButtons++; node.Group?.Buttons.Add(node); }
                if (node.Slider)
                {
                    report.InteractionSliders++;
                    if (node.Field != null) node.Field.Sliders.Add(node);
                    else if (string.IsNullOrWhiteSpace(AttributeValue(element, "label")) &&
                        !HasPropertyBinding(element, "label"))
                    {
                        report.UnclassifiedInteractionFields++;
                        Issue(path, element, report, "unclassified-input-field", "Group this slider with its caption/reading or use its native label.");
                    }
                }
                if (node.Role == "caption") node.Field?.Captions.Add(node);
                if (node.Role == "value") node.Field?.Values.Add(node);
                if ((node.Role == "caption" || node.Role == "value" || node.Role == "help") &&
                    (type == null || !typeof(TextElement).IsAssignableFrom(type)))
                    Issue(path, element, report, "invalid-field-part-type", "A field caption, reading or help role must identify an actual TextElement.");
                if (type != null && typeof(TextElement).IsAssignableFrom(type) && node.Field != null &&
                    node.Role != "caption" && node.Role != "value" && node.Role != "help")
                    Issue(path, element, report, "unclassified-field-text", "Declare this field text as caption, value or help.");
                if ((node.Role == "caption" || node.Role == "value" || node.Role == "help") && node.Field == null)
                    Issue(path, element, report, "field-part-outside-field", "This text part has no field owner.");
                if (node.Role == "tab" && (!node.Button || node.Group?.Role != "navigation"))
                    Issue(path, element, report, "mixed-interaction-group", "A tab must be an actual Button owned by a navigation group.");
            }
            foreach (string required in requirements)
            {
                string name = required.Substring(path.Length + 1);
                if (!globalNames.TryGetValue(name, out var named) || named.Count != 1 || named[0].Role != "scope")
                    Issue(path, document.Root, report, "required-interaction-scope-missing", "Expected one declared scope: " + required);
            }
            AuditGroups(path, groups, panels, styles, report);
            foreach (var field in fields) AuditField(path, field, styles, report);
            for (int index = nodes.Count - 1; index >= 0; index--)
            {
                var node = nodes[index];
                if (node.Scope == null) continue;
                if (node.Button) node.UngroupedButtons = 1;
                if (IsGroup(node.Role)) node.UngroupedButtons = 0;
                else if (node.UngroupedButtons > 1)
                {
                    report.UnclassifiedInteractionGroups++;
                    Issue(path, node.Element, report, "unclassified-interaction-group", "Multiple controls share this container without a declared responsibility.");
                    node.UngroupedButtons = 0;
                }
                if (node.Role == "scope" || node.Role == "context") node.UngroupedButtons = 0;
                if (node.Parent != null && node.Parent.Scope == node.Scope)
                    node.Parent.UngroupedButtons += node.UngroupedButtons;
            }
        }

        private static void AuditGroups(string path, List<Node> groups, List<Node> panels,
            UxmlInlineStyleContractIndex styles, VmAutomationUxmlLayoutAuditReport report)
        {
            var stateOwners = new Dictionary<(Node, Node, string), Node>();
            var targetOwners = new Dictionary<Node, Node>();
            foreach (var group in groups)
            {
                if (group.Context == null || group.Get("context") != group.Context.Get("state"))
                    Issue(path, group.Element, report, "interaction-context-mismatch", "This group is separated from its declared work context.");
                if (group.Role != "navigation") continue;
                report.InteractionNavigations++;
                var key = (group.Scope, group.Context, group.Get("state"));
                if (stateOwners.ContainsKey(key))
                    Issue(path, group.Element, report, "duplicate-navigation-owner", "This work context already has navigation for state '" + key.Item3 + "'.");
                else stateOwners.Add(key, group);
                var content = Unique(group, group.Get("content"));
                if (content == null)
                {
                    Issue(path, group.Element, report, "navigation-content-missing", "The content name must resolve uniquely inside the task scope.");
                    continue;
                }
                if (group.Parent != content.Parent || group.Element.ElementsAfterSelf().FirstOrDefault() != content.Element)
                    Issue(path, group.Element, report, "navigation-content-detached", "Place navigation immediately before its content under one parent.");
                if (group.Buttons.Count < 2)
                    Issue(path, group.Element, report, "uninformative-navigation", "A tab group needs at least two distinct content destinations.");
                if (group.Get("contentMode") == "publication")
                {
                    AuditPublicationNavigation(path, group, content, targetOwners, styles, report);
                    continue;
                }
                int selected = 0, shown = 0;
                foreach (var button in group.Buttons)
                {
                    if (button.Role != "tab" || button.Get("value") != "")
                    {
                        Issue(path, button.Element, report, "mixed-interaction-group", "Every navigation Button must identify its content destination.");
                        continue;
                    }
                    var target = Unique(group, button.Get("target"));
                    if (target == null || target.Parent != content || target.Role != "panel")
                    {
                        Issue(path, button.Element, report, "navigation-target-missing", "A tab must target a unique declared panel directly inside its content.");
                        continue;
                    }
                    if (targetOwners.ContainsKey(target))
                        Issue(path, button.Element, report, "duplicate-navigation-target", "This panel already has an entry in this task.");
                    else targetOwners.Add(target, button);
                    bool active = GetElementClasses(button.Element).Contains(group.Get("selectedClass"));
                    bool visible = Property(target.Element, "display", styles) != "none";
                    if (active) selected++;
                    if (visible) shown++;
                    if (active != visible)
                        Issue(path, button.Element, report, "navigation-selection-mismatch", "Initial tab selection and associated panel visibility disagree.");
                }
                if (selected != 1 || shown != 1)
                    Issue(path, group.Element, report, "navigation-selection-cardinality", "Author exactly one selected tab and one visible destination.");
                foreach (var child in content.Element.Elements())
                    if (indexedVisual(child) && (!group.Scope.Names.TryGetValue(AttributeValue(child, "name") ?? "", out var named) ||
                        named.Count != 1 || !targetOwners.ContainsKey(named[0])))
                        Issue(path, child, report, "unreachable-navigation-panel", "This content child has no unique tab entry.");
            }
            foreach (var panel in panels)
                if (!targetOwners.ContainsKey(panel))
                    Issue(path, panel.Element, report, "unreachable-navigation-panel", "This declared panel has no navigation entry.");
        }

        private static void AuditPublicationNavigation(string path, Node group, Node content,
            Dictionary<Node, Node> targetOwners, UxmlInlineStyleContractIndex styles,
            VmAutomationUxmlLayoutAuditReport report)
        {
            report.InteractionPublicationNavigations++;
            if (content.Role != "publication")
                Issue(path, content.Element, report, "navigation-publication-missing", "Shared content must declare its publisher and initial section value.");
            if (content.Context != group.Context || content.Get("context") != group.Get("context") ||
                content.Get("state") != group.Get("state"))
                Issue(path, content.Element, report, "navigation-publication-owner-mismatch", "Navigation and publication must share the same work context and section state.");
            if (targetOwners.ContainsKey(content))
                Issue(path, group.Element, report, "duplicate-navigation-target", "This publication body already has a navigation owner.");
            else targetOwners.Add(content, group);
            if (Property(content.Element, "display", styles) == "none")
                Issue(path, content.Element, report, "navigation-publication-hidden", "The initially selected publication body must be visible.");
            var values = new HashSet<string>(StringComparer.Ordinal);
            int selected = 0;
            foreach (var button in group.Buttons)
            {
                if (button.Role != "tab" || button.Get("value") == "")
                {
                    Issue(path, button.Element, report, "mixed-interaction-group", "A publication tab must declare its distinct section value.");
                    continue;
                }
                if (Unique(group, button.Get("target")) != content)
                    Issue(path, button.Element, report, "navigation-target-missing", "A publication tab must target its navigation's actual shared body.");
                if (!values.Add(button.Get("value")))
                    Issue(path, button.Element, report, "duplicate-navigation-value", "Each tab must publish a distinct section value.");
                report.InteractionPublicationDestinations++;
                if (!GetElementClasses(button.Element).Contains(group.Get("selectedClass"))) continue;
                selected++;
                if (button.Get("value") != content.Get("value"))
                    Issue(path, button.Element, report, "navigation-selection-mismatch", "Initial tab selection and published section value disagree.");
            }
            if (selected != 1)
                Issue(path, group.Element, report, "navigation-selection-cardinality", "Author exactly one selected publication tab.");
        }

        private static bool indexedVisual(XElement element) =>
            VmAutomationUIToolkitElementStyleBaseline.ResolveType(element.Name.NamespaceName, element.Name.LocalName) != null;

        private static void AuditField(string path, Node field, UxmlInlineStyleContractIndex styles,
            VmAutomationUxmlLayoutAuditReport report)
        {
            if (field.Captions.Count != 1 || field.Values.Count > 1 || field.Sliders.Count != 1)
            {
                Issue(path, field.Element, report, "invalid-field-composition", "A compact field requires one caption, one slider and at most one numeric reading.");
                return;
            }
            Node caption = field.Captions[0], input = field.Sliders[0];
            XElement header = caption.Element.Parent;
            if (header?.Parent != field.Element || input.Parent != field || header.ElementsAfterSelf().FirstOrDefault() != input.Element)
                Issue(path, field.Element, report, "field-control-separated", "Keep the caption header immediately above its input in the same field.");
            if (field.Values.Count == 1 && (field.Values[0].Element.Parent != header ||
                Property(header, "flex-direction", styles) != "row"))
                Issue(path, field.Element, report, "field-reading-separated", "Keep the numeric reading beside its caption in one row header above the input.");
        }

        private static Node Unique(Node from, string name) =>
            from.Scope.Names.TryGetValue(name, out var matches) && matches.Count == 1 ? matches[0] : null;

        private static bool IsGroup(string role) => role == "navigation" || role == "choice" ||
            role == "inspection" || role == "pose" || role == "actions";

        private static string Property(XElement element, string property, UxmlInlineStyleContractIndex styles)
        {
            var declarations = ParseStyle(AttributeValue(element, "style"));
            return declarations.TryGetValue(property, out var value) ? value : styles.ResolveProperty(element, property);
        }

        private static Dictionary<XElement, Dictionary<string, string>> ReadContracts(string path,
            XDocument document, VmAutomationUxmlLayoutAuditReport report)
        {
            var result = new Dictionary<XElement, Dictionary<string, string>>();
            int count = 0;
            foreach (var comment in document.DescendantNodes().OfType<XComment>())
            {
                string text = comment.Value.Trim();
                if (!text.StartsWith(Marker, StringComparison.Ordinal)) continue;
                if (++count > MaximumContracts)
                {
                    Issue(path, comment, report, "interaction-structure-budget", "Contract comment budget exceeded.");
                    break;
                }
                try
                {
                    if (text.Length > 4096) throw new InvalidDataException("A contract exceeds 4096 characters.");
                    XNode next = comment.NextNode;
                    while (next is XComment || next is XText whitespace && string.IsNullOrWhiteSpace(whitespace.Value)) next = next.NextNode;
                    if (!(next is XElement element)) throw new InvalidDataException("A structure comment must precede its actual element.");
                    JObject json;
                    using (var reader = new JsonTextReader(new StringReader(text.Substring(Marker.Length))) { MaxDepth = 4, DateParseHandling = DateParseHandling.None })
                    {
                        json = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                        if (reader.Read()) throw new InvalidDataException("Trailing contract JSON is not allowed.");
                    }
                    var values = new Dictionary<string, string>(StringComparer.Ordinal);
                    foreach (var property in json.Properties())
                    {
                        if (property.Value.Type != JTokenType.String || string.IsNullOrWhiteSpace((string)property.Value))
                            throw new InvalidDataException("Contract values must be nonempty strings.");
                        values.Add(property.Name, (string)property.Value);
                    }
                    if (!values.TryGetValue("role", out string role)) throw new InvalidDataException("A contract needs a role.");
                    string[] keys;
                    if (role == "scope") keys = values.ContainsKey("state") ? new[] { "role", "state" } : new[] { "role" };
                    else if (role == "panel" || role == "field" || role == "caption" || role == "value" || role == "help") keys = new[] { "role" };
                    else if (role == "context") keys = new[] { "role", "state" };
                    else if (role == "navigation")
                    {
                        keys = values.ContainsKey("contentMode")
                            ? new[] { "role", "state", "context", "content", "selectedClass", "contentMode" }
                            : new[] { "role", "state", "context", "content", "selectedClass" };
                        if (values.TryGetValue("contentMode", out var mode) && mode != "publication")
                            throw new InvalidDataException("The explicit contentMode must be publication; omit it for static panels.");
                    }
                    else if (role == "publication") keys = new[] { "role", "state", "context", "value", "owner" };
                    else if (role == "tab") keys = values.ContainsKey("value")
                        ? new[] { "role", "target", "value" } : new[] { "role", "target" };
                    else if (IsGroup(role)) keys = new[] { "role", "state", "context" };
                    else throw new InvalidDataException("Unknown structure role: " + role);
                    if (values.Count != keys.Length || keys.Any(key => !values.ContainsKey(key)))
                        throw new InvalidDataException("This role requires exactly these properties: " + string.Join(", ", keys));
                    if (result.ContainsKey(element)) throw new InvalidDataException("An element must have one structure role.");
                    result.Add(element, values);
                }
                catch (Exception exception) when (exception is JsonException || exception is InvalidDataException || exception is ArgumentException)
                {
                    Issue(path, comment, report, "invalid-interaction-contract", exception.Message);
                }
            }
            return result;
        }

        private static void Issue(string path, XObject source, VmAutomationUxmlLayoutAuditReport report,
            string kind, string message)
        {
            var element = source as XElement ?? source?.Parent;
            report.Record(new VmAutomationUxmlLayoutAuditIssue { AssetPath = path,
                Line = (source as IXmlLineInfo)?.LineNumber ?? 0, Element = element?.Name.LocalName ?? "",
                ElementName = element == null ? "" : AttributeValue(element, "name"),
                Kind = kind, Severity = "error", Message = message }, false);
        }
    }
}
#endif
