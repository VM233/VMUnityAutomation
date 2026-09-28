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
    internal sealed class VmAutomationUIToolkitAuditOptions
    {
        internal readonly List<string> AssetRoots;
        internal readonly List<string> RuntimeSourceRoots;
        internal readonly List<string> ExcludePaths;
        internal readonly bool UxmlTooltipAttributes;
        internal readonly bool PixelGridEnabled;
        internal readonly int PixelGridStep;
        internal readonly List<VmAutomationBuilderPreviewRequirement> RequiredBuilderPreviews;

        private VmAutomationUIToolkitAuditOptions(IEnumerable<string> assetRoots,
            IEnumerable<string> runtimeSourceRoots, IEnumerable<string> excludePaths,
            bool uxmlTooltipAttributes, bool pixelGridEnabled, int pixelGridStep,
            IEnumerable<VmAutomationBuilderPreviewRequirement> requiredBuilderPreviews)
        {
            AssetRoots = NormalizeRoots(assetRoots, "Assets");
            RuntimeSourceRoots = NormalizeRoots(runtimeSourceRoots, "Assets");
            ExcludePaths = NormalizeRoots(excludePaths, null);
            UxmlTooltipAttributes = uxmlTooltipAttributes;
            PixelGridEnabled = pixelGridEnabled;
            PixelGridStep = Math.Max(1, pixelGridStep);
            RequiredBuilderPreviews = (requiredBuilderPreviews ??
                Enumerable.Empty<VmAutomationBuilderPreviewRequirement>()).ToList();
        }

        internal static VmAutomationUIToolkitAuditOptions FromArguments(Dictionary<string, object> args)
        {
            args = args ?? new Dictionary<string, object>();
            bool useProjectSettings =
                VmAutomationUIToolkitAuditUtility.GetBool(args, "useProjectSettings", true);
            VmAutomationUIToolkitAuditProjectSettings settings = useProjectSettings
                ? VmAutomationUIToolkitAuditProjectSettings.Load()
                : new VmAutomationUIToolkitAuditProjectSettings();

            IEnumerable<string> assetRoots = args.ContainsKey("roots")
                ? VmAutomationUIToolkitAuditUtility.GetStringList(args, "roots")
                : settings.AssetRoots;
            IEnumerable<string> runtimeRoots = args.ContainsKey("runtimeSourceRoots")
                ? VmAutomationUIToolkitAuditUtility.GetStringList(args, "runtimeSourceRoots")
                : settings.RuntimeSourceRoots;
            IEnumerable<string> excludePaths = args.ContainsKey("excludePaths")
                ? VmAutomationUIToolkitAuditUtility.GetStringList(args, "excludePaths")
                : settings.ExcludePaths;
            bool pixelGridEnabled = args.ContainsKey("pixelGridEnabled")
                ? VmAutomationUIToolkitAuditUtility.GetBool(args, "pixelGridEnabled")
                : settings.PixelGridEnabled;
            int pixelGridStep = args.ContainsKey("pixelGridStep")
                ? VmAutomationUIToolkitAuditUtility.GetInt(args, "pixelGridStep",
                    settings.PixelGridStep)
                : settings.PixelGridStep;

            return new VmAutomationUIToolkitAuditOptions(assetRoots, runtimeRoots, excludePaths,
                settings.UxmlTooltipAttributes, pixelGridEnabled, pixelGridStep,
                settings.RequiredBuilderPreviews);
        }

        internal static VmAutomationUIToolkitAuditOptions FromProjectSettings(
            VmAutomationUIToolkitAuditProjectSettings settings)
        {
            settings = settings ?? VmAutomationUIToolkitAuditProjectSettings.Load();
            return new VmAutomationUIToolkitAuditOptions(settings.AssetRoots,
                settings.RuntimeSourceRoots, settings.ExcludePaths,
                settings.UxmlTooltipAttributes, settings.PixelGridEnabled,
                settings.PixelGridStep, settings.RequiredBuilderPreviews);
        }

        internal bool Includes(string assetPath)
        {
            string normalized = VmAutomationUIToolkitAuditUtility.NormalizeAssetPath(assetPath);
            return AssetRoots.Any(root => IsAtOrBelow(normalized, root)) &&
                   !ExcludePaths.Any(excluded => IsAtOrBelow(normalized, excluded));
        }

        internal bool IncludesRuntimeSource(string assetPath)
        {
            string normalized = VmAutomationUIToolkitAuditUtility.NormalizeAssetPath(assetPath);
            return RuntimeSourceRoots.Any(root => IsAtOrBelow(normalized, root)) &&
                   !ExcludePaths.Any(excluded => IsAtOrBelow(normalized, excluded));
        }

        internal Dictionary<string, object> ToDictionary()
        {
            return new Dictionary<string, object>
            {
                { "roots", AssetRoots.ToArray() },
                { "runtimeSourceRoots", RuntimeSourceRoots.ToArray() },
                { "excludePaths", ExcludePaths.ToArray() },
                { "requiredBuilderPreviews", RequiredBuilderPreviews.Select(requirement =>
                    new Dictionary<string, object>
                    {
                        { "path", requirement.Path },
                        { "elementName", requirement.ElementName },
                        { "minImages", requirement.MinImages },
                        { "minTextEntries", requirement.MinTextEntries }
                    }).ToArray() },
                {
                    "rules",
                    new Dictionary<string, object>
                    {
                        { "uxmlTooltipAttributes", UxmlTooltipAttributes }
                    }
                },
                {
                    "pixelGrid",
                    new Dictionary<string, object>
                    {
                        { "enabled", PixelGridEnabled },
                        { "step", PixelGridStep }
                    }
                }
            };
        }

        private static List<string> NormalizeRoots(IEnumerable<string> values, string fallback)
        {
            var result = (values ?? Enumerable.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(VmAutomationUIToolkitAuditUtility.NormalizeAssetPath)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList();
            if (result.Count == 0 && !string.IsNullOrEmpty(fallback))
                result.Add(fallback);
            return result;
        }

        private static bool IsAtOrBelow(string path, string root)
        {
            return string.Equals(path, root, StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith(root.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase);
        }
    }
}
#endif
