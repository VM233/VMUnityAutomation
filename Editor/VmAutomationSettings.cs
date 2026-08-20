using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    /// <summary>
    /// Transport-neutral defaults consumed by automation owners. Explicit command
    /// arguments always win; destructive choices and hard safety caps are not configurable.
    /// </summary>
    public static class VmAutomationSettings
    {
        private const string GlobalUserPrefix = "VMUnityAutomation_user_v1_";
        private static string s_ProjectUserPrefix;
        private static VmAutomationProjectConfiguration s_ProjectConfiguration;
        private static bool s_ProjectConfigurationFileExists;
        private static long s_ProjectConfigurationWriteTicks;

        private static string ProjectUserPrefix
        {
            get
            {
                if (s_ProjectUserPrefix != null)
                    return s_ProjectUserPrefix;

                string guid = PlayerSettings.productGUID.ToString("N");
                if (string.IsNullOrEmpty(guid) ||
                    guid == "00000000000000000000000000000000")
                {
                    guid = "path" + StableHash(GetProjectPath());
                }

                s_ProjectUserPrefix = $"VMUnityAutomation_project_{guid}_";
                return s_ProjectUserPrefix;
            }
        }

        public static bool OverrideDefaultResultLimit
        {
            get => EditorPrefs.GetBool(
                GlobalUserPrefix + "OverrideDefaultResultLimit", false);
            set => EditorPrefs.SetBool(
                GlobalUserPrefix + "OverrideDefaultResultLimit", value);
        }

        public static int DefaultResultLimit
        {
            get => Math.Max(1, Math.Min(500, EditorPrefs.GetInt(
                GlobalUserPrefix + "DefaultResultLimit", 100)));
            set => EditorPrefs.SetInt(
                GlobalUserPrefix + "DefaultResultLimit",
                Math.Max(1, Math.Min(500, value)));
        }

        public static bool IncludePrefabFileDiffByDefault
        {
            get => EditorPrefs.GetBool(
                GlobalUserPrefix + "IncludePrefabFileDiffByDefault", false);
            set => EditorPrefs.SetBool(
                GlobalUserPrefix + "IncludePrefabFileDiffByDefault", value);
        }

        public static bool ActionHistoryPersistence
        {
            get => EditorPrefs.GetBool(
                ProjectUserPrefix + "ActionHistoryPersistence", false);
            set => EditorPrefs.SetBool(
                ProjectUserPrefix + "ActionHistoryPersistence", value);
        }

        public static int ActionHistoryMaxEntries
        {
            get => Math.Max(1, Math.Min(10000, EditorPrefs.GetInt(
                ProjectUserPrefix + "ActionHistoryMaxEntries", 500)));
            set => EditorPrefs.SetInt(
                ProjectUserPrefix + "ActionHistoryMaxEntries",
                Math.Max(1, Math.Min(10000, value)));
        }

        public static int JobHistoryMaxEntries
        {
            get => Math.Max(20, Math.Min(2000, EditorPrefs.GetInt(
                GlobalUserPrefix + "JobHistoryMaxEntries", 200)));
            set => EditorPrefs.SetInt(
                GlobalUserPrefix + "JobHistoryMaxEntries",
                Math.Max(20, Math.Min(2000, value)));
        }

        public static string DefaultPhysicsDimension
        {
            get
            {
                VmAutomationProjectConfiguration settings = GetProjectConfiguration();
                return settings.Found && settings.Valid
                    ? settings.PhysicsDimension
                    : VmAutomationProjectConfiguration.DefaultPhysicsDimension;
            }
            set => UpdateProjectConfiguration(settings =>
                settings.PhysicsDimension = value);
        }

        public static string ScreenshotOutputDirectory
        {
            get
            {
                VmAutomationProjectConfiguration settings = GetProjectConfiguration();
                return settings.Found && settings.Valid
                    ? settings.ScreenshotDirectory
                    : VmAutomationProjectConfiguration.DefaultScreenshotDirectory;
            }
            set => UpdateProjectConfiguration(settings =>
                settings.ScreenshotDirectory = value);
        }

        public static int ResolvePrimaryResultLimit(
            IDictionary<string, object> arguments,
            string argumentName,
            int builtInDefault,
            int minimum,
            int maximum)
        {
            if (string.IsNullOrWhiteSpace(argumentName))
                throw new ArgumentException("argumentName is required.", nameof(argumentName));
            if (minimum > maximum)
            {
                throw new ArgumentOutOfRangeException(nameof(minimum),
                    "minimum cannot be greater than maximum.");
            }

            int value;
            if (arguments != null &&
                arguments.TryGetValue(argumentName, out object explicitValue) &&
                explicitValue != null)
            {
                value = Convert.ToInt32(explicitValue);
            }
            else
            {
                value = OverrideDefaultResultLimit
                    ? DefaultResultLimit
                    : builtInDefault;
            }

            return Math.Max(minimum, Math.Min(maximum, value));
        }

        public static string CreateDefaultScreenshotPath(string prefix)
        {
            string safePrefix = string.IsNullOrWhiteSpace(prefix)
                ? "Capture"
                : new string(prefix.Where(character =>
                    char.IsLetterOrDigit(character) ||
                    character == '-' ||
                    character == '_').ToArray());
            if (string.IsNullOrEmpty(safePrefix))
                safePrefix = "Capture";

            return ScreenshotOutputDirectory.TrimEnd('/') + "/" +
                   safePrefix + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") +
                   ".png";
        }

        public static IReadOnlyList<string> GetExecuteCodeAdditionalNamespaces()
        {
            VmAutomationProjectConfiguration settings = GetProjectConfiguration();
            return settings.Found && settings.Valid
                ? settings.ExecuteCodeAdditionalNamespaces.ToArray()
                : Array.Empty<string>();
        }

        internal static VmAutomationProjectConfiguration GetProjectConfiguration()
        {
            string path = VmAutomationProjectConfiguration.GetFullPath();
            bool exists = File.Exists(path);
            long ticks = exists ? File.GetLastWriteTimeUtc(path).Ticks : 0L;
            if (s_ProjectConfiguration == null ||
                exists != s_ProjectConfigurationFileExists ||
                ticks != s_ProjectConfigurationWriteTicks)
            {
                s_ProjectConfiguration = VmAutomationProjectConfiguration.Load();
                CacheProjectConfiguration(s_ProjectConfiguration);
            }

            return s_ProjectConfiguration;
        }

        private static void UpdateProjectConfiguration(
            Action<VmAutomationProjectConfiguration> update)
        {
            VmAutomationProjectConfiguration settings = GetProjectConfiguration();
            if (!settings.Valid)
            {
                throw new InvalidOperationException(
                    $"{VmAutomationProjectConfiguration.ConfigPath}: {settings.Error}");
            }

            update(settings);
            settings.Save();
            CacheProjectConfiguration(settings);
        }

        private static void CacheProjectConfiguration(
            VmAutomationProjectConfiguration settings)
        {
            s_ProjectConfiguration = settings;
            string path = VmAutomationProjectConfiguration.GetFullPath();
            s_ProjectConfigurationFileExists = File.Exists(path);
            s_ProjectConfigurationWriteTicks = s_ProjectConfigurationFileExists
                ? File.GetLastWriteTimeUtc(path).Ticks
                : 0L;
        }

        private static string GetProjectPath()
        {
            string dataPath = Application.dataPath.Replace('\\', '/');
            string projectPath = dataPath.EndsWith(
                "/Assets", StringComparison.OrdinalIgnoreCase)
                ? dataPath.Substring(0, dataPath.Length - "/Assets".Length)
                : dataPath;
            return Path.GetFullPath(projectPath).Replace('\\', '/').TrimEnd('/');
        }

        private static string StableHash(string value)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(value ?? ""));
                var builder = new StringBuilder(16);
                for (int index = 0; index < 8; index++)
                    builder.Append(hash[index].ToString("x2"));
                return builder.ToString();
            }
        }
    }
}
