#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    internal sealed class VmAutomationUIToolkitAuditProjectSettings
    {
        internal const string ConfigPath = "ProjectSettings/VMUnityAutomationUIToolkitAudit.json";

        internal bool Found;
        internal bool Valid = true;
        internal string Error = "";
        internal bool AutomaticUssSingleUseStyles;
        internal bool AutomaticUxmlLayoutContracts;
        internal bool UxmlTooltipAttributes = true;
        internal bool PixelGridEnabled;
        internal int PixelGridStep = 3;
        internal readonly List<string> AssetRoots = new List<string> { "Assets" };
        internal readonly List<string> RuntimeSourceRoots = new List<string> { "Assets" };
        internal readonly List<string> ExcludePaths = new List<string>();
        internal readonly List<VmAutomationBuilderPreviewRequirement> RequiredBuilderPreviews =
            new List<VmAutomationBuilderPreviewRequirement>();

        internal static VmAutomationUIToolkitAuditProjectSettings Load()
        {
            var settings = new VmAutomationUIToolkitAuditProjectSettings();
            string fullPath = Path.Combine(VmAutomationUIToolkitAuditUtility.GetProjectRoot(),
                ConfigPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fullPath))
                return settings;

            settings.Found = true;
            try
            {
                var values = MiniJson.Deserialize(File.ReadAllText(fullPath)) as Dictionary<string, object>;
                if (values == null)
                    throw new InvalidDataException("The root JSON value must be an object.");

                Dictionary<string, object> automatic = GetDictionary(values, "automaticAudit");
                settings.AutomaticUssSingleUseStyles =
                    VmAutomationUIToolkitAuditUtility.GetBool(automatic, "ussSingleUseStyles", false);
                settings.AutomaticUxmlLayoutContracts =
                    VmAutomationUIToolkitAuditUtility.GetBool(automatic, "uxmlLayoutContracts", false);

                Dictionary<string, object> rules = GetDictionary(values, "rules");
                settings.UxmlTooltipAttributes =
                    VmAutomationUIToolkitAuditUtility.GetBool(rules, "uxmlTooltipAttributes", true);

                Dictionary<string, object> pixelGrid = GetDictionary(values, "pixelGrid");
                settings.PixelGridEnabled =
                    VmAutomationUIToolkitAuditUtility.GetBool(pixelGrid, "enabled", false);
                settings.PixelGridStep =
                    VmAutomationUIToolkitAuditUtility.GetInt(pixelGrid, "step", 3);
                if (settings.PixelGridStep <= 0)
                    throw new InvalidDataException("pixelGrid.step must be a positive integer.");

                ReplaceListWhenPresent(values, "assetRoots", settings.AssetRoots);
                ReplaceListWhenPresent(values, "runtimeSourceRoots", settings.RuntimeSourceRoots);
                ReplaceListWhenPresent(values, "excludePaths", settings.ExcludePaths);
                if (values.TryGetValue("requiredBuilderPreviews", out object previewValue))
                {
                    if (!(previewValue is IList previewList))
                        throw new InvalidDataException("requiredBuilderPreviews must be an array.");
                    foreach (object item in previewList)
                    {
                        if (!(item is Dictionary<string, object> entry))
                            throw new InvalidDataException("Each requiredBuilderPreviews entry must be an object.");
                        string path = entry.TryGetValue("path", out object rawPath)
                            ? VmAutomationUIToolkitAuditUtility.NormalizeAssetPath(rawPath?.ToString())
                            : string.Empty;
                        string elementName = entry.TryGetValue("elementName", out object rawName)
                            ? rawName?.ToString()?.Trim()
                            : string.Empty;
                        int minImages = VmAutomationUIToolkitAuditUtility.GetInt(entry,
                            "minImages", 0);
                        int minTextEntries = VmAutomationUIToolkitAuditUtility.GetInt(entry,
                            "minTextEntries", 0);
                        if (!path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
                            !path.EndsWith(".uxml", StringComparison.OrdinalIgnoreCase) ||
                            string.IsNullOrWhiteSpace(elementName) || minImages < 0 ||
                            minTextEntries < 0 || (minImages == 0 && minTextEntries == 0))
                            throw new InvalidDataException(
                                "Each requiredBuilderPreviews entry needs an Assets UXML path, elementName, and positive minImages or minTextEntries.");
                        if (settings.RequiredBuilderPreviews.Any(existing =>
                                string.Equals(existing.Path, path, StringComparison.OrdinalIgnoreCase) &&
                                string.Equals(existing.ElementName, elementName, StringComparison.Ordinal)))
                            throw new InvalidDataException(
                                $"Duplicate requiredBuilderPreviews target '{path}#{elementName}'.");
                        settings.RequiredBuilderPreviews.Add(new VmAutomationBuilderPreviewRequirement
                        {
                            Path = path,
                            ElementName = elementName,
                            MinImages = minImages,
                            MinTextEntries = minTextEntries
                        });
                    }
                }
                NormalizeList(settings.AssetRoots);
                NormalizeList(settings.RuntimeSourceRoots);
                NormalizeList(settings.ExcludePaths);

                if (settings.AssetRoots.Count == 0)
                    settings.AssetRoots.Add("Assets");
                if (settings.RuntimeSourceRoots.Count == 0)
                    settings.RuntimeSourceRoots.Add("Assets");
            }
            catch (Exception exception)
            {
                settings.Valid = false;
                settings.Error = exception.Message;
                settings.AutomaticUssSingleUseStyles = false;
                settings.AutomaticUxmlLayoutContracts = false;
                settings.PixelGridEnabled = false;
            }

            return settings;
        }

        internal void Save()
        {
            string fullPath = Path.Combine(VmAutomationUIToolkitAuditUtility.GetProjectRoot(),
                ConfigPath.Replace('/', Path.DirectorySeparatorChar));
            string directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(directory) == false)
                Directory.CreateDirectory(directory);

            var serialized = new SerializedConfig
            {
                automaticAudit = new SerializedAutomaticAudit
                {
                    ussSingleUseStyles = AutomaticUssSingleUseStyles,
                    uxmlLayoutContracts = AutomaticUxmlLayoutContracts
                },
                rules = new SerializedRules
                {
                    uxmlTooltipAttributes = UxmlTooltipAttributes
                },
                pixelGrid = new SerializedPixelGrid
                {
                    enabled = PixelGridEnabled,
                    step = Math.Max(1, PixelGridStep)
                },
                assetRoots = AssetRoots.ToArray(),
                runtimeSourceRoots = RuntimeSourceRoots.ToArray(),
                excludePaths = ExcludePaths.ToArray(),
                requiredBuilderPreviews = RequiredBuilderPreviews.Select(requirement =>
                    new SerializedBuilderPreviewRequirement
                    {
                        path = requirement.Path,
                        elementName = requirement.ElementName,
                        minImages = requirement.MinImages,
                        minTextEntries = requirement.MinTextEntries
                    }).ToArray()
            };
            File.WriteAllText(fullPath, JsonUtility.ToJson(serialized, true) + Environment.NewLine,
                new UTF8Encoding(false));
            Found = true;
            Valid = true;
            Error = "";
        }

        private static Dictionary<string, object> GetDictionary(
            IDictionary<string, object> values, string key)
        {
            object value;
            return values != null && values.TryGetValue(key, out value)
                ? value as Dictionary<string, object> ?? new Dictionary<string, object>()
                : new Dictionary<string, object>();
        }

        private static void ReplaceListWhenPresent(IDictionary<string, object> values, string key,
            ICollection<string> target)
        {
            if (values == null || !values.ContainsKey(key))
                return;

            target.Clear();
            foreach (string value in VmAutomationUIToolkitAuditUtility.GetStringList(values, key))
                target.Add(value);
        }

        private static void NormalizeList(IList<string> values)
        {
            var normalized = values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(VmAutomationUIToolkitAuditUtility.NormalizeAssetPath)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList();
            values.Clear();
            foreach (string value in normalized)
                values.Add(value);
        }

        [Serializable]
        private sealed class SerializedConfig
        {
            public SerializedAutomaticAudit automaticAudit;
            public SerializedRules rules;
            public SerializedPixelGrid pixelGrid;
            public string[] assetRoots;
            public string[] runtimeSourceRoots;
            public string[] excludePaths;
            public SerializedBuilderPreviewRequirement[] requiredBuilderPreviews;
        }

        [Serializable]
        private sealed class SerializedBuilderPreviewRequirement
        {
            public string path;
            public string elementName;
            public int minImages;
            public int minTextEntries;
        }

        [Serializable]
        private sealed class SerializedAutomaticAudit
        {
            public bool ussSingleUseStyles;
            public bool uxmlLayoutContracts;
        }

        [Serializable]
        private sealed class SerializedRules
        {
            public bool uxmlTooltipAttributes = true;
        }

        [Serializable]
        private sealed class SerializedPixelGrid
        {
            public bool enabled;
            public int step = 3;
        }
    }
}
#endif
