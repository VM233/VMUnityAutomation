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
    [InitializeOnLoad]
    internal static class VmAutomationUIToolkitAutomaticAuditCoordinator
    {
        private static readonly HashSet<string> PendingUss =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> PendingUxml =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> PendingUiPrefabs =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static bool pendingStyleGraphChange;
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

        static VmAutomationUIToolkitAutomaticAuditCoordinator()
        {
            if (!VmAutomationEditorProcess.OwnsAutomationState) return;
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
            var settings = VmAutomationUIToolkitAuditProjectSettings.Load();
            if (!settings.Valid)
                return;

            var options = VmAutomationUIToolkitAuditOptions.FromProjectSettings(settings);
            foreach (string assetPath in assetPaths ?? Enumerable.Empty<string>())
                QueuePath(assetPath, settings, options);
        }

        internal static Dictionary<string, object> GetStatus(string extension)
        {
            var settings = VmAutomationUIToolkitAuditProjectSettings.Load();
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
                { "configPath", VmAutomationUIToolkitAuditProjectSettings.ConfigPath },
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
                PendingUiPrefabs.Clear();
                pendingStyleGraphChange = false;
                DrainFileSystemQueue();
                return;
            }

            if (PendingUss.Count == 0 && PendingUxml.Count == 0 &&
                PendingUiPrefabs.Count == 0 && !pendingStyleGraphChange &&
                FileSystemChanges.IsEmpty)
                return;

            var settings = VmAutomationUIToolkitAuditProjectSettings.Load();
            if (!settings.Valid)
                return;
            var options = VmAutomationUIToolkitAuditOptions.FromProjectSettings(settings);
            string changedPath;
            while (FileSystemChanges.TryDequeue(out changedPath))
                QueuePath(changedPath, settings, options);

            if (PendingUss.Count == 0 && PendingUxml.Count == 0 &&
                PendingUiPrefabs.Count == 0 &&
                !pendingStyleGraphChange)
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
            {
                PendingUxml.Clear();
                PendingUiPrefabs.Clear();
                pendingStyleGraphChange = false;
            }
        }

        private static void QueuePath(string assetPath,
            VmAutomationUIToolkitAuditProjectSettings settings, VmAutomationUIToolkitAuditOptions options)
        {
            string normalized = VmAutomationUIToolkitAuditUtility.NormalizeAssetPath(assetPath);
            if (!options.Includes(normalized))
                return;

            bool queued = false;
            if (normalized.EndsWith(".uss", StringComparison.OrdinalIgnoreCase))
            {
                if (settings.AutomaticUssSingleUseStyles)
                    PendingUss.Add(normalized);
                if (settings.AutomaticUxmlLayoutContracts)
                    pendingStyleGraphChange = true;
                queued = settings.AutomaticUssSingleUseStyles ||
                         settings.AutomaticUxmlLayoutContracts;
            }
            else if (settings.AutomaticUxmlLayoutContracts &&
                     normalized.EndsWith(".uxml", StringComparison.OrdinalIgnoreCase))
            {
                PendingUxml.Add(normalized);
                queued = true;
            }
            else if (settings.AutomaticUxmlLayoutContracts &&
                     normalized.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                PendingUiPrefabs.Add(normalized);
                queued = true;
            }
            else if (settings.AutomaticUxmlLayoutContracts &&
                     normalized.EndsWith(".tss", StringComparison.OrdinalIgnoreCase))
            {
                pendingStyleGraphChange = true;
                queued = true;
            }

            if (queued)
                auditNotBefore = EditorApplication.timeSinceStartup + 0.35d;
        }

        private static void AuditPendingUss(VmAutomationUIToolkitAuditOptions options)
        {
            string[] paths = TakeChangedPaths(PendingUss);
            if (paths.Length == 0)
                return;

            VmAutomationUssStyleAuditReport report =
                VmAutomationUssStyleAuditor.Audit(paths, false, 5000, options);
            UssState.Record(paths, report.WarningCount,
                report.ErrorCount + report.Errors.Count);
            VmAutomationUssStyleAuditConsoleReporter.Log(report, true);
        }

        private static void AuditPendingUxml(VmAutomationUIToolkitAuditOptions options)
        {
            string[] changedUxmlPaths = TakeChangedPaths(PendingUxml);
            string[] changedPrefabPaths = TakeChangedPaths(PendingUiPrefabs);
            bool styleGraphChanged = pendingStyleGraphChange;
            pendingStyleGraphChange = false;
            if (changedUxmlPaths.Length == 0 && changedPrefabPaths.Length == 0 &&
                !styleGraphChanged)
                return;

            string[] layoutPaths = changedPrefabPaths.Length > 0
                ? VmAutomationUIToolkitAuditUtility.FindAssetFiles(".uxml", options).ToArray()
                : changedUxmlPaths;
            VmAutomationUxmlLayoutAuditReport report = layoutPaths.Length > 0
                ? VmAutomationUxmlLayoutAuditor.Audit(layoutPaths,
                    false, 5000, options)
                : new VmAutomationUxmlLayoutAuditReport(5000);
            string[] auditedPaths = layoutPaths;
            if (styleGraphChanged)
            {
                string[] graphOnlyPaths = VmAutomationUIToolkitAuditUtility
                    .FindAssetFiles(".uxml", options)
                    .Except(layoutPaths, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                VmAutomationUxmlThemeStyleAuditor.AuditProject(graphOnlyPaths, report);
                report.ScannedUxmlCount += graphOnlyPaths.Length;
                report.IndexedUxmlCount = Math.Max(report.IndexedUxmlCount,
                    graphOnlyPaths.Length + layoutPaths.Length);
                report.SortIssues();
                auditedPaths = layoutPaths.Concat(graphOnlyPaths).ToArray();
            }

            UxmlState.Record(auditedPaths, report.WarningCount,
                report.ErrorCount + report.Errors.Count);
            VmAutomationUxmlLayoutAuditConsoleReporter.Log(report, true);
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
            string fullPath = VmAutomationUIToolkitAuditUtility.ToFullPath(assetPath);
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
            var settings = VmAutomationUIToolkitAuditProjectSettings.Load();
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
                 !normalized.EndsWith(".uxml", StringComparison.OrdinalIgnoreCase) &&
                 !normalized.EndsWith(".tss", StringComparison.OrdinalIgnoreCase) &&
                 !normalized.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)))
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
}
#endif
