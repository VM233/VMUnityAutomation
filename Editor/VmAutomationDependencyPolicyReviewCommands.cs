#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationDependencyPolicyReviewCommands
    {
        private const int MaxMetaRecords = 250000;
        private static readonly Regex FullCommitRegex = new Regex(
            "^[0-9a-fA-F]{40}$", RegexOptions.Compiled);
        private static readonly Regex GuidRegex = new Regex(
            @"^guid:\s*(?<guid>[0-9a-fA-F]{32})\s*$",
            RegexOptions.Compiled | RegexOptions.Multiline);
        private static readonly Regex FolderAssetRegex = new Regex(
            @"^folderAsset:\s*yes\s*$",
            RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);

        public static object Review(Dictionary<string, object> args)
        {
            int maxIssues = Math.Max(1, Math.Min(GetInt(args, "maxIssues", 200), 5000));
            bool includeResolved = GetBool(args, "includeResolved", true);
            var report = new ReviewReport(maxIssues);
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string manifestPath = Path.Combine(projectRoot, "Packages", "manifest.json");
            string lockPath = Path.Combine(projectRoot, "Packages", "packages-lock.json");

            Dictionary<string, object> manifest = ReadJsonObject(manifestPath, report.Errors);
            Dictionary<string, object> packageLock = ReadJsonObject(lockPath, report.Errors);
            var manifestDependencies = ReadObject(manifest, "dependencies");
            var lockDependencies = ReadObject(packageLock, "dependencies");

            AuditDependencies(manifestDependencies, lockDependencies, includeResolved, report);
            List<string> embeddedRoots = FindEmbeddedPackageRoots(projectRoot);
            foreach (string embeddedRoot in embeddedRoots)
            {
                report.Record(new ReviewIssue("embedded-package", ToProjectPath(projectRoot,
                        embeddedRoot),
                    "Embedded packages under the project's Packages directory are forbidden. Use a full-SHA Git dependency."));
            }

            List<string> metaRoots = ResolveMetaRoots(args, projectRoot, embeddedRoots,
                report.Errors);
            int metaRecords = AuditMetas(projectRoot, metaRoots, report);

            return new Dictionary<string, object>
            {
                { "success", report.Errors.Count == 0 },
                { "passed", report.Errors.Count == 0 && report.IssueCount == 0 },
                { "manifestPath", ToProjectPath(projectRoot, manifestPath) },
                { "lockPath", ToProjectPath(projectRoot, lockPath) },
                { "dependencyCount", manifestDependencies.Count },
                { "embeddedPackageCount", embeddedRoots.Count },
                { "scannedMetaRecords", metaRecords },
                { "issueCount", report.IssueCount },
                { "truncated", report.Truncated },
                { "issues", report.Issues.Select(issue => issue.ToDictionary()).ToList() },
                { "errors", report.Errors.ToList() }
            };
        }

        private static void AuditDependencies(Dictionary<string, object> manifestDependencies,
            Dictionary<string, object> lockDependencies, bool includeResolved,
            ReviewReport report)
        {
            foreach (KeyValuePair<string, object> dependency in manifestDependencies
                         .OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                string name = dependency.Key;
                string identifier = dependency.Value?.ToString()?.Trim() ?? "";
                if (IsLocalIdentifier(identifier))
                {
                    report.Record(new ReviewIssue("local-dependency", name,
                        $"Dependency '{name}' uses forbidden local identifier '{identifier}'."));
                }

                bool isGit = IsGitIdentifier(identifier);
                string manifestRevision = GetGitRef(identifier);
                if (isGit && !FullCommitRegex.IsMatch(manifestRevision))
                {
                    report.Record(new ReviewIssue("git-full-sha", name,
                        $"Git dependency '{name}' must be pinned to a full 40-character commit SHA."));
                }

                if (!lockDependencies.TryGetValue(name, out object lockValue) ||
                    !(lockValue is Dictionary<string, object> lockEntry))
                {
                    report.Record(new ReviewIssue("missing-lock-entry", name,
                        $"Dependency '{name}' is missing from Packages/packages-lock.json."));
                    continue;
                }

                string source = ReadString(lockEntry, "source");
                string lockVersion = ReadString(lockEntry, "version");
                string lockHash = ReadString(lockEntry, "hash");
                if (string.Equals(source, "local", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(source, "embedded", StringComparison.OrdinalIgnoreCase) ||
                    IsLocalIdentifier(lockVersion))
                {
                    report.Record(new ReviewIssue("local-lock-source", name,
                        $"Lock entry '{name}' uses forbidden source '{source}' or local version '{lockVersion}'."));
                }

                if (!isGit || !FullCommitRegex.IsMatch(manifestRevision))
                    continue;
                if (!string.Equals(source, "git", StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(lockHash, manifestRevision,
                        StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(GetGitRef(lockVersion), manifestRevision,
                        StringComparison.OrdinalIgnoreCase))
                {
                    report.Record(new ReviewIssue("manifest-lock-revision", name,
                        $"Git dependency '{name}' manifest and lock revisions differ. Expected {manifestRevision}."));
                }

                if (!includeResolved)
                    continue;
                var expectation = new VmAutomationGitPackageExpectation(name, identifier,
                    manifestRevision);
                Dictionary<string, object> resolved =
                    VmAutomationPackageManagerCommands.BuildGitPackageResolutionState(expectation);
                string resolvedIdentifier = ReadString(resolved, "resolvedIdentifier");
                string resolvedPath = ReadString(resolved, "resolvedPath");
                string resolvedFingerprint = ReadString(resolved, "resolvedFingerprint");
                bool resolvedMatches = ResolvedGitPackageMatchesPolicy(identifier,
                    resolvedIdentifier, resolvedFingerprint, manifestRevision) &&
                    !string.IsNullOrWhiteSpace(resolvedPath) &&
                    Directory.Exists(resolvedPath);
                if ((!string.IsNullOrWhiteSpace(resolvedIdentifier) ||
                     !string.IsNullOrWhiteSpace(resolvedPath)) &&
                    !resolvedMatches)
                {
                    report.Record(new ReviewIssue("resolved-revision-mismatch", name,
                        $"Resolved package '{name}' does not match manifest revision {manifestRevision}."));
                }
            }

            foreach (KeyValuePair<string, object> dependency in lockDependencies)
            {
                if (!(dependency.Value is Dictionary<string, object> lockEntry))
                    continue;
                string source = ReadString(lockEntry, "source");
                if (string.Equals(source, "local", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(source, "embedded", StringComparison.OrdinalIgnoreCase))
                {
                    report.Record(new ReviewIssue("local-lock-source", dependency.Key,
                        $"Lock entry '{dependency.Key}' uses forbidden source '{source}'."));
                }
            }
        }

        private static int AuditMetas(string projectRoot, IReadOnlyCollection<string> roots,
            ReviewReport report)
        {
            int records = 0;
            var guidOwners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!Directory.Exists(root))
                    continue;
                foreach (string path in Directory.EnumerateFileSystemEntries(root, "*",
                             SearchOption.AllDirectories))
                {
                    if (ShouldSkip(path))
                        continue;
                    records++;
                    if (records > MaxMetaRecords)
                    {
                        report.Errors.Add($"Meta review exceeded the {MaxMetaRecords} record limit. Narrow metaRoots.");
                        return records;
                    }

                    bool isMeta = path.EndsWith(".meta", StringComparison.OrdinalIgnoreCase);
                    if (!isMeta)
                    {
                        string expectedMeta = path + ".meta";
                        if (!File.Exists(expectedMeta))
                        {
                            report.Record(new ReviewIssue(
                                Directory.Exists(path) ? "missing-folder-meta" : "missing-meta",
                                ToProjectPath(projectRoot, path),
                                $"Missing Unity meta file '{ToProjectPath(projectRoot, expectedMeta)}'."));
                        }
                        continue;
                    }

                    string target = path.Substring(0, path.Length - ".meta".Length);
                    if (!File.Exists(target) && !Directory.Exists(target))
                    {
                        report.Record(new ReviewIssue("orphan-meta",
                            ToProjectPath(projectRoot, path),
                            "Meta file has no matching asset or folder."));
                    }

                    string contents;
                    try
                    {
                        contents = File.ReadAllText(path);
                    }
                    catch (Exception exception)
                    {
                        report.Errors.Add($"Failed to read '{ToProjectPath(projectRoot, path)}': {exception.Message}");
                        continue;
                    }
                    Match guidMatch = GuidRegex.Match(contents);
                    if (!guidMatch.Success)
                    {
                        report.Record(new ReviewIssue("missing-meta-guid",
                            ToProjectPath(projectRoot, path),
                            "Meta file does not contain a valid 32-character GUID."));
                    }
                    else
                    {
                        string guid = guidMatch.Groups["guid"].Value;
                        string owner = ToProjectPath(projectRoot, path);
                        if (guidOwners.TryGetValue(guid, out string existing))
                        {
                            report.Record(new ReviewIssue("duplicate-meta-guid", owner,
                                $"Meta GUID {guid} is already owned by '{existing}'."));
                        }
                        else
                        {
                            guidOwners[guid] = owner;
                        }
                    }

                    if (Directory.Exists(target) && !FolderAssetRegex.IsMatch(contents))
                    {
                        report.Record(new ReviewIssue("folder-meta-marker",
                            ToProjectPath(projectRoot, path),
                            "Folder meta must declare 'folderAsset: yes'."));
                    }
                }
            }
            return records;
        }

        private static List<string> ResolveMetaRoots(Dictionary<string, object> args,
            string projectRoot, IEnumerable<string> embeddedRoots,
            ICollection<string> errors)
        {
            List<string> requested = GetStringList(args, "metaRoots");
            if (requested.Count == 0)
            {
                requested.Add("Assets");
                requested.AddRange(embeddedRoots.Select(path => ToProjectPath(projectRoot, path)));
            }

            var result = new List<string>();
            string rootPrefix = projectRoot.TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            foreach (string requestedRoot in requested)
            {
                string fullPath = Path.GetFullPath(Path.IsPathRooted(requestedRoot)
                    ? requestedRoot
                    : Path.Combine(projectRoot, requestedRoot));
                if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add($"Meta root is outside the Unity project: {requestedRoot}");
                    continue;
                }
                if (!Directory.Exists(fullPath))
                {
                    errors.Add($"Meta root does not exist: {requestedRoot}");
                    continue;
                }
                result.Add(fullPath);
            }
            return result;
        }

        private static List<string> FindEmbeddedPackageRoots(string projectRoot)
        {
            string packagesRoot = Path.Combine(projectRoot, "Packages");
            if (!Directory.Exists(packagesRoot))
                return new List<string>();
            return Directory.EnumerateDirectories(packagesRoot)
                .Where(path => File.Exists(Path.Combine(path, "package.json")))
                .OrderBy(path => path, StringComparer.Ordinal).ToList();
        }

        private static Dictionary<string, object> ReadJsonObject(string path,
            ICollection<string> errors)
        {
            if (!File.Exists(path))
            {
                errors.Add($"Required file does not exist: {path.Replace('\\', '/')}");
                return new Dictionary<string, object>();
            }
            try
            {
                return MiniJson.Deserialize(File.ReadAllText(path)) as
                           Dictionary<string, object> ?? new Dictionary<string, object>();
            }
            catch (Exception exception)
            {
                errors.Add($"Failed to parse '{path.Replace('\\', '/')}': {exception.Message}");
                return new Dictionary<string, object>();
            }
        }

        private static Dictionary<string, object> ReadObject(
            Dictionary<string, object> values, string key)
        {
            return values != null && values.TryGetValue(key, out object value) &&
                   value is Dictionary<string, object> result
                ? result
                : new Dictionary<string, object>();
        }

        private static string ReadString(Dictionary<string, object> values, string key)
        {
            return values != null && values.TryGetValue(key, out object value) && value != null
                ? value.ToString()
                : "";
        }

        private static bool IsGitIdentifier(string identifier)
        {
            return identifier.StartsWith("git+", StringComparison.OrdinalIgnoreCase) ||
                   identifier.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                   identifier.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                   identifier.StartsWith("ssh://", StringComparison.OrdinalIgnoreCase) ||
                   identifier.Contains(".git");
        }

        internal static bool IsLocalIdentifier(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier))
                return false;
            string value = identifier.Trim().Replace('\\', '/');
            return value.StartsWith("file:", StringComparison.OrdinalIgnoreCase) ||
                   value.StartsWith("./", StringComparison.Ordinal) ||
                   value.StartsWith("../", StringComparison.Ordinal) ||
                   value.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase) ||
                   Path.IsPathRooted(value);
        }

        internal static bool ResolvedGitPackageMatchesPolicy(string manifestIdentifier,
            string resolvedIdentifier, string resolvedFingerprint,
            string expectedRevision)
        {
            if (string.IsNullOrWhiteSpace(expectedRevision) ||
                !string.Equals(GetGitRef(resolvedIdentifier), expectedRevision,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string identifierWithoutRef = manifestIdentifier ?? "";
            int hashIndex = identifierWithoutRef.LastIndexOf('#');
            if (hashIndex >= 0)
                identifierWithoutRef = identifierWithoutRef.Substring(0, hashIndex);
            bool usesPackageSubpath = Regex.IsMatch(identifierWithoutRef,
                @"(?:\?|&)path=", RegexOptions.IgnoreCase);
            if (usesPackageSubpath)
                return !string.IsNullOrWhiteSpace(resolvedFingerprint);

            return string.Equals(resolvedFingerprint, expectedRevision,
                StringComparison.OrdinalIgnoreCase);
        }

        private static string GetGitRef(string identifier)
        {
            int hashIndex = (identifier ?? "").LastIndexOf('#');
            return hashIndex >= 0 && hashIndex < identifier.Length - 1
                ? identifier.Substring(hashIndex + 1)
                : "";
        }

        private static bool ShouldSkip(string path)
        {
            string normalized = path.Replace('\\', '/');
            return normalized.Split('/').Any(segment =>
                segment == ".git" || segment == "Library" || segment == "Temp" ||
                segment == "obj" || segment == "bin" || segment == "node_modules" ||
                segment.EndsWith("~", StringComparison.Ordinal));
        }

        private static string ToProjectPath(string projectRoot, string path)
        {
            string fullPath = Path.GetFullPath(path);
            if (!fullPath.StartsWith(projectRoot.TrimEnd('\\', '/') +
                                     Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
                return fullPath.Replace('\\', '/');
            return fullPath.Substring(projectRoot.TrimEnd('\\', '/').Length)
                .TrimStart('\\', '/').Replace('\\', '/');
        }

        private static string GetString(Dictionary<string, object> args, string key)
        {
            return args != null && args.TryGetValue(key, out object value) && value != null
                ? value.ToString()
                : "";
        }

        private static bool GetBool(Dictionary<string, object> args, string key,
            bool defaultValue)
        {
            string value = GetString(args, key);
            return string.IsNullOrEmpty(value) ? defaultValue :
                bool.TryParse(value, out bool parsed) ? parsed : defaultValue;
        }

        private static int GetInt(Dictionary<string, object> args, string key,
            int defaultValue)
        {
            string value = GetString(args, key);
            return int.TryParse(value, out int parsed) ? parsed : defaultValue;
        }

        private static List<string> GetStringList(Dictionary<string, object> args,
            string key)
        {
            var result = new List<string>();
            if (args == null || !args.TryGetValue(key, out object raw) || raw == null)
                return result;
            if (raw is string single)
            {
                if (!string.IsNullOrWhiteSpace(single))
                    result.Add(single.Trim());
                return result;
            }
            if (raw is IEnumerable values)
            {
                foreach (object value in values)
                {
                    if (value != null && !string.IsNullOrWhiteSpace(value.ToString()))
                        result.Add(value.ToString().Trim());
                }
            }
            return result;
        }

        private sealed class ReviewReport
        {
            private readonly int maxIssues;
            internal readonly List<ReviewIssue> Issues = new List<ReviewIssue>();
            internal readonly List<string> Errors = new List<string>();
            internal int IssueCount { get; private set; }
            internal bool Truncated { get; private set; }

            internal ReviewReport(int maxIssues)
            {
                this.maxIssues = maxIssues;
            }

            internal void Record(ReviewIssue issue)
            {
                IssueCount++;
                if (Issues.Count < maxIssues)
                    Issues.Add(issue);
                else
                    Truncated = true;
            }
        }

        private sealed class ReviewIssue
        {
            private readonly string rule;
            private readonly string subject;
            private readonly string message;

            internal ReviewIssue(string rule, string subject, string message)
            {
                this.rule = rule;
                this.subject = subject;
                this.message = message;
            }

            internal Dictionary<string, object> ToDictionary()
            {
                return new Dictionary<string, object>
                {
                    { "rule", rule },
                    { "severity", "error" },
                    { "subject", subject },
                    { "message", message }
                };
            }
        }
    }
}
#endif
