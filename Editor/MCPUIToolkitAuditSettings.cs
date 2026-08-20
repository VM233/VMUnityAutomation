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
    internal sealed class MCPUIToolkitAuditProjectSettings
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

        internal static MCPUIToolkitAuditProjectSettings Load()
        {
            var settings = new MCPUIToolkitAuditProjectSettings();
            string fullPath = Path.Combine(MCPUIToolkitAuditUtility.GetProjectRoot(),
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
                    MCPUIToolkitAuditUtility.GetBool(automatic, "ussSingleUseStyles", false);
                settings.AutomaticUxmlLayoutContracts =
                    MCPUIToolkitAuditUtility.GetBool(automatic, "uxmlLayoutContracts", false);

                Dictionary<string, object> rules = GetDictionary(values, "rules");
                settings.UxmlTooltipAttributes =
                    MCPUIToolkitAuditUtility.GetBool(rules, "uxmlTooltipAttributes", true);

                Dictionary<string, object> pixelGrid = GetDictionary(values, "pixelGrid");
                settings.PixelGridEnabled =
                    MCPUIToolkitAuditUtility.GetBool(pixelGrid, "enabled", false);
                settings.PixelGridStep =
                    MCPUIToolkitAuditUtility.GetInt(pixelGrid, "step", 3);
                if (settings.PixelGridStep <= 0)
                    throw new InvalidDataException("pixelGrid.step must be a positive integer.");

                ReplaceListWhenPresent(values, "assetRoots", settings.AssetRoots);
                ReplaceListWhenPresent(values, "runtimeSourceRoots", settings.RuntimeSourceRoots);
                ReplaceListWhenPresent(values, "excludePaths", settings.ExcludePaths);
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
            string fullPath = Path.Combine(MCPUIToolkitAuditUtility.GetProjectRoot(),
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
                excludePaths = ExcludePaths.ToArray()
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
            foreach (string value in MCPUIToolkitAuditUtility.GetStringList(values, key))
                target.Add(value);
        }

        private static void NormalizeList(IList<string> values)
        {
            var normalized = values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(MCPUIToolkitAuditUtility.NormalizeAssetPath)
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

    [InitializeOnLoad]
    internal static class MCPUIToolkitAutomaticAuditCoordinator
    {
        private static readonly HashSet<string> PendingUss =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> PendingUxml =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentQueue<string> FileSystemChanges =
            new ConcurrentQueue<string>();
        private static readonly Dictionary<string, string> LastFingerprints =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly AutomaticAuditState UssState = new AutomaticAuditState();
        private static readonly AutomaticAuditState UxmlState = new AutomaticAuditState();

        private static readonly string AssetsFullPath =
            Path.GetFullPath(Application.dataPath).Replace('\\', '/');

        private static FileSystemWatcher watcher;
        private static double auditNotBefore;
        private static double settingsCheckNotBefore;
        private static bool automaticEnabled;

        static MCPUIToolkitAutomaticAuditCoordinator()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
            AssemblyReloadEvents.beforeAssemblyReload -= DisposeWatcher;
            AssemblyReloadEvents.beforeAssemblyReload += DisposeWatcher;
            EditorApplication.quitting -= DisposeWatcher;
            EditorApplication.quitting += DisposeWatcher;
            EnsureWatcherState(true);
        }

        internal static void QueueImportedAssets(IEnumerable<string> assetPaths)
        {
            var settings = MCPUIToolkitAuditProjectSettings.Load();
            if (!settings.Valid)
                return;

            var options = MCPUIToolkitAuditOptions.FromProjectSettings(settings);
            foreach (string assetPath in assetPaths ?? Enumerable.Empty<string>())
                QueuePath(assetPath, settings, options);
        }

        internal static Dictionary<string, object> GetStatus(string extension)
        {
            var settings = MCPUIToolkitAuditProjectSettings.Load();
            bool uss = string.Equals(extension, ".uss", StringComparison.OrdinalIgnoreCase);
            bool enabled = settings.Valid &&
                           (uss
                               ? settings.AutomaticUssSingleUseStyles
                               : settings.AutomaticUxmlLayoutContracts);
            AutomaticAuditState state = uss ? UssState : UxmlState;
            return new Dictionary<string, object>
            {
                { "enabled", enabled },
                { "watcherActive", watcher != null && watcher.EnableRaisingEvents },
                { "runCount", state.RunCount },
                { "lastRunAt", state.LastRunAt },
                { "lastPaths", state.LastPaths },
                { "lastWarningCount", state.LastWarningCount },
                { "lastErrorCount", state.LastErrorCount },
                { "configPath", MCPUIToolkitAuditProjectSettings.ConfigPath },
                { "configFound", settings.Found },
                { "configValid", settings.Valid },
                { "configError", settings.Error }
            };
        }

        private static void OnEditorUpdate()
        {
            EnsureWatcherState(false);
            if (!automaticEnabled)
            {
                PendingUss.Clear();
                PendingUxml.Clear();
                DrainFileSystemQueue();
                return;
            }

            var settings = MCPUIToolkitAuditProjectSettings.Load();
            if (!settings.Valid)
                return;
            var options = MCPUIToolkitAuditOptions.FromProjectSettings(settings);
            string changedPath;
            while (FileSystemChanges.TryDequeue(out changedPath))
                QueuePath(changedPath, settings, options);

            if (PendingUss.Count == 0 && PendingUxml.Count == 0)
                return;

            if (EditorApplication.timeSinceStartup < auditNotBefore ||
                EditorApplication.isCompiling ||
                EditorApplication.isUpdating)
                return;

            if (settings.AutomaticUssSingleUseStyles)
                AuditPendingUss(options);
            else
                PendingUss.Clear();

            if (settings.AutomaticUxmlLayoutContracts)
                AuditPendingUxml(options);
            else
                PendingUxml.Clear();
        }

        private static void QueuePath(string assetPath,
            MCPUIToolkitAuditProjectSettings settings, MCPUIToolkitAuditOptions options)
        {
            string normalized = MCPUIToolkitAuditUtility.NormalizeAssetPath(assetPath);
            if (!options.Includes(normalized))
                return;

            bool queued = false;
            if (settings.AutomaticUssSingleUseStyles &&
                normalized.EndsWith(".uss", StringComparison.OrdinalIgnoreCase))
            {
                PendingUss.Add(normalized);
                queued = true;
            }
            else if (settings.AutomaticUxmlLayoutContracts &&
                     normalized.EndsWith(".uxml", StringComparison.OrdinalIgnoreCase))
            {
                PendingUxml.Add(normalized);
                queued = true;
            }

            if (queued)
                auditNotBefore = EditorApplication.timeSinceStartup + 0.35d;
        }

        private static void AuditPendingUss(MCPUIToolkitAuditOptions options)
        {
            string[] paths = TakeChangedPaths(PendingUss);
            if (paths.Length == 0)
                return;

            MCPUssStyleAuditReport report =
                MCPUssStyleAuditor.Audit(paths, false, 5000, options);
            UssState.Record(paths, report.WarningCount,
                report.ErrorCount + report.Errors.Count);
            MCPUssStyleAuditConsoleReporter.Log(report, true);
        }

        private static void AuditPendingUxml(MCPUIToolkitAuditOptions options)
        {
            string[] paths = TakeChangedPaths(PendingUxml);
            if (paths.Length == 0)
                return;

            MCPUxmlLayoutAuditReport report =
                MCPUxmlLayoutAuditor.Audit(paths, false, 5000, options);
            UxmlState.Record(paths, report.WarningCount, report.Errors.Count);
            MCPUxmlLayoutAuditConsoleReporter.Log(report, true);
        }

        private static string[] TakeChangedPaths(ICollection<string> pending)
        {
            string[] paths = pending
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            pending.Clear();
            return paths.Where(HasChangedSinceLastAudit).ToArray();
        }

        private static bool HasChangedSinceLastAudit(string assetPath)
        {
            string fullPath = MCPUIToolkitAuditUtility.ToFullPath(assetPath);
            if (!File.Exists(fullPath))
            {
                LastFingerprints.Remove(assetPath);
                return false;
            }

            string fingerprint;
            using (SHA256 sha256 = SHA256.Create())
            {
                fingerprint = BitConverter.ToString(
                        sha256.ComputeHash(File.ReadAllBytes(fullPath)))
                    .Replace("-", "");
            }

            string previous;
            if (LastFingerprints.TryGetValue(assetPath, out previous) &&
                string.Equals(previous, fingerprint, StringComparison.Ordinal))
                return false;

            LastFingerprints[assetPath] = fingerprint;
            return true;
        }

        private static void EnsureWatcherState(bool force)
        {
            if (!force && EditorApplication.timeSinceStartup < settingsCheckNotBefore)
                return;

            settingsCheckNotBefore = EditorApplication.timeSinceStartup + 1d;
            var settings = MCPUIToolkitAuditProjectSettings.Load();
            bool enabled = settings.Valid &&
                           (settings.AutomaticUssSingleUseStyles ||
                            settings.AutomaticUxmlLayoutContracts);
            if (enabled == automaticEnabled && (!enabled || watcher != null))
                return;

            automaticEnabled = enabled;
            if (automaticEnabled)
                StartWatcher();
            else
                DisposeWatcher();
        }

        private static void StartWatcher()
        {
            DisposeWatcher();
            if (!Directory.Exists(AssetsFullPath))
                return;

            try
            {
                watcher = new FileSystemWatcher(AssetsFullPath)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite |
                                   NotifyFilters.CreationTime,
                    EnableRaisingEvents = true
                };
                watcher.Changed += OnFileChanged;
                watcher.Created += OnFileChanged;
                watcher.Renamed += OnFileRenamed;
            }
            catch (Exception exception)
            {
                DisposeWatcher();
                Debug.LogError("[UI Toolkit Static Audit] Failed to start automatic file watcher: " +
                               exception.Message);
            }
        }

        private static void OnFileChanged(object sender, FileSystemEventArgs args)
        {
            EnqueueFullPath(args.FullPath);
        }

        private static void OnFileRenamed(object sender, RenamedEventArgs args)
        {
            EnqueueFullPath(args.FullPath);
        }

        private static void EnqueueFullPath(string fullPath)
        {
            string normalized = Path.GetFullPath(fullPath ?? "").Replace('\\', '/');
            if (!normalized.StartsWith(AssetsFullPath + "/",
                    StringComparison.OrdinalIgnoreCase) ||
                (!normalized.EndsWith(".uss", StringComparison.OrdinalIgnoreCase) &&
                 !normalized.EndsWith(".uxml", StringComparison.OrdinalIgnoreCase)))
                return;

            FileSystemChanges.Enqueue(
                "Assets/" + normalized.Substring(AssetsFullPath.Length + 1));
        }

        private static void DisposeWatcher()
        {
            if (watcher == null)
                return;

            watcher.EnableRaisingEvents = false;
            watcher.Changed -= OnFileChanged;
            watcher.Created -= OnFileChanged;
            watcher.Renamed -= OnFileRenamed;
            watcher.Dispose();
            watcher = null;
        }

        private static void DrainFileSystemQueue()
        {
            string ignored;
            while (FileSystemChanges.TryDequeue(out ignored))
            {
            }
        }

        private sealed class AutomaticAuditState
        {
            internal int RunCount;
            internal string LastRunAt = "";
            internal string[] LastPaths = new string[0];
            internal int LastWarningCount;
            internal int LastErrorCount;

            internal void Record(string[] paths, int warningCount, int errorCount)
            {
                RunCount++;
                LastRunAt = DateTime.UtcNow.ToString("O");
                LastPaths = paths;
                LastWarningCount = warningCount;
                LastErrorCount = errorCount;
            }
        }
    }

    internal sealed class MCPUIToolkitAuditPostprocessor : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            MCPUIToolkitAutomaticAuditCoordinator.QueueImportedAssets(
                (importedAssets ?? new string[0]).Concat(movedAssets ?? new string[0]));
        }
    }

    internal sealed class MCPUIToolkitAuditOptions
    {
        internal readonly List<string> AssetRoots;
        internal readonly List<string> RuntimeSourceRoots;
        internal readonly List<string> ExcludePaths;
        internal readonly bool UxmlTooltipAttributes;
        internal readonly bool PixelGridEnabled;
        internal readonly int PixelGridStep;

        private MCPUIToolkitAuditOptions(IEnumerable<string> assetRoots,
            IEnumerable<string> runtimeSourceRoots, IEnumerable<string> excludePaths,
            bool uxmlTooltipAttributes, bool pixelGridEnabled, int pixelGridStep)
        {
            AssetRoots = NormalizeRoots(assetRoots, "Assets");
            RuntimeSourceRoots = NormalizeRoots(runtimeSourceRoots, "Assets");
            ExcludePaths = NormalizeRoots(excludePaths, null);
            UxmlTooltipAttributes = uxmlTooltipAttributes;
            PixelGridEnabled = pixelGridEnabled;
            PixelGridStep = Math.Max(1, pixelGridStep);
        }

        internal static MCPUIToolkitAuditOptions FromArguments(Dictionary<string, object> args)
        {
            args = args ?? new Dictionary<string, object>();
            bool useProjectSettings =
                MCPUIToolkitAuditUtility.GetBool(args, "useProjectSettings", true);
            MCPUIToolkitAuditProjectSettings settings = useProjectSettings
                ? MCPUIToolkitAuditProjectSettings.Load()
                : new MCPUIToolkitAuditProjectSettings();

            IEnumerable<string> assetRoots = args.ContainsKey("roots")
                ? MCPUIToolkitAuditUtility.GetStringList(args, "roots")
                : settings.AssetRoots;
            IEnumerable<string> runtimeRoots = args.ContainsKey("runtimeSourceRoots")
                ? MCPUIToolkitAuditUtility.GetStringList(args, "runtimeSourceRoots")
                : settings.RuntimeSourceRoots;
            IEnumerable<string> excludePaths = args.ContainsKey("excludePaths")
                ? MCPUIToolkitAuditUtility.GetStringList(args, "excludePaths")
                : settings.ExcludePaths;
            bool pixelGridEnabled = args.ContainsKey("pixelGridEnabled")
                ? MCPUIToolkitAuditUtility.GetBool(args, "pixelGridEnabled")
                : settings.PixelGridEnabled;
            int pixelGridStep = args.ContainsKey("pixelGridStep")
                ? MCPUIToolkitAuditUtility.GetInt(args, "pixelGridStep",
                    settings.PixelGridStep)
                : settings.PixelGridStep;

            return new MCPUIToolkitAuditOptions(assetRoots, runtimeRoots, excludePaths,
                settings.UxmlTooltipAttributes, pixelGridEnabled, pixelGridStep);
        }

        internal static MCPUIToolkitAuditOptions FromProjectSettings(
            MCPUIToolkitAuditProjectSettings settings)
        {
            settings = settings ?? MCPUIToolkitAuditProjectSettings.Load();
            return new MCPUIToolkitAuditOptions(settings.AssetRoots,
                settings.RuntimeSourceRoots, settings.ExcludePaths,
                settings.UxmlTooltipAttributes, settings.PixelGridEnabled,
                settings.PixelGridStep);
        }

        internal bool Includes(string assetPath)
        {
            string normalized = MCPUIToolkitAuditUtility.NormalizeAssetPath(assetPath);
            return AssetRoots.Any(root => IsAtOrBelow(normalized, root)) &&
                   !ExcludePaths.Any(excluded => IsAtOrBelow(normalized, excluded));
        }

        internal bool IncludesRuntimeSource(string assetPath)
        {
            string normalized = MCPUIToolkitAuditUtility.NormalizeAssetPath(assetPath);
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
                .Select(MCPUIToolkitAuditUtility.NormalizeAssetPath)
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

    internal static class MCPUIToolkitAuditUtility
    {
        internal static string GetProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        internal static List<string> GetStringList(IDictionary<string, object> args, string key)
        {
            object value;
            if (args == null || !args.TryGetValue(key, out value) || value == null)
                return new List<string>();

            var text = value as string;
            if (text != null)
                return new List<string> { text };

            var result = new List<string>();
            var values = value as IEnumerable;
            if (values == null)
                return result;
            foreach (object item in values)
            {
                if (item != null)
                    result.Add(item.ToString());
            }
            return result;
        }

        internal static bool GetBool(IDictionary<string, object> args, string key,
            bool defaultValue = false)
        {
            object value;
            if (args == null || !args.TryGetValue(key, out value) || value == null)
                return defaultValue;
            if (value is bool)
                return (bool)value;
            bool parsed;
            return bool.TryParse(value.ToString(), out parsed) ? parsed : defaultValue;
        }

        internal static int GetInt(IDictionary<string, object> args, string key, int defaultValue)
        {
            object value;
            if (args == null || !args.TryGetValue(key, out value) || value == null)
                return defaultValue;
            try
            {
                return Convert.ToInt32(value);
            }
            catch
            {
                return defaultValue;
            }
        }

        internal static string NormalizeAssetPath(string path)
        {
            path = (path ?? "").Trim().Replace('\\', '/');
            if (string.IsNullOrEmpty(path))
                return "";

            if (Path.IsPathRooted(path))
            {
                string fullPath = Path.GetFullPath(path).Replace('\\', '/');
                string projectRoot = GetProjectRoot().Replace('\\', '/').TrimEnd('/');
                if (fullPath.StartsWith(projectRoot + "/", StringComparison.OrdinalIgnoreCase))
                    path = fullPath.Substring(projectRoot.Length + 1);
                else
                    return fullPath;
            }

            while (path.StartsWith("./", StringComparison.Ordinal))
                path = path.Substring(2);
            return path.Trim('/');
        }

        internal static string ToFullPath(string assetPath)
        {
            string normalized = NormalizeAssetPath(assetPath);
            if (Path.IsPathRooted(normalized))
                return Path.GetFullPath(normalized);
            return Path.GetFullPath(Path.Combine(GetProjectRoot(),
                normalized.Replace('/', Path.DirectorySeparatorChar)));
        }

        internal static string ToAssetPath(string fullPath)
        {
            string normalizedFullPath = Path.GetFullPath(fullPath).Replace('\\', '/');
            string projectRoot = GetProjectRoot().Replace('\\', '/').TrimEnd('/');
            return normalizedFullPath.StartsWith(projectRoot + "/", StringComparison.OrdinalIgnoreCase)
                ? normalizedFullPath.Substring(projectRoot.Length + 1)
                : normalizedFullPath;
        }

        internal static IEnumerable<string> FindAssetFiles(string extension,
            MCPUIToolkitAuditOptions options)
        {
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string root in options.AssetRoots)
            {
                string fullRoot = ToFullPath(root);
                if (!Directory.Exists(fullRoot))
                    continue;

                foreach (string fullPath in Directory.EnumerateFiles(fullRoot, "*" + extension,
                             SearchOption.AllDirectories))
                {
                    string assetPath = ToAssetPath(fullPath);
                    if (options.Includes(assetPath))
                        paths.Add(assetPath);
                }
            }
            return paths.OrderBy(path => path, StringComparer.Ordinal);
        }

        internal static IEnumerable<string> FindRuntimeSourceFiles(
            MCPUIToolkitAuditOptions options)
        {
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string root in options.RuntimeSourceRoots)
            {
                string fullRoot = ToFullPath(root);
                if (!Directory.Exists(fullRoot))
                    continue;

                foreach (string fullPath in Directory.EnumerateFiles(fullRoot, "*.cs",
                             SearchOption.AllDirectories))
                {
                    string assetPath = ToAssetPath(fullPath);
                    if (options.IncludesRuntimeSource(assetPath))
                        paths.Add(assetPath);
                }
            }
            return paths.OrderBy(path => path, StringComparer.Ordinal);
        }
    }
}
#endif
