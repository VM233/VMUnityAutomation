using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationVFXGraphValidateCommands
    {
        internal static object Validate(Dictionary<string, object> args)
        {
            if (!ValidateKeys(args, new[]
                {
                    "assetPath", "mode", "diagnosticOffset", "maxDiagnostics",
                    "includeShaders", "includeShaderSource", "shaderOffset",
                    "maxShaders", "shaderSourceOffset",
                    "maxShaderSourceChars", "systemOffset", "maxSystems",
                    "eventOffset", "maxEvents", "exposedPropertyOffset",
                    "maxExposedProperties", "dependencyOffset",
                    "maxDependencies", "_agentId",
                }, out object keyError))
                return keyError;
            string assetPath = GetString(args, "assetPath");
            string mode = GetString(args, "mode", "inspect").ToLowerInvariant();
            if (mode != "inspect" && mode != "reimport" && mode != "compile")
                return VmAutomationResponse.Error(
                    "mode must be inspect, reimport, or compile.",
                    "invalid_arguments");
            if (!TryRange(args, "diagnosticOffset", 0, 0, int.MaxValue,
                    out int diagnosticOffset, out object rangeError) ||
                !TryRange(args, "maxDiagnostics", 200, 1, 1000,
                    out int maxDiagnostics, out rangeError) ||
                !TryRange(args, "shaderOffset", 0, 0, int.MaxValue,
                    out int shaderOffset, out rangeError) ||
                !TryRange(args, "maxShaders", 64, 1, 256,
                    out int maxShaders, out rangeError) ||
                !TryRange(args, "shaderSourceOffset", 0, 0, int.MaxValue,
                    out int shaderSourceOffset, out rangeError) ||
                !TryRange(args, "maxShaderSourceChars", 4096, 1, 4096,
                    out int maxSourceChars, out rangeError) ||
                !TryRange(args, "systemOffset", 0, 0, int.MaxValue,
                    out int systemOffset, out rangeError) ||
                !TryRange(args, "maxSystems", 100, 1, 1000,
                    out int maxSystems, out rangeError) ||
                !TryRange(args, "eventOffset", 0, 0, int.MaxValue,
                    out int eventOffset, out rangeError) ||
                !TryRange(args, "maxEvents", 100, 1, 1000,
                    out int maxEvents, out rangeError) ||
                !TryRange(args, "exposedPropertyOffset", 0, 0, int.MaxValue,
                    out int exposedPropertyOffset, out rangeError) ||
                !TryRange(args, "maxExposedProperties", 100, 1, 1000,
                    out int maxExposedProperties, out rangeError) ||
                !TryRange(args, "dependencyOffset", 0, 0, int.MaxValue,
                    out int dependencyOffset, out rangeError) ||
                !TryRange(args, "maxDependencies", 100, 1, 1000,
                    out int maxDependencies, out rangeError))
                return rangeError;
            if (!VmAutomationVFXGraphSession.TryOpen(assetPath,
                    out VmAutomationVFXGraphSession session, out object openError))
                return openError;

            try
            {
                object compileOutput = null;
                if (mode == "reimport")
                {
                    AssetDatabase.ImportAsset(assetPath,
                        ImportAssetOptions.ForceUpdate |
                        ImportAssetOptions.ForceSynchronousImport);
                    if (!VmAutomationVFXGraphSession.TryOpen(assetPath, out session,
                            out object reopenedError))
                        return reopenedError;
                }
                else if (mode == "compile")
                {
                    compileOutput = VmAutomationVFXReflection.Invoke(session.Graph,
                        "RecompileIfNeeded", false, false);
                    if (session.AssetKind == "graph")
                        VmAutomationVFXReflection.Invoke(session.Graph,
                            "CompileAndUpdateAsset", session.Asset);
                    VmAutomationVFXReflection.Invoke(session.Resource, "WriteAsset");
                    AssetDatabase.ImportAsset(assetPath,
                        ImportAssetOptions.ForceUpdate |
                        ImportAssetOptions.ForceSynchronousImport);
                    if (!VmAutomationVFXGraphSession.TryOpen(assetPath, out session,
                            out object reopenedError))
                        return reopenedError;
                }

                Dictionary<UnityEngine.Object, string> ids = session.BuildModelIds();
                List<Dictionary<string, object>> allDiagnostics =
                    VmAutomationVFXGraphInfoCommands.BuildDiagnostics(session.Graph,
                        session.Models.ToList(), ids);
                List<Dictionary<string, object>> diagnostics = allDiagnostics
                    .Skip(diagnosticOffset).Take(maxDiagnostics).ToList();
                bool includeShaders = GetBool(args, "includeShaders", true);
                bool includeSource = GetBool(args, "includeShaderSource", false);
                int shaderCount = ShaderCount(session.Resource);
                List<Dictionary<string, object>> shaders = includeShaders
                    ? ShaderManifest(session.Resource, includeSource,
                        shaderOffset, maxShaders, shaderSourceOffset,
                        maxSourceChars, shaderCount)
                    : new List<Dictionary<string, object>>();
                bool runtimeManifestAvailable = session.AssetKind == "graph";
                List<string> allSystems = runtimeManifestAvailable
                    ? AssetStringList(session.Asset, "GetParticleSystemNames")
                    : new List<string>();
                List<string> systems = allSystems.Skip(systemOffset)
                    .Take(maxSystems).ToList();
                List<string> allEvents = runtimeManifestAvailable
                    ? AssetStringList(session.Asset, "GetEvents")
                    : new List<string>();
                List<string> events = allEvents.Skip(eventOffset)
                    .Take(maxEvents).ToList();
                List<Dictionary<string, object>> allProperties =
                    runtimeManifestAvailable
                        ? ExposedPropertyManifest(session.Asset)
                        : new List<Dictionary<string, object>>();
                List<Dictionary<string, object>> properties = allProperties
                    .Skip(exposedPropertyOffset).Take(maxExposedProperties)
                    .ToList();
                List<string> allDependencies = Bounded(
                    AssetDatabase.GetDependencies(assetPath, true),
                    "asset dependencies");
                List<string> dependencies = allDependencies
                    .Skip(dependencyOffset).Take(maxDependencies).ToList();
                bool hasErrors = allDiagnostics.Any(IsError);
                var result = new Dictionary<string, object>
                {
                    { "success", !hasErrors },
                    { "assetPath", assetPath }, { "assetKind", session.AssetKind },
                    { "mode", mode }, { "compiled", mode == "compile" },
                    { "reimported", mode == "reimport" || mode == "compile" },
                    { "diagnosticCount", allDiagnostics.Count },
                    { "diagnosticOffset", diagnosticOffset },
                    { "returnedDiagnosticCount", diagnostics.Count },
                    { "diagnosticsTruncated", diagnosticOffset + diagnostics.Count <
                                                   allDiagnostics.Count },
                    { "nextDiagnosticOffset", diagnosticOffset + diagnostics.Count <
                                                allDiagnostics.Count
                        ? (object)(diagnosticOffset + diagnostics.Count) : null },
                    { "diagnostics", diagnostics },
                    { "runtimeManifestAvailable", runtimeManifestAvailable },
                    { "systems", systems }, { "systemCount", allSystems.Count },
                    { "systemOffset", systemOffset },
                    { "returnedSystemCount", systems.Count },
                    { "systemsTruncated", systemOffset + systems.Count <
                        allSystems.Count },
                    { "nextSystemOffset", systemOffset + systems.Count <
                        allSystems.Count ? (object)(systemOffset + systems.Count)
                        : null },
                    { "events", events }, { "eventCount", allEvents.Count },
                    { "eventOffset", eventOffset },
                    { "returnedEventCount", events.Count },
                    { "eventsTruncated", eventOffset + events.Count <
                        allEvents.Count },
                    { "nextEventOffset", eventOffset + events.Count <
                        allEvents.Count ? (object)(eventOffset + events.Count)
                        : null },
                    { "exposedProperties", properties },
                    { "exposedPropertyCount", allProperties.Count },
                    { "exposedPropertyOffset", exposedPropertyOffset },
                    { "returnedExposedPropertyCount", properties.Count },
                    { "exposedPropertiesTruncated", exposedPropertyOffset +
                        properties.Count < allProperties.Count },
                    { "nextExposedPropertyOffset", exposedPropertyOffset +
                        properties.Count < allProperties.Count
                            ? (object)(exposedPropertyOffset + properties.Count)
                            : null },
                    { "shaderCount", shaderCount },
                    { "shadersIncluded", includeShaders },
                    { "shaderOffset", shaderOffset },
                    { "returnedShaderCount", shaders.Count },
                    { "shadersTruncated", includeShaders &&
                        shaderOffset + shaders.Count <
                        shaderCount },
                    { "nextShaderOffset", includeShaders &&
                        shaderOffset + shaders.Count <
                        shaderCount
                            ? (object)(shaderOffset + shaders.Count) : null },
                    { "shaders", shaders },
                    { "dependencyCount", allDependencies.Count },
                    { "dependencyOffset", dependencyOffset },
                    { "returnedDependencyCount", dependencies.Count },
                    { "dependenciesTruncated", dependencyOffset +
                        dependencies.Count < allDependencies.Count },
                    { "nextDependencyOffset", dependencyOffset +
                        dependencies.Count < allDependencies.Count
                            ? (object)(dependencyOffset + dependencies.Count)
                            : null },
                    { "dependencies", dependencies },
                    { "compileOutput", CompileOutputSummary(compileOutput) },
                    { "instancingDisabledReason", InstancingReason(session,
                        compileOutput) },
                };
                if (hasErrors)
                {
                    result["error"] =
                        "VFX Graph validation reported one or more errors.";
                    result["message"] = result["error"];
                    result["errorCode"] = "vfx_compile_failed";
                    result["retryable"] = false;
                }
                return result;
            }
            catch (Exception exception)
            {
                return VmAutomationVFXError.Response(exception,
                    mode == "inspect" ? "unsupported_vfx_version" :
                    "vfx_compile_failed");
            }
        }

        private static List<Dictionary<string, object>> ShaderManifest(object resource,
            bool includeSource, int shaderOffset, int maxShaders,
            int sourceOffset, int maxSourceChars, int shaderCount)
        {
            var result = new List<Dictionary<string, object>>();
            long sourceCharacters = 0;
            int end = (int)Math.Min(shaderCount,
                (long)shaderOffset + maxShaders);
            for (int index = shaderOffset; index < end; index++)
            {
                string name = VmAutomationVFXReflection.Invoke(resource,
                    "GetShaderSourceName", index)?.ToString() ?? "";
                string source = includeSource
                    ? VmAutomationVFXReflection.Invoke(resource,
                        "GetShaderSource", index)?.ToString() ?? ""
                    : null;
                if (includeSource)
                {
                    sourceCharacters += source.Length;
                    if (sourceCharacters >
                        VmAutomationVFXLimits.ShaderSourceCharsPerRequest)
                        throw VmAutomationVFXError.Create("response_too_large",
                            $"Generated VFX shader sources exceed {VmAutomationVFXLimits.ShaderSourceCharsPerRequest} characters in one request. Reduce maxShaders or request sources individually.");
                }
                int returnedSourceChars = includeSource
                    ? Math.Min(maxSourceChars,
                        Math.Max(0, source.Length - sourceOffset)) : 0;
                var shader = new Dictionary<string, object>
                {
                    { "index", index }, { "name", name },
                    { "sourceLength", includeSource ? (object)source.Length : null },
                    { "sourceHash", includeSource ? Hash(source) : "" },
                    { "sourceIncluded", includeSource },
                    { "sourceOffset", sourceOffset },
                    { "returnedSourceChars", returnedSourceChars },
                    { "sourcePrefixOmitted", includeSource && sourceOffset > 0 },
                    { "sourceTruncated", includeSource && sourceOffset +
                        returnedSourceChars < source.Length },
                    { "nextSourceOffset", includeSource && sourceOffset +
                        returnedSourceChars < source.Length
                            ? (object)(sourceOffset + returnedSourceChars) : null },
                };
                if (includeSource)
                    shader["source"] = source.Substring(
                        Math.Min(sourceOffset, source.Length),
                        returnedSourceChars);
                result.Add(shader);
            }
            return result;
        }

        private static int ShaderCount(object resource)
        {
            return Convert.ToInt32(VmAutomationVFXReflection.Invoke(resource,
                "GetShaderSourceCount"));
        }

        private static List<string> AssetStringList(UnityEngine.Object asset,
            string methodName)
        {
            MethodInfo method = asset.GetType().GetMethods(BindingFlags.Instance |
                    BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(candidate => candidate.Name == methodName &&
                    candidate.GetParameters().Length == 1 &&
                    candidate.GetParameters()[0].ParameterType.IsGenericType &&
                    candidate.GetParameters()[0].ParameterType
                        .GetGenericArguments()[0] == typeof(string)) ??
                throw new MissingMethodException(asset.GetType().FullName,
                    methodName);
            object list = Activator.CreateInstance(
                method.GetParameters()[0].ParameterType);
            VmAutomationVFXReflection.InvokeMethod(method, asset, new[] { list });
            return Bounded(VmAutomationVFXReflection.Enumerate(list), methodName)
                .Select(item => item?.ToString() ?? "")
                .Distinct().OrderBy(item => item, StringComparer.Ordinal)
                .ToList();
        }

        private static List<Dictionary<string, object>> ExposedPropertyManifest(
            UnityEngine.Object asset)
        {
            MethodInfo method = asset.GetType().GetMethods(BindingFlags.Instance |
                    BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(candidate => candidate.Name == "GetExposedProperties" &&
                    candidate.GetParameters().Length == 1) ??
                throw new MissingMethodException(asset.GetType().FullName,
                    "GetExposedProperties");
            Type listType = method.GetParameters()[0].ParameterType;
            object list = Activator.CreateInstance(listType);
            VmAutomationVFXReflection.InvokeMethod(method, asset, new[] { list });
            return Bounded(VmAutomationVFXReflection.Enumerate(list),
                    "exposed properties").Select(property =>
                new Dictionary<string, object>
                {
                    { "name", VmAutomationVFXReflection.Get(property, "name")?.ToString() ?? "" },
                    { "type", (VmAutomationVFXReflection.Get(property, "type") as Type)?.FullName ??
                              VmAutomationVFXReflection.Get(property, "type")?.ToString() ?? "" },
                }).ToList();
        }

        private static Dictionary<string, object> CompileOutputSummary(object output)
        {
            if (output == null)
                return null;
            return new Dictionary<string, object>
            {
                { "type", output.GetType().FullName },
                { "expressionCount", Count(VmAutomationVFXReflection.Get(output,
                    "expressions")) },
                { "systemCount", Count(VmAutomationVFXReflection.Get(output,
                    "systemDesc")) },
                { "taskCount", Count(VmAutomationVFXReflection.Get(output,
                    "taskDesc")) },
                { "shaderSourceCount", Count(VmAutomationVFXReflection.Get(output,
                    "shaderSourceDesc")) },
                { "sourceDependencies", Bounded(VmAutomationVFXReflection.Enumerate(
                    VmAutomationVFXReflection.Get(output, "sourceDependencies")),
                    "compile source dependencies")
                    .Select(value => value?.ToString() ?? "").ToList() },
            };
        }

        private static List<T> Bounded<T>(IEnumerable<T> values, string label)
        {
            List<T> result = values.Take(VmAutomationVFXLimits.CollectionItems + 1)
                .ToList();
            if (result.Count > VmAutomationVFXLimits.CollectionItems)
                throw VmAutomationVFXError.Create("response_too_large",
                    $"VFX validation exposes more than {VmAutomationVFXLimits.CollectionItems} {label}.");
            return result;
        }

        private static object InstancingReason(VmAutomationVFXGraphSession session,
            object compileOutput)
        {
            object reason = VmAutomationVFXReflection.Get(compileOutput,
                "instancingDisabledReason");
            if (reason != null)
                return reason.ToString();
            var serialized = new SerializedObject(session.Asset);
            SerializedProperty property = serialized.FindProperty(
                "m_Infos.m_InstancingDisabledReason");
            return property != null ? property.intValue.ToString() : "";
        }

        private static bool IsError(Dictionary<string, object> diagnostic)
        {
            string severity = diagnostic.TryGetValue("severity", out object value)
                ? value?.ToString() ?? "" : "";
            return severity.Equals("Error", StringComparison.OrdinalIgnoreCase) ||
                   severity.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int Count(object value)
        {
            if (value is ICollection collection)
                return collection.Count;
            return VmAutomationVFXReflection.Enumerate(value).Count();
        }

        private static string Hash(string value)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(
                        Encoding.UTF8.GetBytes(value ?? "")))
                    .Replace("-", "").ToLowerInvariant();
        }

        private static bool ValidateKeys(Dictionary<string, object> args,
            IEnumerable<string> allowed, out object error)
        {
            var set = new HashSet<string>(allowed, StringComparer.Ordinal);
            string unknown = args?.Keys.FirstOrDefault(key => !set.Contains(key));
            if (unknown == null)
            {
                error = null;
                return true;
            }
            error = VmAutomationResponse.Error($"Unsupported argument '{unknown}'.",
                "invalid_arguments");
            return false;
        }

        private static bool TryRange(Dictionary<string, object> args, string key,
            int defaultValue, int min, int max, out int value, out object error)
        {
            try
            {
                value = args != null && args.TryGetValue(key, out object raw) &&
                        raw != null
                    ? (int)VmAutomationVFXValueCodec.ConvertTo(raw, typeof(int), key)
                    : defaultValue;
            }
            catch (Exception exception)
            {
                value = defaultValue;
                error = VmAutomationResponse.Error(
                    VmAutomationVFXReflection.Unwrap(exception).Message,
                    "invalid_arguments");
                return false;
            }
            if (value < min || value > max)
            {
                error = VmAutomationResponse.Error(
                    $"{key} must be between {min} and {max}.",
                    "invalid_arguments");
                return false;
            }
            error = null;
            return true;
        }

        private static string GetString(Dictionary<string, object> args, string key,
            string defaultValue = "")
        {
            return args != null && args.TryGetValue(key, out object value) &&
                   value != null ? value.ToString() : defaultValue;
        }

        private static bool GetBool(Dictionary<string, object> args, string key,
            bool defaultValue)
        {
            return args != null && args.TryGetValue(key, out object value) &&
                   value != null ? (bool)VmAutomationVFXValueCodec.ConvertTo(value,
                       typeof(bool), key) : defaultValue;
        }
    }
}
