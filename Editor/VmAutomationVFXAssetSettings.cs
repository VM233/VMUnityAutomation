using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationVFXAssetSettings
    {
        private const float MinimumPrewarmDeltaTime = 1f / 800f;
        private const uint MaximumPrewarmStepCount = 2400;
        private const string CompilationModeName = "compilationMode";
        private const string CompilationModeSnapshotKey = "$compilationMode";

        private sealed class Descriptor
        {
            internal Descriptor(string name, string serializedPath,
                Type valueType, string enumTypeName = null, object minimum = null,
                object maximum = null)
            {
                Name = name;
                SerializedPath = serializedPath;
                ValueType = valueType;
                EnumTypeName = enumTypeName;
                Minimum = minimum;
                Maximum = maximum;
            }

            internal string Name { get; }
            internal string SerializedPath { get; }
            internal Type ValueType { get; }
            internal string EnumTypeName { get; }
            internal object Minimum { get; }
            internal object Maximum { get; }

            internal Type ResolveValueType()
            {
                return string.IsNullOrEmpty(EnumTypeName)
                    ? ValueType
                    : VmAutomationVFXReflection.FindType(EnumTypeName);
            }
        }

        private static readonly Descriptor[] Descriptors =
        {
            new Descriptor("updateMode", "m_Infos.m_UpdateMode", null,
                VmAutomationVFXReflection.UpdateModeTypeName),
            new Descriptor("cullingFlags", "m_Infos.m_CullingFlags", null,
                VmAutomationVFXReflection.CullingFlagsTypeName),
            new Descriptor("motionVectorGenerationMode",
                "m_Infos.m_RendererSettings.motionVectorGenerationMode", null,
                VmAutomationVFXReflection.MotionVectorGenerationModeTypeName),
            new Descriptor("initialEventName", "m_Infos.m_InitialEventName",
                typeof(string)),
            new Descriptor("prewarmDeltaTime", "m_Infos.m_PreWarmDeltaTime",
                typeof(float), minimum: 0f),
            new Descriptor("prewarmStepCount", "m_Infos.m_PreWarmStepCount",
                typeof(uint), minimum: 0u, maximum: MaximumPrewarmStepCount),
            new Descriptor("instancingMode", "m_Infos.m_InstancingMode", null,
                VmAutomationVFXReflection.InstancingModeTypeName),
            new Descriptor("instancingCapacity", "m_Infos.m_InstancingCapacity",
                typeof(int), minimum: 1),
        };

        internal static Dictionary<string, object> Values(
            VmAutomationVFXGraphSession session)
        {
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            if (session == null || session.AssetKind != "graph")
                return result;
            foreach (Descriptor descriptor in Descriptors)
            {
                if (TryRead(session, descriptor, out object value))
                    result[descriptor.Name] = VmAutomationVFXValueCodec.Sanitize(value);
            }
            return result;
        }

        internal static List<Dictionary<string, object>> Summaries(
            VmAutomationVFXGraphSession session)
        {
            if (session == null || session.AssetKind != "graph")
                return new List<Dictionary<string, object>>();
            return Descriptors.Select(descriptor => Summary(session, descriptor))
                .ToList();
        }

        internal static Dictionary<string, object> CompilationModeSummary(
            VmAutomationVFXGraphSession session)
        {
            Type valueType = VmAutomationVFXReflection.FindType(
                VmAutomationVFXReflection.CompilationModeTypeName);
            bool available = valueType != null && session?.Graph != null &&
                FindGraphMethod(session.Graph, "GetCompilationMode", 0) != null &&
                FindGraphMethod(session.Graph, "SetCompilationMode", 2) != null;
            object value = available
                ? VmAutomationVFXReflection.Invoke(session.Graph, "GetCompilationMode")
                : null;
            return new Dictionary<string, object>
            {
                { "name", CompilationModeName },
                { "type", valueType?.FullName ??
                    VmAutomationVFXReflection.CompilationModeTypeName },
                { "value", VmAutomationVFXValueCodec.Sanitize(value) },
                { "visibility", "InGraph" },
                { "readOnly", !available },
                { "available", available },
                { "mutable", available },
                { "enumValues", valueType?.IsEnum == true
                    ? (object)Enum.GetNames(valueType).ToList()
                    : new List<string>() },
            };
        }

        internal static Dictionary<string, object> Capture(
            VmAutomationVFXGraphSession session)
        {
            Dictionary<string, object> result = Values(session);
            Dictionary<string, object> compilation =
                CompilationModeSummary(session);
            if (compilation.TryGetValue("available", out object available) &&
                available is bool isAvailable && isAvailable)
                result[CompilationModeSnapshotKey] = compilation["value"];
            return result;
        }

        internal static void Restore(VmAutomationVFXGraphSession session,
            IReadOnlyDictionary<string, object> snapshot)
        {
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));
            foreach (Descriptor descriptor in Descriptors)
            {
                if (snapshot.TryGetValue(descriptor.Name, out object value))
                    Write(session, descriptor, value, false);
            }
            if (snapshot.TryGetValue(CompilationModeSnapshotKey,
                    out object compilationMode))
                SetCompilationMode(session, compilationMode);
        }

        internal static Dictionary<string, object> Set(
            VmAutomationVFXGraphSession session, string name, object rawValue)
        {
            if (session == null || session.AssetKind != "graph")
                throw VmAutomationVFXError.Create("setting_not_found",
                    "VFX asset settings are available only on graph assets.");
            Descriptor descriptor = Descriptors.FirstOrDefault(item =>
                string.Equals(item.Name, name, StringComparison.Ordinal));
            if (descriptor == null)
                throw VmAutomationVFXError.Create("setting_not_found",
                    $"VFX asset setting '{name}' is not supported. Allowed names: {string.Join(", ", Descriptors.Select(item => item.Name))}.");
            object value = Write(session, descriptor, rawValue, true);
            return new Dictionary<string, object>
            {
                { "setting", name },
                { "value", VmAutomationVFXValueCodec.Sanitize(value) },
            };
        }

        internal static bool TrySetCompilationMode(VmAutomationVFXGraphSession session,
            string name, object rawValue)
        {
            if (!string.Equals(name, CompilationModeName,
                    StringComparison.Ordinal))
                return false;
            SetCompilationMode(session, rawValue);
            return true;
        }

        private static Dictionary<string, object> Summary(
            VmAutomationVFXGraphSession session, Descriptor descriptor)
        {
            Type valueType = descriptor.ResolveValueType();
            object value = null;
            bool available = valueType != null &&
                TryRead(session, descriptor, out value);
            return new Dictionary<string, object>
            {
                { "name", descriptor.Name },
                { "type", valueType?.FullName ?? descriptor.EnumTypeName ?? "" },
                { "value", VmAutomationVFXValueCodec.Sanitize(value) },
                { "available", available },
                { "mutable", available },
                { "owner", "VisualEffectResource" },
                { "serializedPath", descriptor.SerializedPath },
                { "minimum", descriptor.Minimum },
                { "maximum", descriptor.Maximum },
                { "enumValues", valueType?.IsEnum == true
                    ? (object)Enum.GetNames(valueType).ToList()
                    : new List<string>() },
            };
        }

        private static bool TryRead(VmAutomationVFXGraphSession session,
            Descriptor descriptor, out object value)
        {
            value = null;
            if (!(session?.Resource is UnityEngine.Object resource) ||
                resource == null || descriptor.ResolveValueType() == null)
                return false;
            var serialized = new SerializedObject(resource);
            serialized.UpdateIfRequiredOrScript();
            SerializedProperty property = serialized.FindProperty(
                descriptor.SerializedPath);
            if (property == null)
                return false;
            value = Read(property, descriptor.ResolveValueType());
            return true;
        }

        private static object Write(VmAutomationVFXGraphSession session,
            Descriptor descriptor, object rawValue, bool validate)
        {
            if (!(session?.Resource is UnityEngine.Object resource) ||
                resource == null)
                throw VmAutomationVFXError.Create("vfx_resource_unavailable",
                    "The VFX Graph resource is not a Unity object.");
            Type valueType = descriptor.ResolveValueType();
            if (valueType == null)
                throw VmAutomationVFXError.Create("unsupported_vfx_version",
                    $"The installed VFX Graph version does not expose {descriptor.EnumTypeName}.");
            object value = VmAutomationVFXValueCodec.ConvertTo(rawValue, valueType,
                "operation.value");
            if (validate)
                Validate(descriptor, value);

            var serialized = new SerializedObject(resource);
            serialized.UpdateIfRequiredOrScript();
            SerializedProperty property = serialized.FindProperty(
                descriptor.SerializedPath);
            if (property == null)
                throw VmAutomationVFXError.Create("unsupported_vfx_version",
                    $"The installed VFX Graph version does not expose asset setting '{descriptor.Name}' at '{descriptor.SerializedPath}'.");
            Assign(property, value, valueType);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(resource);
            return Read(property, valueType);
        }

        private static void SetCompilationMode(VmAutomationVFXGraphSession session,
            object rawValue)
        {
            Type valueType = VmAutomationVFXReflection.FindType(
                VmAutomationVFXReflection.CompilationModeTypeName);
            if (valueType == null || session?.Graph == null ||
                FindGraphMethod(session.Graph, "GetCompilationMode", 0) == null ||
                FindGraphMethod(session.Graph, "SetCompilationMode", 2) == null)
                throw VmAutomationVFXError.Create("unsupported_vfx_version",
                    "The installed VFX Graph version does not expose per-graph compilation mode APIs.");
            object value = VmAutomationVFXValueCodec.ConvertTo(rawValue, valueType,
                "operation.value");
            ValidateEnumValue(valueType, value, "operation.value");
            VmAutomationVFXReflection.Invoke(session.Graph, "SetCompilationMode", value,
                false);
        }

        private static MethodInfo FindGraphMethod(object graph, string name,
            int parameterCount)
        {
            return graph?.GetType().GetMethods(BindingFlags.Instance |
                    BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(method => method.Name == name &&
                    method.GetParameters().Length == parameterCount);
        }

        private static object Read(SerializedProperty property, Type valueType)
        {
            if (valueType.IsEnum)
                return Enum.ToObject(valueType, property.intValue);
            if (valueType == typeof(string))
                return property.stringValue;
            if (valueType == typeof(float))
                return property.floatValue;
            if (valueType == typeof(uint))
                return (uint)property.longValue;
            if (valueType == typeof(int))
                return property.intValue;
            throw new NotSupportedException(
                $"VFX asset setting type '{valueType.FullName}' is not supported.");
        }

        private static void Assign(SerializedProperty property, object value,
            Type valueType)
        {
            if (valueType.IsEnum)
                property.intValue = Convert.ToInt32(value);
            else if (valueType == typeof(string))
                property.stringValue = (string)value;
            else if (valueType == typeof(float))
                property.floatValue = (float)value;
            else if (valueType == typeof(uint))
                property.longValue = (uint)value;
            else if (valueType == typeof(int))
                property.intValue = (int)value;
            else
                throw new NotSupportedException(
                    $"VFX asset setting type '{valueType.FullName}' is not supported.");
        }

        private static void Validate(Descriptor descriptor, object value)
        {
            Type valueType = descriptor.ResolveValueType();
            if (valueType.IsEnum)
                ValidateEnumValue(valueType, value, "operation.value");
            if (descriptor.Name == "prewarmDeltaTime")
            {
                float deltaTime = (float)value;
                if (deltaTime > 0 && deltaTime < MinimumPrewarmDeltaTime)
                    throw new ArgumentOutOfRangeException("operation.value",
                        $"prewarmDeltaTime must be 0 or at least {MinimumPrewarmDeltaTime}.");
            }
            if (descriptor.Minimum != null &&
                Convert.ToDecimal(value) < Convert.ToDecimal(descriptor.Minimum))
                throw new ArgumentOutOfRangeException("operation.value",
                    $"{descriptor.Name} must be at least {descriptor.Minimum}.");
            if (descriptor.Maximum != null &&
                Convert.ToDecimal(value) > Convert.ToDecimal(descriptor.Maximum))
                throw new ArgumentOutOfRangeException("operation.value",
                    $"{descriptor.Name} must be at most {descriptor.Maximum}.");
        }

        private static void ValidateEnumValue(Type enumType, object value,
            string valuePath)
        {
            long numeric = Convert.ToInt64(value);
            if (enumType.GetCustomAttribute<FlagsAttribute>() != null)
            {
                long mask = Enum.GetValues(enumType).Cast<object>()
                    .Aggregate(0L, (current, item) =>
                        current | Convert.ToInt64(item));
                if ((numeric & ~mask) == 0)
                    return;
            }
            else if (Enum.IsDefined(enumType, value))
            {
                return;
            }
            throw new ArgumentException(
                $"{valuePath} is not a defined {enumType.FullName} value.");
        }
    }
}
