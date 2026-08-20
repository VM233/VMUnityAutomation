#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using static VMUnityAutomation.Editor.VmAutomationUxmlLayoutAuditor;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationUxmlLayoutModels
    {
    internal sealed class RepeatedInlineLayoutCandidate
    {
        public XElement Element;
        public string BaseClass;
        public string Signature;
        public Dictionary<string, string> Declarations;
        public List<string> RelatedVariantClasses;
    }

    internal sealed class UxmlSimpleSelector
    {
        public string Text;
        public string TypeName;
        public string Id;
        public int Specificity;
        public readonly List<string> ClassNames = new List<string>();

        public bool Matches(XElement element, IReadOnlyCollection<string> elementClasses)
        {
            if (string.IsNullOrWhiteSpace(TypeName) == false &&
                string.Equals(TypeName, element.Name.LocalName,
                    StringComparison.Ordinal) == false)
            {
                return false;
            }

            if (string.IsNullOrWhiteSpace(Id) == false &&
                string.Equals(Id, AttributeValue(element, "name"),
                    StringComparison.Ordinal) == false)
            {
                return false;
            }

            return ClassNames.All(elementClasses.Contains);
        }
    }

    internal sealed class UxmlInlineStyleRule
    {
        public string SourcePath;
        public UxmlSimpleSelector Selector;
        public int SourceOrder;
        public readonly Dictionary<string, string> Declarations =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    internal sealed class UxmlStylesheetDeclaration
    {
        public string Value;
        public string Selector;
        public string SourcePath;
        public int Specificity;
        public int SourceOrder;
    }

    internal sealed class UxmlInlineStyleContractIndex
    {
        private readonly List<UxmlInlineStyleRule> rules =
            new List<UxmlInlineStyleRule>();
        private int sourceOrder;

        public void AddRule(string sourcePath, UxmlSimpleSelector selector,
            IReadOnlyDictionary<string, string> declarations)
        {
            var rule = new UxmlInlineStyleRule
            {
                SourcePath = sourcePath,
                Selector = selector,
                SourceOrder = sourceOrder++
            };
            foreach (var declaration in declarations)
            {
                rule.Declarations[declaration.Key] = declaration.Value;
            }

            rules.Add(rule);
        }

        public Dictionary<string, UxmlStylesheetDeclaration> Resolve(XElement element)
        {
            var result = new Dictionary<string, UxmlStylesheetDeclaration>(
                StringComparer.OrdinalIgnoreCase);
            var elementClasses = GetElementClasses(element);
            foreach (var rule in rules)
            {
                if (rule.Selector.Matches(element, elementClasses) == false)
                {
                    continue;
                }

                foreach (var declaration in rule.Declarations)
                {
                    if (result.TryGetValue(declaration.Key, out var current) &&
                        (current.Specificity > rule.Selector.Specificity ||
                         current.Specificity == rule.Selector.Specificity &&
                         current.SourceOrder > rule.SourceOrder))
                    {
                        continue;
                    }

                    result[declaration.Key] = new UxmlStylesheetDeclaration
                    {
                        Value = declaration.Value,
                        Selector = rule.Selector.Text,
                        SourcePath = rule.SourcePath,
                        Specificity = rule.Selector.Specificity,
                        SourceOrder = rule.SourceOrder
                    };
                }
            }

            return result;
        }
    }

    internal sealed class UxmlElementNameReferenceIndex
    {
        public static readonly UxmlElementNameReferenceIndex Disabled =
            new UxmlElementNameReferenceIndex(false);

        private readonly HashSet<string> definitions =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> references =
            new HashSet<string>(StringComparer.Ordinal);

        public readonly bool Enabled;

        public int DefinitionCount => definitions.Count;

        public UxmlElementNameReferenceIndex(bool enabled)
        {
            Enabled = enabled;
        }

        public void AddDefinition(string name)
        {
            if (Enabled && string.IsNullOrWhiteSpace(name) == false)
            {
                definitions.Add(name.Trim());
            }
        }

        public void AddReference(string name)
        {
            if (Enabled == false || string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            var normalized = name.Trim();
            if (definitions.Contains(normalized))
            {
                references.Add(normalized);
            }
        }

        public bool IsReferenced(string name)
        {
            return Enabled &&
                   string.IsNullOrWhiteSpace(name) == false &&
                   references.Contains(name.Trim());
        }
    }

    internal sealed class UxmlLayoutContractIndex
    {
        public readonly HashSet<string> BoxClasses =
            new HashSet<string>(StringComparer.Ordinal);
        public readonly HashSet<string> BoxIds =
            new HashSet<string>(StringComparer.Ordinal);

        private readonly Dictionary<string, HashSet<string>> classLayoutProperties =
            new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, HashSet<string>> authoredVariants =
            new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        public void AddClassLayoutProperty(string className, string property)
        {
            if (!classLayoutProperties.TryGetValue(className, out var properties))
            {
                properties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                classLayoutProperties[className] = properties;
            }

            properties.Add(property);
        }

        public void AddAuthoredVariant(string baseClass, string variantClass)
        {
            if (!authoredVariants.TryGetValue(baseClass, out var variants))
            {
                variants = new HashSet<string>(StringComparer.Ordinal);
                authoredVariants[baseClass] = variants;
            }

            variants.Add(variantClass);
        }

        public IEnumerable<string> GetRelatedVariants(string baseClass,
            IEnumerable<string> inlineProperties)
        {
            if (!authoredVariants.TryGetValue(baseClass, out var variants))
            {
                return Enumerable.Empty<string>();
            }

            var propertySet = new HashSet<string>(
                inlineProperties ?? Enumerable.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);
            return variants.Where(variant =>
                    classLayoutProperties.TryGetValue(variant, out var properties) &&
                    properties.Overlaps(propertySet))
                .OrderBy(variant => variant, StringComparer.Ordinal);
        }

        public IEnumerable<string> GetClassLayoutProperties(string className)
        {
            return classLayoutProperties.TryGetValue(className, out var properties)
                ? properties
                : Enumerable.Empty<string>();
        }
    }
    }
}
#endif
