using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    internal sealed class VmAutomationScriptableSingletonTarget
    {
        private readonly MethodInfo saveMethod;

        private VmAutomationScriptableSingletonTarget(string settingsPath, MethodInfo saveMethod)
        {
            SettingsPath = settingsPath;
            this.saveMethod = saveMethod;
        }

        internal string SettingsPath { get; }

        internal void Save(UnityEngine.Object target)
        {
            // Unity exposes persistence only as a protected API on ScriptableSingleton<T>.
            saveMethod.Invoke(target, new object[] { true });
        }

        internal static object Resolve(string typeName, out UnityEngine.Object target,
            out VmAutomationScriptableSingletonTarget persistence)
        {
            target = null;
            persistence = null;
            Type type = VmAutomationComponentCommands.FindType(typeName);
            if (type == null || type.IsAbstract || type.ContainsGenericParameters)
            {
                return VmAutomationResponse.Error(
                    $"ScriptableSingleton type '{typeName}' was not found or is not concrete.",
                    "scriptable_singleton_type_invalid");
            }

            object error = Describe(type, out persistence);
            if (error != null)
                return error;
            if (persistence == null)
            {
                return VmAutomationResponse.Error(
                    $"Type '{typeName}' must derive from ScriptableSingleton<{type.Name}>.",
                    "scriptable_singleton_type_invalid");
            }

            Type singletonBase = typeof(ScriptableSingleton<>).MakeGenericType(type);
            target = (UnityEngine.Object)singletonBase.GetProperty("instance",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly).GetValue(null);
            return null;
        }

        internal static object Describe(Type type, out VmAutomationScriptableSingletonTarget persistence)
        {
            persistence = null;
            if (!typeof(ScriptableObject).IsAssignableFrom(type))
                return null;

            Type singletonBase = typeof(ScriptableSingleton<>).MakeGenericType(type);
            if (!singletonBase.IsAssignableFrom(type))
                return null;

            string filePath = (string)singletonBase.GetMethod("GetFilePath",
                BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly).Invoke(null, null);
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return VmAutomationResponse.Error(
                    $"ScriptableSingleton '{type.FullName}' has no FilePathAttribute; persistence is unavailable.",
                    "scriptable_singleton_file_path_missing");
            }

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string projectPrefix = projectRoot.TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string fullPath = Path.GetFullPath(Path.Combine(projectRoot, filePath));
            var comparison = Application.platform == RuntimePlatform.WindowsEditor
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            if (!fullPath.StartsWith(projectPrefix, comparison))
            {
                return VmAutomationResponse.Error(
                    $"ScriptableSingleton '{type.FullName}' stores its file outside the bound project: '{fullPath}'.",
                    "scriptable_singleton_file_path_outside_project");
            }

            string settingsPath = fullPath.Substring(projectPrefix.Length).Replace('\\', '/');
            MethodInfo save = singletonBase.GetMethod("Save",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            persistence = new VmAutomationScriptableSingletonTarget(settingsPath, save);
            return null;
        }
    }
}
