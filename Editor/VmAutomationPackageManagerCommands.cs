using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    /// <summary>
    /// Commands for managing Unity packages via Package Manager.
    /// </summary>
    public static class VmAutomationPackageManagerCommands
    {
        // ─── List Installed Packages ───

        public static object ListPackages(Dictionary<string, object> args)
        {
            var listRequest = Client.List(true);
            while (!listRequest.IsCompleted)
                System.Threading.Thread.Sleep(10);

            if (listRequest.Status == StatusCode.Failure)
                return new { error = listRequest.Error?.message ?? "Failed to list packages" };

            var packages = new List<Dictionary<string, object>>();
            foreach (var pkg in listRequest.Result)
            {
                packages.Add(new Dictionary<string, object>
                {
                    { "name", pkg.name },
                    { "displayName", pkg.displayName },
                    { "version", pkg.version },
                    { "source", pkg.source.ToString() },
                    { "description", pkg.description ?? "" },
                });
            }

            return new Dictionary<string, object>
            {
                { "count", packages.Count },
                { "packages", packages },
            };
        }

        // ─── Add Package ───

        public static object AddPackage(Dictionary<string, object> args)
        {
            string identifier = args.ContainsKey("identifier") ? args["identifier"].ToString() : "";
            if (string.IsNullOrEmpty(identifier))
                return new { error = "identifier is required (e.g. 'com.unity.cinemachine' or 'com.unity.cinemachine@3.0.0')" };

            if (!VmAutomationRuntimePreconditions.TryRequireEditMode(
                    "packages/add",
                    "Package Manager cannot reliably adopt package changes " +
                    "while the Editor is playing or changing Play Mode",
                    out Dictionary<string, object> editModeError))
            {
                return editModeError;
            }

            var addRequest = Client.Add(identifier);
            while (!addRequest.IsCompleted)
                System.Threading.Thread.Sleep(10);

            if (addRequest.Status == StatusCode.Failure)
                return new { error = addRequest.Error?.message ?? "Failed to add package" };

            var pkg = addRequest.Result;
            return new Dictionary<string, object>
            {
                { "success", true },
                { "name", pkg.name },
                { "displayName", pkg.displayName },
                { "version", pkg.version },
            };
        }

        public static void ListPackagesDeferred(Dictionary<string, object> args, Action<object> resolve)
        {
            int offset = Math.Max(0, GetInt(args, "offset", 0));
            int limit = Math.Max(1, Math.Min(200, GetInt(args, "limit", 100)));
            ListRequest request;
            try { request = Client.List(true); }
            catch (Exception exception)
            {
                resolve(VmAutomationResponse.Error(exception.Message, "package_list_start_failed", true));
                return;
            }

            void Tick()
            {
                if (!request.IsCompleted) return;
                EditorApplication.update -= Tick;
                if (request.Status == StatusCode.Failure)
                {
                    resolve(VmAutomationResponse.Error(request.Error?.message ?? "Failed to list packages.",
                        "package_list_failed", true));
                    return;
                }

                var all = request.Result.Select(package => new Dictionary<string, object>
                {
                    { "name", package.name }, { "displayName", package.displayName },
                    { "version", package.version }, { "source", package.source.ToString() },
                    { "description", package.description ?? "" },
                }).OrderBy(package => package["name"].ToString(), StringComparer.Ordinal).ToList();
                var page = all.Skip(offset).Take(limit).ToList();
                resolve(new Dictionary<string, object>
                {
                    { "success", true }, { "count", page.Count }, { "total", all.Count },
                    { "offset", offset }, { "limit", limit },
                    { "hasMore", offset + page.Count < all.Count },
                    { "nextOffset", offset + page.Count < all.Count ? (object)(offset + page.Count) : null },
                    { "packages", page }
                });
            }

            EditorApplication.update += Tick;
            Tick();
        }

        public static void AddPackageDeferred(Dictionary<string, object> args, Action<object> resolve)
        {
            string identifier = args.ContainsKey("identifier") ? args["identifier"]?.ToString() : "";
            if (string.IsNullOrEmpty(identifier))
            {
                resolve(VmAutomationResponse.Error("identifier is required.", "invalid_arguments"));
                return;
            }
            if (!VmAutomationRuntimePreconditions.TryRequireEditMode(
                    "packages/add",
                    "Package Manager cannot reliably adopt package changes " +
                    "while the Editor is playing or changing Play Mode",
                    out Dictionary<string, object> editModeError))
            {
                resolve(editModeError);
                return;
            }
            AddRequest request;
            try { request = Client.Add(identifier); }
            catch (Exception exception)
            {
                resolve(VmAutomationResponse.Error(exception.Message, "package_add_start_failed", true));
                return;
            }
            void Tick()
            {
                if (!request.IsCompleted) return;
                EditorApplication.update -= Tick;
                if (request.Status == StatusCode.Failure)
                {
                    resolve(VmAutomationResponse.Error(request.Error?.message ?? "Failed to add package.",
                        "package_add_failed", true));
                    return;
                }
                resolve(new Dictionary<string, object>
                {
                    { "success", true }, { "name", request.Result.name },
                    { "displayName", request.Result.displayName }, { "version", request.Result.version },
                });
            }
            EditorApplication.update += Tick;
            Tick();
        }

        public static object UpdateGitPackage(Dictionary<string, object> args)
        {
            return VmAutomationWorkspaceJobRunner.StartGitPackageUpdate(args);
        }

        public static object ResolvePackages(Dictionary<string, object> args)
        {
            return VmAutomationWorkspaceJobRunner.StartPackageResolve(args);
        }

        // ─── Lint Package .meta Files ───

        public static object LintPackageMetas(Dictionary<string, object> args)
        {
            string packageName = GetString(args, "name");
            string packagePath = GetString(args, "path");
            bool all = GetBool(args, "all", false);
            bool checkDirectories = GetBool(args, "checkDirectories", true);
            int maxResults = GetInt(args, "maxResults", 200);

            var packageRoots = new List<Dictionary<string, string>>();

            if (!string.IsNullOrEmpty(packagePath))
            {
                packageRoots.Add(new Dictionary<string, string>
                {
                    { "name", string.IsNullOrEmpty(packageName) ? Path.GetFileName(packagePath) : packageName },
                    { "path", GetAbsolutePath(packagePath) },
                });
            }

            if (!string.IsNullOrEmpty(packageName) || all)
            {
                var listRequest = Client.List(true);
                while (!listRequest.IsCompleted)
                    System.Threading.Thread.Sleep(10);

                if (listRequest.Status == StatusCode.Failure)
                    return new { error = listRequest.Error?.message ?? "Failed to list packages" };

                foreach (var pkg in listRequest.Result)
                {
                    if (!all && pkg.name != packageName)
                        continue;

                    if (string.IsNullOrEmpty(pkg.resolvedPath) || !Directory.Exists(pkg.resolvedPath))
                        continue;

                    packageRoots.Add(new Dictionary<string, string>
                    {
                        { "name", pkg.name },
                        { "path", pkg.resolvedPath },
                    });
                }
            }

            if (packageRoots.Count == 0)
                return new { error = "Pass name, path, or all=true to select package roots to lint." };

            var results = new List<Dictionary<string, object>>();
            int totalMissing = 0;
            foreach (var root in packageRoots)
            {
                string rootName = root["name"];
                string rootPath = root["path"];
                if (!Directory.Exists(rootPath))
                {
                    results.Add(new Dictionary<string, object>
                    {
                        { "name", rootName },
                        { "path", NormalizePath(rootPath) },
                        { "error", "Package path does not exist" },
                    });
                    continue;
                }

                var missing = new List<Dictionary<string, string>>();
                int rootMissingCount = 0;
                bool truncated = false;
                foreach (var entry in Directory.EnumerateFileSystemEntries(rootPath, "*", SearchOption.AllDirectories))
                {
                    if (ShouldSkipPath(entry))
                        continue;

                    bool isDirectory = Directory.Exists(entry);
                    if (isDirectory && !checkDirectories)
                        continue;

                    if (!isDirectory && entry.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string metaPath = entry + ".meta";
                    if (File.Exists(metaPath))
                        continue;

                    rootMissingCount++;
                    if (missing.Count >= maxResults)
                    {
                        truncated = true;
                        continue;
                    }

                    missing.Add(new Dictionary<string, string>
                    {
                        { "path", NormalizePath(GetRelativePath(rootPath, entry)) },
                        { "expectedMeta", NormalizePath(GetRelativePath(rootPath, metaPath)) },
                        { "type", isDirectory ? "directory" : "file" },
                    });
                }

                totalMissing += rootMissingCount;
                results.Add(new Dictionary<string, object>
                {
                    { "name", rootName },
                    { "path", NormalizePath(rootPath) },
                    { "isValid", rootMissingCount == 0 },
                    { "missingCount", rootMissingCount },
                    { "returnedMissingCount", missing.Count },
                    { "truncated", truncated },
                    { "missing", missing },
                });
            }

            return new Dictionary<string, object>
            {
                { "success", true },
                { "isValid", totalMissing == 0 },
                { "missingCount", totalMissing },
                { "packages", results },
            };
        }

        // ─── Remove Package ───

        public static object RemovePackage(Dictionary<string, object> args)
        {
            string name = args.ContainsKey("name") ? args["name"].ToString() : "";
            if (string.IsNullOrEmpty(name))
                return new { error = "name is required (e.g. 'com.unity.cinemachine')" };

            if (!VmAutomationRuntimePreconditions.TryRequireEditMode(
                    "packages/remove",
                    "Package Manager cannot reliably adopt package changes " +
                    "while the Editor is playing or changing Play Mode",
                    out Dictionary<string, object> editModeError))
            {
                return editModeError;
            }

            var removeRequest = Client.Remove(name);
            while (!removeRequest.IsCompleted)
                System.Threading.Thread.Sleep(10);

            if (removeRequest.Status == StatusCode.Failure)
                return new { error = removeRequest.Error?.message ?? "Failed to remove package" };

            return new Dictionary<string, object>
            {
                { "success", true },
                { "removed", name },
            };
        }

        public static void RemovePackageDeferred(Dictionary<string, object> args, Action<object> resolve)
        {
            string name = args.ContainsKey("name") ? args["name"]?.ToString() : "";
            if (string.IsNullOrEmpty(name))
            {
                resolve(VmAutomationResponse.Error("name is required.", "invalid_arguments"));
                return;
            }
            if (!VmAutomationRuntimePreconditions.TryRequireEditMode(
                    "packages/remove",
                    "Package Manager cannot reliably adopt package changes " +
                    "while the Editor is playing or changing Play Mode",
                    out Dictionary<string, object> editModeError))
            {
                resolve(editModeError);
                return;
            }
            RemoveRequest request;
            try { request = Client.Remove(name); }
            catch (Exception exception)
            {
                resolve(VmAutomationResponse.Error(exception.Message, "package_remove_start_failed", true));
                return;
            }
            void Tick()
            {
                if (!request.IsCompleted) return;
                EditorApplication.update -= Tick;
                if (request.Status == StatusCode.Failure)
                {
                    resolve(VmAutomationResponse.Error(request.Error?.message ?? "Failed to remove package.",
                        "package_remove_failed", true));
                    return;
                }
                resolve(new Dictionary<string, object> { { "success", true }, { "removed", name } });
            }
            EditorApplication.update += Tick;
            Tick();
        }

        // ─── Search Package ───

        public static object SearchPackage(Dictionary<string, object> args)
        {
            string query = args.ContainsKey("query") ? args["query"].ToString() : "";
            if (string.IsNullOrEmpty(query))
                return new { error = "query is required" };

            var searchRequest = Client.Search(query);
            while (!searchRequest.IsCompleted)
                System.Threading.Thread.Sleep(10);

            if (searchRequest.Status == StatusCode.Failure)
                return new { error = searchRequest.Error?.message ?? "Search failed" };

            var results = new List<Dictionary<string, object>>();
            foreach (var pkg in searchRequest.Result)
            {
                results.Add(new Dictionary<string, object>
                {
                    { "name", pkg.name },
                    { "displayName", pkg.displayName },
                    { "version", pkg.version },
                    { "description", pkg.description ?? "" },
                });
            }

            return new Dictionary<string, object>
            {
                { "query", query },
                { "count", results.Count },
                { "results", results },
            };
        }

        public static void SearchPackageDeferred(Dictionary<string, object> args, Action<object> resolve)
        {
            string query = args.ContainsKey("query") ? args["query"]?.ToString() : "";
            if (string.IsNullOrEmpty(query))
            {
                resolve(VmAutomationResponse.Error("query is required.", "invalid_arguments"));
                return;
            }
            int offset = Math.Max(0, GetInt(args, "offset", 0));
            int limit = Math.Max(1, Math.Min(200, GetInt(args, "limit", 50)));
            SearchRequest request;
            try { request = Client.Search(query); }
            catch (Exception exception)
            {
                resolve(VmAutomationResponse.Error(exception.Message, "package_search_start_failed", true));
                return;
            }
            void Tick()
            {
                if (!request.IsCompleted) return;
                EditorApplication.update -= Tick;
                if (request.Status == StatusCode.Failure)
                {
                    resolve(VmAutomationResponse.Error(request.Error?.message ?? "Package search failed.",
                        "package_search_failed", true));
                    return;
                }
                var all = request.Result.Select(package => new Dictionary<string, object>
                {
                    { "name", package.name }, { "displayName", package.displayName },
                    { "version", package.version }, { "description", package.description ?? "" },
                }).ToList();
                var page = all.Skip(offset).Take(limit).ToList();
                resolve(new Dictionary<string, object>
                {
                    { "success", true }, { "query", query }, { "total", all.Count },
                    { "offset", offset }, { "limit", limit },
                    { "hasMore", offset + page.Count < all.Count },
                    { "nextOffset", offset + page.Count < all.Count ? (object)(offset + page.Count) : null },
                    { "results", page },
                });
            }
            EditorApplication.update += Tick;
            Tick();
        }

        // ─── Get Package Info ───

        public static object GetPackageInfo(Dictionary<string, object> args)
        {
            string name = args.ContainsKey("name") ? args["name"].ToString() : "";
            if (string.IsNullOrEmpty(name))
                return new { error = "name is required" };

            var listRequest = Client.List(true);
            while (!listRequest.IsCompleted)
                System.Threading.Thread.Sleep(10);

            if (listRequest.Status == StatusCode.Failure)
                return new { error = "Failed to list packages" };

            foreach (var pkg in listRequest.Result)
            {
                if (pkg.name == name)
                {
                    var versions = new List<string>();
                    if (pkg.versions != null && pkg.versions.compatible != null)
                        versions.AddRange(pkg.versions.compatible);

                    return new Dictionary<string, object>
                    {
                        { "name", pkg.name },
                        { "displayName", pkg.displayName },
                        { "version", pkg.version },
                        { "source", pkg.source.ToString() },
                        { "description", pkg.description ?? "" },
                        { "category", pkg.category ?? "" },
                        { "documentationUrl", pkg.documentationUrl ?? "" },
                        { "compatibleVersions", versions },
                        { "dependencies", pkg.dependencies?.Select(d => d.name + "@" + d.version).ToList() ?? new List<string>() },
                    };
                }
            }

            return new { error = $"Package '{name}' not found" };
        }

        public static object GetPackageStatus(Dictionary<string, object> args)
        {
            string name = GetString(args, "name");
            bool includeResolved = GetBool(args, "includeResolved", false);
            var manifestDependencies = GetManifestDependencies();
            var packages = new List<Dictionary<string, object>>();

            if (string.IsNullOrEmpty(name))
            {
                foreach (var pair in manifestDependencies)
                {
                    if (IsGitIdentifier(pair.Value))
                        packages.Add(BuildPackageStatus(pair.Key, pair.Value, includeResolved));
                }
            }
            else
            {
                manifestDependencies.TryGetValue(name, out string manifestDependency);
                packages.Add(BuildPackageStatus(name, manifestDependency ?? "", includeResolved));
            }

            return new Dictionary<string, object>
            {
                { "success", true },
                { "isCompiling", EditorApplication.isCompiling },
                { "isUpdating", EditorApplication.isUpdating },
                { "projectRoot", GetProjectRoot() },
                { "manifestPath", NormalizePath(Path.Combine(GetProjectRoot(), "Packages", "manifest.json")) },
                { "lockPath", NormalizePath(Path.Combine(GetProjectRoot(), "Packages", "packages-lock.json")) },
                { "count", packages.Count },
                { "packages", packages },
            };
        }

        private static Dictionary<string, object> BuildPackageStatus(string name, string manifestDependency,
            bool includeResolved)
        {
            var lockInfo = GetPackageLockInfo(name);
            var result = new Dictionary<string, object>
            {
                { "name", name },
                { "manifestDependency", manifestDependency ?? "" },
                { "manifestRef", GetGitRef(manifestDependency ?? "") },
                { "manifestGitUrl", StripGitRef(manifestDependency ?? "") },
                { "manifestIsGit", IsGitIdentifier(manifestDependency ?? "") },
                { "lockVersion", lockInfo.version },
                { "lockSource", lockInfo.source },
                { "lockHash", lockInfo.hash },
                { "lockRef", GetGitRef(lockInfo.version ?? "") },
                { "manifestMatchesLockHash", !string.IsNullOrEmpty(GetGitRef(manifestDependency ?? "")) &&
                                             !string.IsNullOrEmpty(lockInfo.hash) &&
                                             lockInfo.hash.StartsWith(GetGitRef(manifestDependency ?? ""),
                                                 StringComparison.OrdinalIgnoreCase) },
            };

            if (includeResolved)
                result["resolved"] = GetResolvedPackageInfo(name);

            return result;
        }

        private static Dictionary<string, object> GetResolvedPackageInfo(string name)
        {
            if (string.IsNullOrEmpty(name) || EditorApplication.isUpdating)
                return new Dictionary<string, object>();

            var listRequest = Client.List(true);
            while (!listRequest.IsCompleted)
                System.Threading.Thread.Sleep(10);

            if (listRequest.Status == StatusCode.Failure)
            {
                return new Dictionary<string, object>
                {
                    { "error", listRequest.Error?.message ?? "Failed to list packages" },
                };
            }

            foreach (var pkg in listRequest.Result)
            {
                if (pkg.name != name)
                    continue;

                return new Dictionary<string, object>
                {
                    { "name", pkg.name },
                    { "displayName", pkg.displayName },
                    { "version", pkg.version },
                    { "source", pkg.source.ToString() },
                    { "resolvedPath", NormalizePath(pkg.resolvedPath ?? "") },
                };
            }

            return new Dictionary<string, object>();
        }

        private static string GetString(Dictionary<string, object> args, string key)
        {
            return args != null && args.ContainsKey(key) && args[key] != null ? args[key].ToString() : "";
        }

        private static bool GetBool(Dictionary<string, object> args, string key, bool defaultValue)
        {
            if (args == null || !args.ContainsKey(key) || args[key] == null)
                return defaultValue;

            if (args[key] is bool value)
                return value;

            return bool.TryParse(args[key].ToString(), out bool parsed) ? parsed : defaultValue;
        }

        private static int GetInt(Dictionary<string, object> args, string key, int defaultValue)
        {
            if (args == null || !args.ContainsKey(key) || args[key] == null)
                return defaultValue;

            return int.TryParse(args[key].ToString(), out int parsed) ? parsed : defaultValue;
        }

        internal static bool TryBuildGitPackageIdentifier(Dictionary<string, object> args,
            out string name, out string identifier, out string revision, out string error)
        {
            name = GetString(args, "name");
            identifier = "";
            revision = "";
            error = "";

            if (string.IsNullOrEmpty(name))
            {
                error = "name is required (e.g. 'com.example.package')";
                return false;
            }

            string gitUrl = GetString(args, "gitUrl");
            revision = GetString(args, "revision");
            if (!IsFullCommitHash(revision))
            {
                error = "revision is required and must be a full 40-character Git commit SHA.";
                return false;
            }

            if (string.IsNullOrEmpty(gitUrl))
            {
                string manifestDependency = GetManifestDependency(name);
                if (string.IsNullOrEmpty(manifestDependency))
                {
                    error = $"Package '{name}' was not found in Packages/manifest.json";
                    return false;
                }

                if (!IsGitIdentifier(manifestDependency))
                {
                    error = $"Package '{name}' is not a Git dependency. Pass gitUrl to update it as a Git package.";
                    return false;
                }

                gitUrl = StripGitRef(manifestDependency);
            }

            identifier = StripGitRef(gitUrl) + "#" + revision;
            return true;
        }

        internal static bool TryBuildExpectedGitPackages(Dictionary<string, object> args,
            out List<VmAutomationGitPackageExpectation> expectations, out string error)
        {
            expectations = new List<VmAutomationGitPackageExpectation>();
            error = "";
            if (args == null || !args.TryGetValue("expectedPackages", out object raw) ||
                !(raw is System.Collections.IList values) || values.Count == 0)
            {
                error = "expectedPackages must contain at least one Git package target.";
                return false;
            }

            foreach (object value in values)
            {
                Dictionary<string, object> item = VmAutomationResponse.ToDictionary(value);
                if (item == null || !TryBuildGitPackageIdentifier(item, out string name,
                        out string identifier, out string revision, out error))
                {
                    if (string.IsNullOrEmpty(error))
                        error = "Every expectedPackages item must be an object.";
                    expectations.Clear();
                    return false;
                }
                expectations.Add(new VmAutomationGitPackageExpectation(name, identifier, revision));
            }

            if (expectations.Select(item => item.Name).Distinct(StringComparer.Ordinal).Count() !=
                expectations.Count)
            {
                expectations.Clear();
                error = "expectedPackages contains duplicate package names.";
                return false;
            }
            return true;
        }

        internal static Dictionary<string, object> BuildGitPackageResolutionState(
            VmAutomationGitPackageExpectation expectation)
        {
            string manifestDependency = GetManifestDependency(expectation.Name);
            var lockInfo = GetPackageLockInfo(expectation.Name);
            UnityEditor.PackageManager.PackageInfo packageInfo =
                UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages()
                    .FirstOrDefault(package => package.name == expectation.Name);
            string resolvedIdentifier = packageInfo?.packageId ?? "";
            string resolvedPath = NormalizePath(packageInfo?.resolvedPath ?? "");

            bool manifestMatches =
                string.Equals(StripGitRef(manifestDependency),
                    StripGitRef(expectation.Identifier), StringComparison.OrdinalIgnoreCase) &&
                string.Equals(GetGitRef(manifestDependency), expectation.Revision,
                    StringComparison.OrdinalIgnoreCase);
            bool lockMatches =
                string.Equals(lockInfo.source, "git", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(lockInfo.hash, expectation.Revision,
                    StringComparison.OrdinalIgnoreCase);
            bool resolvedMatches = packageInfo != null &&
                string.Equals(GetGitRef(resolvedIdentifier), expectation.Revision,
                    StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrEmpty(resolvedPath) && Directory.Exists(packageInfo.resolvedPath);

            return new Dictionary<string, object>
            {
                { "name", expectation.Name },
                { "requestedIdentifier", expectation.Identifier },
                { "requestedRevision", expectation.Revision },
                { "manifestDependency", manifestDependency },
                { "manifestMatches", manifestMatches },
                { "lockVersion", lockInfo.version },
                { "lockSource", lockInfo.source },
                { "lockHash", lockInfo.hash },
                { "lockMatches", lockMatches },
                { "resolvedIdentifier", resolvedIdentifier },
                { "resolvedPath", resolvedPath },
                { "resolvedMatches", resolvedMatches },
                { "matches", manifestMatches && lockMatches && resolvedMatches },
            };
        }

        private static string GetManifestDependency(string name)
        {
            var dependencies = GetManifestDependencies();
            return dependencies.TryGetValue(name, out string dependency) ? dependency : "";
        }

        private static Dictionary<string, string> GetManifestDependencies()
        {
            string manifestPath = Path.Combine(GetProjectRoot(), "Packages", "manifest.json");
            if (!File.Exists(manifestPath))
                return new Dictionary<string, string>();

            var manifest = MiniJson.Deserialize(File.ReadAllText(manifestPath)) as Dictionary<string, object>;
            if (manifest == null || !manifest.TryGetValue("dependencies", out object dependenciesObj))
                return new Dictionary<string, string>();

            var dependencies = dependenciesObj as Dictionary<string, object>;
            if (dependencies == null)
                return new Dictionary<string, string>();

            return dependencies.ToDictionary(pair => pair.Key, pair => pair.Value?.ToString() ?? "");
        }

        private static (string version, string source, string hash) GetPackageLockInfo(string name)
        {
            string lockPath = Path.Combine(GetProjectRoot(), "Packages", "packages-lock.json");
            if (!File.Exists(lockPath))
                return ("", "", "");

            var packageLock = MiniJson.Deserialize(File.ReadAllText(lockPath)) as Dictionary<string, object>;
            if (packageLock == null || !packageLock.TryGetValue("dependencies", out object dependenciesObj))
                return ("", "", "");

            var dependencies = dependenciesObj as Dictionary<string, object>;
            if (dependencies == null || !dependencies.TryGetValue(name, out object dependencyObj))
                return ("", "", "");

            var dependency = dependencyObj as Dictionary<string, object>;
            if (dependency == null)
                return ("", "", "");

            string version = dependency.TryGetValue("version", out object versionObj) ? versionObj?.ToString() ?? "" : "";
            string source = dependency.TryGetValue("source", out object sourceObj) ? sourceObj?.ToString() ?? "" : "";
            string hash = dependency.TryGetValue("hash", out object hashObj) ? hashObj?.ToString() ?? "" : "";
            return (version, source, hash);
        }

        private static bool IsGitIdentifier(string identifier)
        {
            return identifier.StartsWith("git+", StringComparison.OrdinalIgnoreCase) ||
                   identifier.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                   identifier.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                   identifier.StartsWith("ssh://", StringComparison.OrdinalIgnoreCase) ||
                   identifier.Contains(".git");
        }

        private static string StripGitRef(string identifier)
        {
            int hashIndex = identifier.IndexOf('#');
            return hashIndex >= 0 ? identifier.Substring(0, hashIndex) : identifier;
        }

        private static string GetGitRef(string identifier)
        {
            int hashIndex = identifier.LastIndexOf('#');
            return hashIndex >= 0 && hashIndex < identifier.Length - 1 ? identifier.Substring(hashIndex + 1) : "";
        }

        private static bool IsLikelyCommitHash(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length < 7 || value.Length > 40)
                return false;

            foreach (char c in value)
            {
                bool isDigit = c >= '0' && c <= '9';
                bool isLowerHex = c >= 'a' && c <= 'f';
                bool isUpperHex = c >= 'A' && c <= 'F';
                if (!isDigit && !isLowerHex && !isUpperHex)
                    return false;
            }

            return true;
        }

        private static bool IsFullCommitHash(string value)
        {
            return value != null && value.Length == 40 && IsLikelyCommitHash(value);
        }

        private static string GetProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        private static string GetAbsolutePath(string path)
        {
            return Path.IsPathRooted(path) ? Path.GetFullPath(path) : Path.GetFullPath(Path.Combine(GetProjectRoot(), path));
        }

        private static bool ShouldSkipPath(string path)
        {
            string normalized = NormalizePath(path);
            return HasHiddenPathSegment(normalized) ||
                   HasUnityIgnoredTildeSegment(normalized) ||
                   HasPathSegment(normalized, ".git") ||
                   HasPathSegment(normalized, "node_modules") ||
                   HasPathSegment(normalized, "Temp") ||
                   HasPathSegment(normalized, "obj") ||
                   HasPathSegment(normalized, "bin");
        }

        private static bool HasPathSegment(string path, string segment)
        {
            return path.Split('/').Contains(segment);
        }

        private static bool HasHiddenPathSegment(string path)
        {
            foreach (string segment in path.Split('/'))
            {
                if (string.IsNullOrEmpty(segment))
                    continue;

                if (segment == "." || segment == "..")
                    continue;

                if (segment.StartsWith(".", StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static bool HasUnityIgnoredTildeSegment(string path)
        {
            foreach (string segment in path.Split('/'))
            {
                if (segment.EndsWith("~", StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static string GetRelativePath(string root, string path)
        {
            Uri rootUri = new Uri(AppendDirectorySeparatorChar(Path.GetFullPath(root)));
            Uri pathUri = new Uri(Path.GetFullPath(path));
            return Uri.UnescapeDataString(rootUri.MakeRelativeUri(pathUri).ToString());
        }

        private static string AppendDirectorySeparatorChar(string path)
        {
            return path.EndsWith(Path.DirectorySeparatorChar.ToString()) ? path : path + Path.DirectorySeparatorChar;
        }

        private static string NormalizePath(string path)
        {
            return path.Replace('\\', '/');
        }
    }
}
