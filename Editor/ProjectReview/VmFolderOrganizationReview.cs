using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace VMUnityAutomation.Editor
{
    public static class VmFolderOrganizationReview
    {
        public const string ConfigurationPath = "ProjectSettings/VMUnityAutomationFolderAudit.json";
        public const int MaximumConfigurationBytes = 65536;
        private const int MaximumDirectories = 4096;
        private const int MaximumEntries = 32768;
        private const int MaximumPathLength = 1024;
        private const int MaximumPathSegments = 64;
        private const int MaximumSamples = 8;

        public static VmFolderOrganizationResult Review(string projectRoot, string configuration,
            string[] paths = null, int maxIssues = 200)
        {
            if (maxIssues < 1 || maxIssues > 5000)
                throw new ArgumentOutOfRangeException(nameof(maxIssues), "maxIssues must be in 1..5000.");
            if (configuration == null || Encoding.UTF8.GetByteCount(configuration) > MaximumConfigurationBytes)
                throw new InvalidDataException("Folder policy must be a JSON document of at most 64 KiB.");
            projectRoot = Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var policy = ParsePolicy(configuration);
            var selected = paths ?? Array.Empty<string>();
            if (selected.Length > 64)
                throw new InvalidDataException("At most 64 requested directories are allowed.");
            foreach (var folder in policy.Folders)
                RequireDirectory(projectRoot, folder.Path);
            foreach (string excluded in policy.ExcludePaths)
                if (!policy.Folders.Any(folder => IsWithin(excluded, folder.Path)))
                    throw new InvalidDataException($"Excluded directory '{excluded}' is outside the configured folders.");
            var starts = selected.Length == 0
                ? policy.Folders.Select(folder => folder.Path).ToArray()
                : selected.Select(NormalizePath).ToArray();
            foreach (string start in starts)
            {
                if (!policy.Folders.Any(folder => Covers(folder, start)))
                    throw new InvalidDataException($"Requested directory '{start}' is outside the configured folders.");
                RequireDirectory(projectRoot, start);
            }

            var result = new VmFolderOrganizationResult
            {
                ConfigurationPath = ConfigurationPath,
                Roots = starts.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value, StringComparer.Ordinal).ToArray()
            };
            var pending = new Stack<string>(result.Roots);
            var discovered = new HashSet<string>(result.Roots, StringComparer.OrdinalIgnoreCase);
            var findings = new List<VmFolderOrganizationResult.Finding>();
            while (pending.Count > 0)
            {
                string current = pending.Pop();
                if (policy.ExcludePaths.Any(excluded => IsWithin(current, excluded)))
                {
                    result.ExcludedDirectories++;
                    continue;
                }
                result.ScannedDirectories++;
                var effective = policy.Folders.Where(folder => Covers(folder, current))
                    .OrderByDescending(folder => folder.Path.Length).First();
                bool recurse = policy.Folders.Any(folder => folder.Recursive && IsWithin(current, folder.Path));
                int files = 0, directories = 0;
                var examples = new SortedSet<string>(StringComparer.Ordinal);
                foreach (string entry in Directory.EnumerateFileSystemEntries(FullPath(projectRoot, current)))
                {
                    if (++result.ScannedEntries > MaximumEntries)
                        throw new InvalidDataException("Folder review exceeds 32768 entries; narrow paths.");
                    string name = Path.GetFileName(entry);
                    string relative = NormalizePath(current + "/" + name);
                    var attributes = File.GetAttributes(entry);
                    if ((attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        result.SkippedLinks++;
                        continue;
                    }
                    if ((attributes & FileAttributes.Directory) != 0)
                    {
                        if (policy.ExcludePaths.Any(excluded => IsWithin(relative, excluded)))
                        {
                            result.ExcludedDirectories++;
                            continue;
                        }
                        directories++;
                        if (recurse && discovered.Add(relative))
                        {
                            if (discovered.Count > MaximumDirectories)
                                throw new InvalidDataException("Folder review exceeds 4096 directories; narrow paths.");
                            pending.Push(relative);
                        }
                        continue;
                    }
                    if (name.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                    {
                        result.IgnoredMetaFiles++;
                        continue;
                    }
                    files++;
                    examples.Add(name);
                    if (examples.Count > MaximumSamples) examples.Remove(examples.Max);
                }
                if (files > effective.MaxDirectFiles || directories > effective.MaxDirectSubfolders)
                    findings.Add(new VmFolderOrganizationResult.Finding
                    {
                        Path = current, PolicyFolder = effective.Path,
                        DirectFiles = files, DirectSubfolders = directories,
                        MaxDirectFiles = effective.MaxDirectFiles, MaxDirectSubfolders = effective.MaxDirectSubfolders,
                        SampleFiles = examples.ToArray(),
                        Message = $"Directory has {files} direct content files and {directories} direct subfolders " +
                                  $"(limits {effective.MaxDirectFiles}/{effective.MaxDirectSubfolders}). " +
                                  "Review project-owned classification by responsibility or content; do not split mechanically " +
                                  "to meet a count or reorganize external/vendor contents."
                    });
            }
            if (result.ScannedDirectories == 0)
                throw new InvalidDataException("No directories were reviewed; check the folder policy and exclusions.");
            result.IssueCount = findings.Count;
            result.Issues = findings.OrderBy(finding => finding.Path, StringComparer.Ordinal).Take(maxIssues).ToList();
            result.Truncated = findings.Count > result.Issues.Count;
            result.RequiresReview = findings.Count > 0;
            result.Passed = findings.Count == 0;
            result.Success = true;
            return result;
        }

        private static Policy ParsePolicy(string json)
        {
            JObject values;
            try
            {
                using (var reader = new JsonTextReader(new StringReader(json)) { MaxDepth = 8 })
                {
                    values = JObject.Load(reader, new JsonLoadSettings
                        { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    if (reader.Read()) throw new InvalidDataException("Folder policy has trailing JSON content.");
                }
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException("Invalid folder policy JSON: " + exception.Message, exception);
            }
            RequireKeys(values, "schemaVersion", "folders", "excludePaths");
            RequireInteger(values, "schemaVersion", 1, 1);
            var folders = RequireArray(values, "folders", 1, 16);
            var policy = new Policy();
            var identities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (object entry in folders)
            {
                var folder = RequireObject(entry, "folders entry");
                RequireKeys(folder, "path", "maxDirectFiles", "maxDirectSubfolders", "recursive");
                string path = ReadPath(folder, "path");
                if (!identities.Add(path))
                    throw new InvalidDataException($"Duplicate configured folder '{path}'.");
                policy.Folders.Add(new FolderPolicy
                {
                    Path = path,
                    Recursive = ReadRecursive(folder),
                    MaxDirectFiles = RequireInteger(folder, "maxDirectFiles", 1, 10000),
                    MaxDirectSubfolders = RequireInteger(folder, "maxDirectSubfolders", 1, 1000)
                });
            }
            if (values.Property("excludePaths") != null)
                foreach (object entry in RequireArray(values, "excludePaths", 0, 64))
                {
                    if (!(entry is JValue literal) || literal.Type != JTokenType.String)
                        throw new InvalidDataException("excludePaths entries must be directory paths.");
                    policy.ExcludePaths.Add(NormalizePath((string)literal.Value));
                }
            return policy;
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new InvalidDataException("Directory paths must be nonempty project-relative paths.");
            path = path.Replace('\\', '/').TrimEnd('/');
            var segments = path.Split('/');
            if (path.Length > MaximumPathLength || segments.Length > MaximumPathSegments ||
                Path.IsPathRooted(path) || path.IndexOf(':') >= 0 ||
                segments.Any(segment => segment.Length == 0 || segment == "." || segment == ".."))
                throw new InvalidDataException($"Invalid project-relative directory path '{path}'.");
            return path;
        }

        private static bool IsWithin(string path, string parent) =>
            path.Equals(parent, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith(parent + "/", StringComparison.OrdinalIgnoreCase);

        private static bool Covers(FolderPolicy folder, string path) =>
            path.Equals(folder.Path, StringComparison.OrdinalIgnoreCase) ||
            (folder.Recursive && IsWithin(path, folder.Path));

        private static bool ReadRecursive(JObject folder)
        {
            if (!folder.TryGetValue("recursive", out JToken value)) return true;
            if (value.Type != JTokenType.Boolean)
                throw new InvalidDataException("recursive must be a boolean.");
            return value.Value<bool>();
        }

        private static string FullPath(string root, string relative) =>
            Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));

        private static void RequireDirectory(string root, string relative)
        {
            string full = FullPath(root, relative);
            if (!Directory.Exists(full))
                throw new InvalidDataException($"Configured/requested directory '{relative}' does not exist.");
            string current = root;
            foreach (string segment in relative.Split('/'))
            {
                current = Path.Combine(current, segment);
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException($"Directory '{relative}' traverses a link; external directories are not reviewed.");
            }
        }

        private static JObject RequireObject(object value, string field)
        {
            if (!(value is JObject dictionary))
                throw new InvalidDataException(field + " must be an object.");
            return dictionary;
        }

        private static void RequireKeys(JObject values, params string[] allowed)
        {
            foreach (var property in values.Properties())
                if (!allowed.Contains(property.Name))
                    throw new InvalidDataException("Unknown folder policy field '" + property.Name + "'.");
        }

        private static JArray RequireArray(JObject values, string field, int minimum, int maximum)
        {
            if (!values.TryGetValue(field, out JToken value) || !(value is JArray list) ||
                list.Count < minimum || list.Count > maximum)
                throw new InvalidDataException($"{field} must be an array with {minimum}..{maximum} entries.");
            return list;
        }

        private static int RequireInteger(JObject values, string field, int minimum, int maximum)
        {
            if (!values.TryGetValue(field, out JToken value) || value.Type != JTokenType.Integer ||
                !(value is JValue literal) || !(literal.Value is long number) ||
                number < minimum || number > maximum)
                throw new InvalidDataException($"{field} must be an integer in {minimum}..{maximum}.");
            return (int)number;
        }

        private static string ReadPath(JObject values, string field)
        {
            if (!values.TryGetValue(field, out JToken value) || value.Type != JTokenType.String)
                throw new InvalidDataException(field + " must be a project-relative directory path.");
            return NormalizePath(value.Value<string>());
        }

        private sealed class Policy
        {
            internal readonly List<FolderPolicy> Folders = new List<FolderPolicy>();
            internal readonly List<string> ExcludePaths = new List<string>();
        }

        private sealed class FolderPolicy
        {
            internal string Path;
            internal bool Recursive;
            internal int MaxDirectFiles, MaxDirectSubfolders;
        }
    }
}
