#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    /// <summary>
    /// Team-owned automation defaults stored below ProjectSettings.
    /// </summary>
    internal sealed class VmAutomationProjectConfiguration
    {
        internal const int CurrentSchemaVersion = 1;
        internal const string ConfigPath =
            "ProjectSettings/VMUnityAutomationSettings.json";
        internal const string DefaultPhysicsDimension = "3D";
        internal const string DefaultScreenshotDirectory = "Assets/Screenshots";

        internal bool Found;
        internal bool Valid = true;
        internal string Error = "";
        internal int SchemaVersion = CurrentSchemaVersion;
        internal readonly List<string> ExecuteCodeAdditionalNamespaces = new();
        internal string PhysicsDimension = DefaultPhysicsDimension;
        internal string ScreenshotDirectory = DefaultScreenshotDirectory;

        internal static VmAutomationProjectConfiguration Load()
        {
            var settings = new VmAutomationProjectConfiguration();
            string fullPath = GetFullPath();
            if (!File.Exists(fullPath))
                return settings;

            settings.Found = true;
            try
            {
                var values = MiniJson.Deserialize(
                    File.ReadAllText(fullPath)) as Dictionary<string, object>;
                if (values == null)
                    throw new InvalidDataException("The root JSON value must be an object.");

                int schemaVersion = GetInt(
                    values, "schemaVersion", CurrentSchemaVersion);
                if (schemaVersion > CurrentSchemaVersion)
                {
                    throw new InvalidDataException(
                        $"{ConfigPath} uses schema version {schemaVersion}, " +
                        $"but this package supports up to {CurrentSchemaVersion}.");
                }

                settings.SchemaVersion = Math.Max(1, schemaVersion);
                Dictionary<string, object> executeCode =
                    GetDictionary(values, "executeCode");
                settings.ExecuteCodeAdditionalNamespaces.AddRange(
                    NormalizeNamespaces(GetStringList(
                        executeCode, "additionalNamespaces")));

                Dictionary<string, object> toolDefaults =
                    GetDictionary(values, "toolDefaults");
                settings.PhysicsDimension = NormalizePhysicsDimension(GetString(
                    toolDefaults, "physicsDimension", DefaultPhysicsDimension));
                settings.ScreenshotDirectory = NormalizeProjectRelativePath(GetString(
                        toolDefaults, "screenshotDirectory", DefaultScreenshotDirectory),
                    DefaultScreenshotDirectory,
                    "toolDefaults.screenshotDirectory");
            }
            catch (Exception exception)
            {
                settings.Valid = false;
                settings.Error = exception.GetBaseException().Message;
            }

            return settings;
        }

        internal void Save()
        {
            if (!Valid)
            {
                throw new InvalidOperationException(
                    $"Cannot overwrite invalid {ConfigPath}: {Error}");
            }

            PhysicsDimension = NormalizePhysicsDimension(PhysicsDimension);
            ScreenshotDirectory = NormalizeProjectRelativePath(
                ScreenshotDirectory,
                DefaultScreenshotDirectory,
                "toolDefaults.screenshotDirectory");
            List<string> namespaces =
                NormalizeNamespaces(ExecuteCodeAdditionalNamespaces);
            ExecuteCodeAdditionalNamespaces.Clear();
            ExecuteCodeAdditionalNamespaces.AddRange(namespaces);

            var serialized = new SerializedConfiguration
            {
                schemaVersion = CurrentSchemaVersion,
                executeCode = new SerializedExecuteCode
                {
                    additionalNamespaces = ExecuteCodeAdditionalNamespaces.ToArray()
                },
                toolDefaults = new SerializedToolDefaults
                {
                    physicsDimension = PhysicsDimension,
                    screenshotDirectory = ScreenshotDirectory
                }
            };

            string fullPath = GetFullPath();
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(
                fullPath,
                JsonUtility.ToJson(serialized, true) + Environment.NewLine,
                new UTF8Encoding(false));

            SchemaVersion = CurrentSchemaVersion;
            Found = true;
            Valid = true;
            Error = "";
        }

        internal static string GetFullPath()
        {
            string projectRoot = Path.GetFullPath(
                Path.Combine(Application.dataPath, ".."));
            return Path.Combine(
                projectRoot,
                ConfigPath.Replace('/', Path.DirectorySeparatorChar));
        }

        internal static List<string> NormalizeNamespaces(
            IEnumerable<string> values)
        {
            return (values ?? Enumerable.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        private static string NormalizePhysicsDimension(string value)
        {
            value = (value ?? "").Trim();
            if (string.Equals(value, "2D", StringComparison.OrdinalIgnoreCase))
                return "2D";
            if (string.Equals(value, "3D", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrEmpty(value))
            {
                return "3D";
            }

            throw new InvalidDataException(
                "toolDefaults.physicsDimension must be either 2D or 3D.");
        }

        private static string NormalizeProjectRelativePath(
            string value,
            string fallback,
            string fieldName)
        {
            value = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            value = value.Replace('\\', '/').TrimEnd('/');
            if (string.IsNullOrEmpty(value))
                value = fallback;
            if (Path.IsPathRooted(value) ||
                value.Split('/').Any(segment =>
                    string.Equals(segment, "..", StringComparison.Ordinal)))
            {
                throw new InvalidDataException(
                    $"{fieldName} must be a project-relative path without '..' segments.");
            }

            while (value.StartsWith("./", StringComparison.Ordinal))
                value = value.Substring(2);
            return value;
        }

        private static Dictionary<string, object> GetDictionary(
            IDictionary<string, object> values,
            string key)
        {
            return values != null &&
                   values.TryGetValue(key, out object value) &&
                   value is Dictionary<string, object> dictionary
                ? dictionary
                : new Dictionary<string, object>();
        }

        private static string GetString(
            IDictionary<string, object> values,
            string key,
            string fallback)
        {
            return values != null &&
                   values.TryGetValue(key, out object value) &&
                   value != null
                ? value.ToString()
                : fallback;
        }

        private static int GetInt(
            IDictionary<string, object> values,
            string key,
            int fallback)
        {
            return values != null &&
                   values.TryGetValue(key, out object value) &&
                   value != null &&
                   int.TryParse(value.ToString(), out int parsed)
                ? parsed
                : fallback;
        }

        private static IEnumerable<string> GetStringList(
            IDictionary<string, object> values,
            string key)
        {
            if (values == null ||
                !values.TryGetValue(key, out object value) ||
                value == null)
            {
                return Enumerable.Empty<string>();
            }

            if (value is string text)
                return new[] { text };
            if (!(value is IEnumerable enumerable))
                return Enumerable.Empty<string>();

            var result = new List<string>();
            foreach (object item in enumerable)
            {
                if (item != null)
                    result.Add(item.ToString());
            }

            return result;
        }

        [Serializable]
        private sealed class SerializedConfiguration
        {
            public int schemaVersion = CurrentSchemaVersion;
            public SerializedExecuteCode executeCode = new();
            public SerializedToolDefaults toolDefaults = new();
        }

        [Serializable]
        private sealed class SerializedExecuteCode
        {
            public string[] additionalNamespaces = Array.Empty<string>();
        }

        [Serializable]
        private sealed class SerializedToolDefaults
        {
            public string physicsDimension = DefaultPhysicsDimension;
            public string screenshotDirectory = DefaultScreenshotDirectory;
        }
    }
}
#endif
