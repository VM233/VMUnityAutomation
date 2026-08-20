#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine.UIElements;

namespace VMUnityAutomation.Editor
{
    internal static class MCPUIToolkitElementStyleBaseline
    {
        private static readonly Dictionary<string, ElementIdentity> identitiesByXmlType =
            new Dictionary<string, ElementIdentity>(StringComparer.Ordinal);

        internal static ElementIdentity Resolve(string namespaceName, string localName)
        {
            var cacheKey = $"{namespaceName}|{localName}";
            if (identitiesByXmlType.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            var fullTypeName = BuildFullTypeName(namespaceName, localName,
                out var assemblyName);
            var elementType = ResolveVisualElementType(fullTypeName, assemblyName);
            var classes = new HashSet<string>(StringComparer.Ordinal);
            if (TryCollectConstructedClasses(elementType, classes) == false)
            {
                for (var current = elementType;
                     current != null && typeof(VisualElement).IsAssignableFrom(current);
                     current = current.BaseType)
                {
                    TryAddUssClassName(current, classes);
                }
            }

            var componentTypeName = elementType?.FullName;
            if (string.IsNullOrWhiteSpace(componentTypeName))
            {
                componentTypeName = string.IsNullOrWhiteSpace(fullTypeName)
                    ? localName ?? "VisualElement"
                    : fullTypeName;
            }

            var identity = new ElementIdentity(componentTypeName,
                classes.OrderBy(value => value, StringComparer.Ordinal).ToArray());
            identitiesByXmlType[cacheKey] = identity;
            return identity;
        }

        private static string BuildFullTypeName(string namespaceName, string localName,
            out string assemblyName)
        {
            assemblyName = "";
            namespaceName = (namespaceName ?? "").Trim();
            localName = (localName ?? "").Trim();
            const string clrNamespacePrefix = "clr-namespace:";
            if (namespaceName.StartsWith(clrNamespacePrefix,
                    StringComparison.OrdinalIgnoreCase))
            {
                var declaration = namespaceName.Substring(clrNamespacePrefix.Length);
                var segments = declaration.Split(';');
                namespaceName = segments[0].Trim();
                foreach (var segment in segments.Skip(1))
                {
                    const string assemblyPrefix = "assembly=";
                    var trimmed = segment.Trim();
                    if (trimmed.StartsWith(assemblyPrefix,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        assemblyName = trimmed.Substring(assemblyPrefix.Length).Trim();
                    }
                }
            }

            return string.IsNullOrWhiteSpace(namespaceName)
                ? localName
                : $"{namespaceName}.{localName}";
        }

        private static Type ResolveVisualElementType(string fullTypeName,
            string assemblyName)
        {
            if (string.IsNullOrWhiteSpace(fullTypeName))
            {
                return null;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(assemblyName) == false &&
                        string.Equals(assembly.GetName().Name, assemblyName,
                            StringComparison.Ordinal) == false)
                    {
                        continue;
                    }

                    var candidate = assembly.GetType(fullTypeName, false);
                    if (candidate != null &&
                        typeof(VisualElement).IsAssignableFrom(candidate))
                    {
                        return candidate;
                    }
                }
                catch
                {
                    // Assemblies that cannot expose reflected UI Toolkit types are irrelevant.
                }
            }

            return null;
        }

        private static bool TryCollectConstructedClasses(Type elementType,
            ISet<string> classes)
        {
            if (elementType == null || elementType.IsAbstract)
            {
                return false;
            }

            try
            {
                if (Activator.CreateInstance(elementType, true) is not VisualElement element)
                {
                    return false;
                }

                foreach (var className in element.GetClasses())
                {
                    if (string.IsNullOrWhiteSpace(className) == false)
                    {
                        classes.Add(className);
                    }
                }

                return true;
            }
            catch
            {
                // UXML controls normally expose a parameterless constructor. A control
                // that cannot be safely constructed still retains the static class-name
                // fallback below.
                return false;
            }
        }

        private static void TryAddUssClassName(Type elementType,
            ISet<string> classes)
        {
            try
            {
                var field = elementType.GetField("ussClassName",
                    BindingFlags.Public | BindingFlags.NonPublic |
                    BindingFlags.Static | BindingFlags.DeclaredOnly);
                if (field != null && field.FieldType == typeof(string) &&
                    field.GetValue(null) is string className &&
                    string.IsNullOrWhiteSpace(className) == false)
                {
                    classes.Add(className);
                }
            }
            catch
            {
                // A third-party VisualElement can expose an unsafe static accessor.
                // Static auditing only consumes safe, readable class-name constants.
            }
        }

        internal sealed class ElementIdentity
        {
            internal readonly string ComponentTypeName;
            internal readonly IReadOnlyList<string> ImplicitClasses;

            internal ElementIdentity(string componentTypeName,
                IReadOnlyList<string> implicitClasses)
            {
                ComponentTypeName = componentTypeName;
                ImplicitClasses = implicitClasses;
            }
        }
    }
}
#endif
