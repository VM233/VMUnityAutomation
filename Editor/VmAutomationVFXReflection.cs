using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationVFXReflection
    {
        internal const string ActivationSlotSelector = "$activation";

        internal sealed class SlotReference
        {
            internal SlotReference(object slot, string selector)
            {
                Slot = slot;
                Selector = selector;
            }

            internal object Slot { get; }
            internal string Selector { get; }
        }

        internal const string ResourceTypeName = "UnityEditor.VFX.VisualEffectResource";
        internal const string ResourceExtensionsTypeName =
            "UnityEditor.VFX.VisualEffectResourceExtensions";
        internal const string GraphTypeName = "UnityEditor.VFX.VFXGraph";
        internal const string LibraryTypeName = "UnityEditor.VFX.VFXLibrary";
        internal const string ModelTypeName = "UnityEditor.VFX.VFXModel";
        internal const string ContextTypeName = "UnityEditor.VFX.VFXContext";
        internal const string BlockTypeName = "UnityEditor.VFX.VFXBlock";
        internal const string OperatorTypeName = "UnityEditor.VFX.VFXOperator";
        internal const string ParameterTypeName = "UnityEditor.VFX.VFXParameter";
        internal const string ParameterInfoTypeName =
            "UnityEditor.VFX.VFXParameterInfo";
        internal const string DataTypeName = "UnityEditor.VFX.VFXData";
        internal const string NodeIdTypeName = "UnityEditor.VFX.VFXNodeID";
        internal const string ValueTypeName = "UnityEngine.VFX.VFXValueType";
        internal const string VisualEffectAssetTypeName =
            "UnityEngine.VFX.VisualEffectAsset";
        internal const string VisualEffectSubgraphTypeName =
            "UnityEditor.VFX.VisualEffectSubgraph";
        internal const string AssetUtilityTypeName =
            "UnityEditor.VisualEffectAssetEditorUtility";
        internal const string VisualEffectTypeName = "UnityEngine.VFX.VisualEffect";
        internal const string VisualEffectRendererTypeName =
            "UnityEngine.VFX.VFXRenderer";
        internal const string TemplateHelperTypeName =
            "UnityEditor.VFX.VFXTemplateHelper";
        internal const string TemplateDescriptorTypeName =
            "UnityEditor.VFX.VFXTemplateDescriptor";
        internal const string PropertyBinderBaseTypeName =
            "UnityEngine.VFX.Utility.VFXBinderBase";
        internal const string EventBinderBaseTypeName =
            "UnityEngine.VFX.Utility.VFXEventBinderBase";
        internal const string OutputEventHandlerBaseTypeName =
            "UnityEngine.VFX.Utility.VFXOutputEventAbstractHandler";
        internal const string SpawnerCallbacksBaseTypeName =
            "UnityEngine.VFX.VFXSpawnerCallbacks";
        internal const string ViewPreferenceTypeName =
            "UnityEditor.VFX.VFXViewPreference";
        internal const string CompilationModeTypeName =
            "UnityEngine.VFX.VFXCompilationMode";
        internal const string UpdateModeTypeName =
            "UnityEngine.VFX.VFXUpdateMode";
        internal const string CullingFlagsTypeName =
            "UnityEngine.VFX.VFXCullingFlags";
        internal const string InstancingModeTypeName =
            "UnityEngine.VFX.VFXInstancingMode";
        internal const string MotionVectorGenerationModeTypeName =
            "UnityEngine.MotionVectorGenerationMode";
        internal const string CameraBufferFallbackTypeName =
            "UnityEngine.VFX.VFXMainCameraBufferFallback";
        internal const string MeshToSdfBakerTypeName =
            "UnityEngine.VFX.SDF.MeshToSDFBaker";
        internal const string PointCacheBakeToolTypeName =
            "UnityEditor.Experimental.VFX.Utility.PointCacheBakeTool";
        internal const string PointCacheTypeName =
            "UnityEditor.Experimental.VFX.Utility.PCache";
        private static readonly Dictionary<string, Type> TypeCache =
            new Dictionary<string, Type>(StringComparer.Ordinal);

        private static readonly string[] CoreTypeNames =
        {
            VisualEffectAssetTypeName,
        };

        internal static bool IsAvailable =>
            MissingRequiredTypeNames().Count == 0;

        internal static IReadOnlyList<string> MissingRequiredTypeNames()
        {
            return CoreTypeNames.Where(name => FindType(name) == null)
                .ToList();
        }

        internal static List<Dictionary<string, object>> CapabilitySummaries()
        {
            return new List<Dictionary<string, object>>
            {
                Capability("graph-authoring", new[]
                {
                    ResourceTypeName, ResourceExtensionsTypeName, GraphTypeName,
                    LibraryTypeName, ModelTypeName, ContextTypeName,
                    BlockTypeName, OperatorTypeName, ParameterTypeName,
                    DataTypeName,
                }, "vfxgraph/catalog", "vfxgraph/info",
                    "vfxgraph/transaction", "vfxgraph/validate"),
                Capability("graph-asset-creation", new[]
                {
                    AssetUtilityTypeName,
                }, "vfxgraph/create"),
                Capability("subgraph-asset-creation", new[]
                {
                    AssetUtilityTypeName, VisualEffectSubgraphTypeName,
                }, "vfxgraph/create"),
                Capability("templates", new[]
                {
                    AssetUtilityTypeName, TemplateHelperTypeName,
                    TemplateDescriptorTypeName,
                }, "vfxgraph/catalog", "vfxgraph/create"),
                Capability("runtime-component", new[]
                {
                    VisualEffectTypeName, VisualEffectRendererTypeName,
                }, "vfxgraph/component-control"),
                Capability("component-authoring", new[]
                {
                    VisualEffectTypeName, VisualEffectRendererTypeName,
                    ParameterInfoTypeName, GraphTypeName,
                }, "vfxgraph/component-info",
                    "vfxgraph/component-transaction",
                    "vfxgraph/info"),
                Capability("property-binders", new[]
                {
                    PropertyBinderBaseTypeName,
                }, "vfxgraph/catalog", "component/*", "prefab-asset/*"),
                Capability("event-binders", new[]
                {
                    EventBinderBaseTypeName,
                }, "vfxgraph/catalog", "component/*", "prefab-asset/*"),
                Capability("output-event-handlers", new[]
                {
                    OutputEventHandlerBaseTypeName,
                }, "vfxgraph/catalog", "component/*", "prefab-asset/*"),
                Capability("spawner-callbacks", new[]
                {
                    SpawnerCallbacksBaseTypeName,
                }, "vfxgraph/catalog", "vfxgraph/transaction"),
                Capability("settings", new[]
                {
                    ViewPreferenceTypeName, CompilationModeTypeName,
                    CameraBufferFallbackTypeName,
                }, "vfxgraph/settings-info",
                    "vfxgraph/settings-transaction"),
                Capability("point-cache-bake", new[]
                {
                    PointCacheBakeToolTypeName, PointCacheTypeName,
                }, "vfxgraph/bake"),
                Capability("sdf-bake", new[]
                {
                    MeshToSdfBakerTypeName,
                }, "vfxgraph/bake"),
            };
        }

        internal static Type FindType(string fullName)
        {
            if (string.IsNullOrEmpty(fullName))
                return null;
            if (TypeCache.TryGetValue(fullName, out Type cached))
                return cached;

            Type resolved = VmAutomationAssetGraphUtility.FindType(fullName);
            TypeCache[fullName] = resolved;
            return resolved;
        }

        internal static Type RequireType(string fullName)
        {
            return FindType(fullName) ?? throw new MissingMemberException(fullName);
        }

        internal static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            if (assembly == null)
                return Enumerable.Empty<Type>();
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException exception)
            {
                return exception.Types.Where(type => type != null);
            }
        }

        internal static bool HasBaseType(Type type, string fullOrShortName)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                if (string.Equals(current.FullName, fullOrShortName,
                        StringComparison.Ordinal) ||
                    string.Equals(current.Name, fullOrShortName,
                        StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        internal static object Get(object target, string memberName)
        {
            if (target == null)
                return null;
            Type type = target as Type ?? target.GetType();
            bool isStatic = target is Type;
            PropertyInfo property = FindProperty(type, memberName, isStatic);
            if (property != null)
                return property.GetValue(isStatic ? null : target, null);
            FieldInfo field = FindField(type, memberName, isStatic);
            return field?.GetValue(isStatic ? null : target);
        }

        internal static bool TrySet(object target, string memberName, object value)
        {
            if (target == null)
                return false;
            Type type = target as Type ?? target.GetType();
            bool isStatic = target is Type;
            PropertyInfo property = FindProperty(type, memberName, isStatic);
            if (property != null && property.CanWrite)
            {
                property.SetValue(isStatic ? null : target, value, null);
                return true;
            }
            FieldInfo field = FindField(type, memberName, isStatic);
            if (field == null || field.IsInitOnly)
                return false;
            field.SetValue(isStatic ? null : target, value);
            return true;
        }

        internal static Type GetMemberType(object target, string memberName)
        {
            if (target == null)
                return null;
            Type type = target as Type ?? target.GetType();
            bool isStatic = target is Type;
            PropertyInfo property = FindProperty(type, memberName, isStatic);
            if (property != null)
                return property.PropertyType;
            return FindField(type, memberName, isStatic)?.FieldType;
        }

        internal static PropertyInfo FindProperty(Type type, string name,
            bool isStatic = false)
        {
            BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic |
                                 BindingFlags.DeclaredOnly |
                                 (isStatic ? BindingFlags.Static : BindingFlags.Instance);
            for (Type current = type; current != null; current = current.BaseType)
            {
                PropertyInfo property = current.GetProperties(flags)
                    .FirstOrDefault(candidate =>
                        string.Equals(candidate.Name, name, StringComparison.Ordinal) &&
                        candidate.GetIndexParameters().Length == 0);
                if (property != null)
                    return property;
            }
            return null;
        }

        internal static FieldInfo FindField(Type type, string name,
            bool isStatic = false)
        {
            BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic |
                                 BindingFlags.DeclaredOnly |
                                 (isStatic ? BindingFlags.Static : BindingFlags.Instance);
            for (Type current = type; current != null; current = current.BaseType)
            {
                FieldInfo field = current.GetField(name, flags);
                if (field != null)
                    return field;
            }
            return null;
        }

        internal static object Invoke(object target, string methodName,
            params object[] arguments)
        {
            if (target == null)
                throw new InvalidOperationException(
                    $"Cannot invoke '{methodName}' on a null VFX target.");
            Type type = target as Type ?? target.GetType();
            bool isStatic = target is Type;
            MethodInfo method = FindMethod(type, methodName, arguments, isStatic);
            if (method == null)
            {
                throw new MissingMethodException(type.FullName, methodName + "(" +
                    string.Join(", ", (arguments ?? Array.Empty<object>())
                        .Select(argument => argument?.GetType().FullName ?? "null")) + ")");
            }

            try
            {
                return method.Invoke(isStatic ? null : target,
                    CompleteOptionalArguments(method, arguments));
            }
            catch (TargetInvocationException exception)
            {
                throw Unwrap(exception);
            }
        }

        internal static bool TryInvoke(object target, string methodName,
            out object result, params object[] arguments)
        {
            result = null;
            if (target == null)
                return false;
            Type type = target as Type ?? target.GetType();
            bool isStatic = target is Type;
            MethodInfo method = FindMethod(type, methodName, arguments, isStatic);
            if (method == null)
                return false;
            try
            {
                result = method.Invoke(isStatic ? null : target,
                    CompleteOptionalArguments(method, arguments));
                return true;
            }
            catch (TargetInvocationException exception)
            {
                throw Unwrap(exception);
            }
        }

        internal static object InvokeExact(object target, string methodName,
            Type[] parameterTypes, params object[] arguments)
        {
            if (target == null)
                throw new InvalidOperationException(
                    $"Cannot invoke '{methodName}' on a null VFX target.");
            Type type = target as Type ?? target.GetType();
            bool isStatic = target is Type;
            BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic |
                                 (isStatic ? BindingFlags.Static : BindingFlags.Instance);
            MethodInfo method = type.GetMethod(methodName, flags, null,
                parameterTypes ?? Type.EmptyTypes, null);
            if (method == null)
                throw new MissingMethodException(type.FullName, methodName);
            try
            {
                return method.Invoke(isStatic ? null : target, arguments);
            }
            catch (TargetInvocationException exception)
            {
                throw Unwrap(exception);
            }
        }

        internal static object InvokeMethod(MethodInfo method, object target,
            object[] arguments)
        {
            if (method == null)
                throw new ArgumentNullException(nameof(method));
            try
            {
                return method.Invoke(method.IsStatic ? null : target, arguments);
            }
            catch (TargetInvocationException exception)
            {
                throw Unwrap(exception);
            }
        }

        internal static IEnumerable<object> Enumerate(object value)
        {
            if (value is IEnumerable enumerable && !(value is string))
            {
                foreach (object item in enumerable)
                    yield return item;
            }
        }

        internal static IReadOnlyList<SlotReference> EnumerateSlots(
            object model, string memberName)
        {
            var result = new List<SlotReference>();
            List<object> roots = Enumerate(Get(model, memberName)).ToList();
            object activationSlot = string.Equals(memberName, "inputSlots",
                    StringComparison.Ordinal)
                ? Get(model, "activationSlot")
                : null;
            for (int index = 0; index < roots.Count; index++)
            {
                if (ReferenceEquals(roots[index], activationSlot))
                    continue;
                AppendSlotTree(roots[index], $"[{index}]", 0, result);
            }
            if (activationSlot != null)
                AppendSlotTree(activationSlot, ActivationSlotSelector, 0, result);
            return result;
        }

        internal static string StableId(UnityEngine.Object target, int fallbackIndex = -1)
        {
            if (target != null &&
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(target, out string _,
                    out long localId) && localId != 0)
                return localId.ToString();
            return fallbackIndex >= 0 ? "transient:" + fallbackIndex : "";
        }

        internal static string SemanticName(object model)
        {
            string value = Get(model, "name")?.ToString();
            if (!string.IsNullOrEmpty(value))
                return value;
            return (model as UnityEngine.Object)?.name ?? "";
        }

        internal static Dictionary<string, object> Vector2Value(object value)
        {
            if (!(value is Vector2 vector))
                return null;
            return new Dictionary<string, object>
            {
                { "x", vector.x },
                { "y", vector.y },
            };
        }

        internal static Dictionary<string, object> RectValue(object value)
        {
            if (!(value is Rect rect))
                return null;
            return new Dictionary<string, object>
            {
                { "x", rect.x },
                { "y", rect.y },
                { "width", rect.width },
                { "height", rect.height },
            };
        }

        internal static Exception Unwrap(Exception exception)
        {
            Exception current = exception;
            while (current is TargetInvocationException invocation &&
                   invocation.InnerException != null)
                current = invocation.InnerException;
            return current;
        }

        private static MethodInfo FindMethod(Type type, string name,
            IReadOnlyList<object> arguments, bool isStatic)
        {
            BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic |
                                 (isStatic ? BindingFlags.Static : BindingFlags.Instance);
            object[] supplied = arguments?.ToArray() ?? Array.Empty<object>();
            return type.GetMethods(flags)
                .Where(method => string.Equals(method.Name, name,
                    StringComparison.Ordinal))
                .Where(method => !method.ContainsGenericParameters)
                .Where(method =>
                {
                    ParameterInfo[] parameters = method.GetParameters();
                    if (supplied.Length > parameters.Length)
                        return false;
                    for (int index = supplied.Length; index < parameters.Length;
                         index++)
                    {
                        if (!TryGetOptionalDefault(method, index, out object _))
                            return false;
                    }
                    for (int index = 0; index < supplied.Length; index++)
                    {
                        object argument = supplied[index];
                        Type parameterType = parameters[index].ParameterType;
                        if (parameterType.IsByRef)
                            parameterType = parameterType.GetElementType();
                        if (argument == null)
                        {
                            if (parameterType != null && parameterType.IsValueType &&
                                Nullable.GetUnderlyingType(parameterType) == null)
                                return false;
                            continue;
                        }
                        if (parameterType == null ||
                            !parameterType.IsInstanceOfType(argument))
                            return false;
                    }
                    return true;
                })
                .OrderBy(method => method.GetParameters().Length)
                .FirstOrDefault();
        }

        private static void AppendSlotTree(object slot, string selector, int depth,
            ICollection<SlotReference> result)
        {
            if (slot == null)
                return;
            if (depth > VmAutomationVFXLimits.SlotDepth)
                throw VmAutomationVFXError.Create("response_too_large",
                    $"VFX slot selector '{selector}' exceeds the supported recursive depth of {VmAutomationVFXLimits.SlotDepth}.");
            if (result.Count >= VmAutomationVFXLimits.SlotsPerModelDirection)
                throw VmAutomationVFXError.Create("response_too_large",
                    $"A VFX model exposes more than {VmAutomationVFXLimits.SlotsPerModelDirection} recursive slots in one direction.");
            result.Add(new SlotReference(slot, selector));
            List<object> children = Enumerate(Get(slot, "children")).ToList();
            for (int index = 0; index < children.Count; index++)
                AppendSlotTree(children[index], selector + $"[{index}]",
                    depth + 1, result);
        }

        private static Dictionary<string, object> Capability(string name,
            IEnumerable<string> requiredTypes, params string[] ownerRoutes)
        {
            List<string> required = requiredTypes.Distinct().ToList();
            List<string> missing = required.Where(typeName =>
                FindType(typeName) == null).ToList();
            return new Dictionary<string, object>
            {
                { "name", name },
                { "available", missing.Count == 0 },
                { "requiredTypes", required },
                { "missingTypes", missing },
                { "ownerRoutes", ownerRoutes.ToList() },
            };
        }

        private static object[] CompleteOptionalArguments(MethodInfo method,
            IReadOnlyList<object> arguments)
        {
            ParameterInfo[] parameters = method.GetParameters();
            object[] result = new object[parameters.Length];
            int suppliedCount = arguments?.Count ?? 0;
            for (int index = 0; index < parameters.Length; index++)
            {
                if (index < suppliedCount)
                {
                    result[index] = arguments[index];
                    continue;
                }
                if (!TryGetOptionalDefault(method, index,
                        out object defaultValue))
                    throw new MissingMethodException(method.DeclaringType?.FullName,
                        method.Name);
                result[index] = defaultValue;
            }
            return result;
        }

        private static bool TryGetOptionalDefault(MethodInfo method, int index,
            out object value)
        {
            ParameterInfo[] parameters = method.GetParameters();
            if (index >= 0 && index < parameters.Length &&
                parameters[index].IsOptional)
            {
                value = parameters[index].DefaultValue;
                return true;
            }

            MethodInfo baseMethod = method.GetBaseDefinition();
            ParameterInfo[] baseParameters = baseMethod.GetParameters();
            if (!ReferenceEquals(baseMethod, method) && index >= 0 &&
                index < baseParameters.Length && baseParameters[index].IsOptional)
            {
                value = baseParameters[index].DefaultValue;
                return true;
            }
            value = null;
            return false;
        }
    }
}
