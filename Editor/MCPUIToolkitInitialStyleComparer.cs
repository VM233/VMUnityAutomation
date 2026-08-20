#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

namespace VMUnityAutomation.Editor
{
    internal static class MCPUIToolkitInitialStyleComparer
    {
        private static readonly ReflectionContract contract = new ReflectionContract();
        private static readonly Dictionary<string, bool> resultsByDeclaration =
            new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        internal static bool IsInitialValue(string property, string value)
        {
            property = (property ?? "").Trim();
            value = (value ?? "").Trim();
            if (property.StartsWith("--", StringComparison.Ordinal))
            {
                return false;
            }

            var cacheKey = property + "\n" + value;
            if (resultsByDeclaration.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            var result = contract.IsInitialValue(property, value);
            resultsByDeclaration[cacheKey] = result;
            return result;
        }

        private sealed class ReflectionContract
        {
            private const BindingFlags INSTANCE_MEMBERS =
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            private const BindingFlags STATIC_MEMBERS =
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

            private readonly ConstructorInfo importerConstructor;
            private readonly MethodInfo importStyleSheet;
            private readonly PropertyInfo styleSheetRules;
            private readonly PropertyInfo ruleProperties;
            private readonly PropertyInfo propertyId;
            private readonly FieldInfo requiresVariableResolve;
            private readonly ConstructorInfo propertyReaderConstructor;
            private readonly MethodInfo setInlineContext;
#if UNITY_6000_5_OR_NEWER
            private readonly object emptyVariableContext;
#endif
            private readonly MethodInfo acquireInitialStyle;
            private readonly MethodInfo applyProperties;
            private readonly MethodInfo finalizeApply;
            private readonly MethodInfo compareChanges;
            private readonly MethodInfo releaseStyle;
            private readonly MethodInfo isInheritedProperty;
            private readonly MethodInfo isShorthandProperty;
            private readonly MethodInfo allPropertyIds;
            private readonly MethodInfo isMatchingShorthand;
            private readonly long unchangedVersionFlags;

            internal ReflectionContract()
            {
                var uiAssembly = typeof(VisualElement).Assembly;
                var importerType = RequireUniqueLoadedType(
                    "UnityEditor.UIElements.StyleSheets.StyleSheetImporterImpl");
                var computedStyleType = RequireType(uiAssembly,
                    "UnityEngine.UIElements.ComputedStyle");
                var initialStyleType = RequireType(uiAssembly,
                    "UnityEngine.UIElements.StyleSheets.InitialStyle");
                var propertyReaderType = RequireType(uiAssembly,
                    "UnityEngine.UIElements.StyleSheets.StylePropertyReader");
                var styleDebugType = RequireType(uiAssembly,
                    "UnityEngine.UIElements.StyleDebug");
                var propertyUtilType = RequireType(uiAssembly,
                    "UnityEngine.UIElements.StyleSheets.StylePropertyUtil");
                var versionChangeType = RequireType(uiAssembly,
                    "UnityEngine.UIElements.VersionChangeType");

                importerConstructor = RequireConstructor(importerType);
                importStyleSheet = RequireMethod(importerType, "Import",
                    INSTANCE_MEMBERS, 2);
                styleSheetRules = RequireProperty(typeof(StyleSheet), "rules",
                    INSTANCE_MEMBERS);
                var ruleType = styleSheetRules.PropertyType.GetElementType() ??
                               throw new InvalidOperationException(
                                   "Unity StyleSheet.rules is not an array.");
                ruleProperties = RequireProperty(ruleType, "properties",
                    INSTANCE_MEMBERS);
                var propertyType = ruleProperties.PropertyType.GetElementType() ??
                                   throw new InvalidOperationException(
                                       "Unity StyleRule.properties is not an array.");
                propertyId = RequireProperty(propertyType, "id", INSTANCE_MEMBERS);
                requiresVariableResolve = RequireField(propertyType,
                    "requireVariableResolve", INSTANCE_MEMBERS);

                propertyReaderConstructor = RequireConstructor(propertyReaderType);
#if UNITY_6000_5_OR_NEWER
                setInlineContext = RequireMethod(propertyReaderType,
                    "SetInlineContext", INSTANCE_MEMBERS, 4);
                var variableContextType = setInlineContext.GetParameters()[2]
                    .ParameterType;
                emptyVariableContext = RequireField(variableContextType, "none",
                    STATIC_MEMBERS).GetValue(null);
#else
                setInlineContext = RequireMethod(propertyReaderType,
                    "SetInlineContext", INSTANCE_MEMBERS, 3);
#endif
                acquireInitialStyle = RequireMethod(initialStyleType,
                    "Acquire", STATIC_MEMBERS, 0);
                applyProperties = RequireMethod(computedStyleType,
                    "ApplyProperties", INSTANCE_MEMBERS, 2);
                finalizeApply = RequireMethod(computedStyleType,
                    "FinalizeApply", INSTANCE_MEMBERS, 1);
                compareChanges = RequireMethod(computedStyleType,
                    "CompareChanges", STATIC_MEMBERS, 2);
                releaseStyle = RequireMethod(computedStyleType,
                    "Release", INSTANCE_MEMBERS, 0);

                isInheritedProperty = RequireMethod(styleDebugType,
                    "IsInheritedProperty", STATIC_MEMBERS, 1);
                isShorthandProperty = RequireMethod(styleDebugType,
                    "IsShorthandProperty", STATIC_MEMBERS, 1);
                allPropertyIds = RequireMethod(propertyUtilType,
                    "AllPropertyIds", STATIC_MEMBERS, 0);
                isMatchingShorthand = RequireMethod(propertyUtilType,
                    "IsMatchingShorthand", STATIC_MEMBERS, 2);
                unchangedVersionFlags = Convert.ToInt64(
                    Enum.Parse(versionChangeType, "Styles"));
            }

            internal bool IsInitialValue(string property, string value)
            {
                var styleSheet = ScriptableObject.CreateInstance<StyleSheet>();
                try
                {
                    var importer = importerConstructor.Invoke(null);
                    importStyleSheet.Invoke(importer, new object[]
                    {
                        styleSheet,
                        $".unity-mcp-initial-style {{ {property}: {value}; }}"
                    });

                    var properties = ReadSingleRuleProperties(styleSheet,
                        property, value);
                    var styleProperty = properties.GetValue(0);
                    if ((bool)requiresVariableResolve.GetValue(styleProperty))
                    {
                        return false;
                    }

                    var id = propertyId.GetValue(styleProperty);
                    if (IncludesInheritedValue(id))
                    {
                        return false;
                    }

                    var reader = propertyReaderConstructor.Invoke(null);
#if UNITY_6000_5_OR_NEWER
                    setInlineContext.Invoke(reader, new[]
                    {
                        styleSheet, properties, emptyVariableContext, (object)1f
                    });
#else
                    setInlineContext.Invoke(reader,
                        new object[] { styleSheet, properties, 1f });
#endif
                    return ApplyingDeclarationLeavesInitialStyleUnchanged(reader);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(styleSheet);
                }
            }

            private Array ReadSingleRuleProperties(StyleSheet styleSheet,
                string property, string value)
            {
                var rules = styleSheetRules.GetValue(styleSheet) as Array;
                if (rules == null || rules.Length != 1)
                {
                    throw new InvalidOperationException(
                        $"Unity parsed '{property}: {value}' into " +
                        $"{rules?.Length ?? 0} rules; exactly one was required.");
                }

                var properties = ruleProperties.GetValue(rules.GetValue(0)) as Array;
                if (properties == null || properties.Length != 1)
                {
                    throw new InvalidOperationException(
                        $"Unity parsed '{property}: {value}' into " +
                        $"{properties?.Length ?? 0} properties; exactly one was required.");
                }

                return properties;
            }

            private bool IncludesInheritedValue(object id)
            {
                if ((bool)isInheritedProperty.Invoke(null, new[] { id }))
                {
                    return true;
                }

                if ((bool)isShorthandProperty.Invoke(null, new[] { id }) == false)
                {
                    return false;
                }

                var propertyIds = (IEnumerable)allPropertyIds.Invoke(null, null);
                foreach (var candidate in propertyIds)
                {
                    if ((bool)isInheritedProperty.Invoke(null,
                            new[] { candidate }) &&
                        (bool)isMatchingShorthand.Invoke(null,
                            new[] { id, candidate }))
                    {
                        return true;
                    }
                }

                return false;
            }

            private bool ApplyingDeclarationLeavesInitialStyleUnchanged(object reader)
            {
                var targetStyle = acquireInitialStyle.Invoke(null, null);
                var parentStyle = acquireInitialStyle.Invoke(null, null);
                try
                {
                    var applyArguments = new[] { reader, parentStyle };
                    applyProperties.Invoke(targetStyle, applyArguments);
                    parentStyle = applyArguments[1];

                    var finalizeArguments = new[] { parentStyle };
                    finalizeApply.Invoke(targetStyle, finalizeArguments);
                    parentStyle = finalizeArguments[0];

                    var compareArguments = new[] { targetStyle, parentStyle };
                    var changes = compareChanges.Invoke(null, compareArguments);
                    return Convert.ToInt64(changes) == unchangedVersionFlags;
                }
                finally
                {
                    releaseStyle.Invoke(targetStyle, null);
                    releaseStyle.Invoke(parentStyle, null);
                }
            }

            private static Type RequireUniqueLoadedType(string fullName)
            {
                var matches = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(assembly => assembly.GetType(fullName, false))
                    .Where(type => type != null)
                    .Distinct()
                    .ToList();
                if (matches.Count != 1)
                {
                    throw new InvalidOperationException(
                        $"Expected one loaded Unity type '{fullName}', found " +
                        $"{matches.Count}.");
                }

                return matches[0];
            }

            private static Type RequireType(Assembly assembly, string fullName)
            {
                return assembly.GetType(fullName, false) ??
                       throw new InvalidOperationException(
                           $"Unity type '{fullName}' is unavailable.");
            }

            private static ConstructorInfo RequireConstructor(Type type)
            {
                return type.GetConstructor(INSTANCE_MEMBERS, null,
                           Type.EmptyTypes, null) ??
                       throw new InvalidOperationException(
                           $"Unity type '{type.FullName}' has no parameterless constructor.");
            }

            private static MethodInfo RequireMethod(Type type, string name,
                BindingFlags flags, int parameterCount)
            {
                var matches = type.GetMethods(flags)
                    .Where(method => method.Name == name &&
                                     method.GetParameters().Length == parameterCount)
                    .ToList();
                if (matches.Count != 1)
                {
                    throw new InvalidOperationException(
                        $"Expected one Unity method '{type.FullName}.{name}' with " +
                        $"{parameterCount} parameter(s), found {matches.Count}.");
                }

                return matches[0];
            }

            private static PropertyInfo RequireProperty(Type type, string name,
                BindingFlags flags)
            {
                return type.GetProperty(name, flags) ??
                       throw new InvalidOperationException(
                           $"Unity property '{type.FullName}.{name}' is unavailable.");
            }

            private static FieldInfo RequireField(Type type, string name,
                BindingFlags flags)
            {
                return type.GetField(name, flags) ??
                       throw new InvalidOperationException(
                           $"Unity field '{type.FullName}.{name}' is unavailable.");
            }
        }
    }
}
#endif
