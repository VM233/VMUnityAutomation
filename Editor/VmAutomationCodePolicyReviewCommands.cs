#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    public static class VmAutomationCodePolicyReviewCommands
    {
        private const int MaxFiles = 4096;
        private const long MaxFileBytes = 4L * 1024L * 1024L;

        public static object Review(Dictionary<string, object> args)
        {
            int maxIssues = Math.Max(1, Math.Min(GetInt(args, "maxIssues", 200), 5000));
            int maxTypeLines = Math.Max(1, Math.Min(GetInt(args, "maxTypeLines", 1500), 100000));
            bool changedOnly = GetBool(args, "changedOnly", false);
            bool forbidPartial = GetBool(args, "forbidPartial",
                changedOnly || GetStringList(args, "paths").Count > 0);
            var errors = new List<string>();
            var report = new ReviewReport(maxIssues);

            if (!VmAutomationEditorCommands.TryGetRoslynAssemblies(
                    out Assembly csharpAssembly, out Assembly coreAssembly))
            {
                errors.Add("Roslyn (Microsoft.CodeAnalysis) is not available in this Unity Editor.");
                return BuildResult(report, errors, changedOnly, 0);
            }

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            List<string> files = ResolveFiles(args, projectRoot, changedOnly, errors);
            if (files.Count > MaxFiles)
            {
                errors.Add($"The review selected {files.Count} C# files, above the {MaxFiles} file limit. Narrow paths or enable changedOnly.");
                files = files.Take(MaxFiles).ToList();
            }

            var forbiddenMethods = GetStringList(args, "forbiddenMethodNames");
            var forbiddenMembers = new HashSet<string>(
                GetStringList(args, "forbiddenMemberAccesses").Select(RemoveWhitespace),
                StringComparer.Ordinal);
            var forbiddenGenericInvocations = GetStringList(args, "forbiddenGenericInvocations")
                .Select(RemoveWhitespace).Where(value => value.Length > 0).ToList();

            foreach (string file in files)
            {
                try
                {
                    var info = new FileInfo(file);
                    string assetPath = ToProjectPath(projectRoot, file);
                    if (info.Length > MaxFileBytes)
                    {
                        report.Record(new ReviewIssue(assetPath, 1, 1,
                            "file-size-limit",
                            $"C# source is {info.Length} bytes, above the {MaxFileBytes} byte review limit."));
                        continue;
                    }

                    string source = File.ReadAllText(file);
                    AuditEndOfFile(assetPath, source, report);
                    object syntaxTree = ParseSyntaxTree(csharpAssembly, source);
                    object root = InvokeWithDefaults(syntaxTree, "GetRoot");
                    List<object> nodes = DescendantNodes(root).ToList();
                    AuditTopLevelTypes(assetPath, source, nodes, maxTypeLines, forbidPartial,
                        report);
                    AuditAttributes(assetPath, source, nodes, report);
                    AuditForbiddenSyntax(assetPath, source, nodes, forbiddenMethods,
                        forbiddenMembers, forbiddenGenericInvocations, report);
                }
                catch (Exception exception)
                {
                    errors.Add($"Failed to review '{ToProjectPath(projectRoot, file)}': {Unwrap(exception).Message}");
                }
            }

            return BuildResult(report, errors, changedOnly, files.Count);
        }

        private static Dictionary<string, object> BuildResult(ReviewReport report,
            IReadOnlyCollection<string> errors, bool changedOnly, int scannedFiles)
        {
            return new Dictionary<string, object>
            {
                { "success", errors.Count == 0 },
                { "passed", errors.Count == 0 && report.IssueCount == 0 },
                { "changedOnly", changedOnly },
                { "scannedFiles", scannedFiles },
                { "issueCount", report.IssueCount },
                { "truncated", report.Truncated },
                { "issues", report.Issues.Select(issue => issue.ToDictionary()).ToList() },
                { "errors", errors.ToList() }
            };
        }

        private static void AuditEndOfFile(string assetPath, string source,
            ReviewReport report)
        {
            int trailingLineBreakCharacters = 0;
            for (int index = source.Length - 1; index >= 0; index--)
            {
                if (source[index] != '\r' && source[index] != '\n')
                    break;
                trailingLineBreakCharacters++;
            }

            bool exactlyOne = source.EndsWith("\r\n", StringComparison.Ordinal)
                ? trailingLineBreakCharacters == 2
                : source.EndsWith("\n", StringComparison.Ordinal) &&
                  trailingLineBreakCharacters == 1;
            if (!exactlyOne)
            {
                report.Record(new ReviewIssue(assetPath,
                    Math.Max(1, CountLines(source)), 1, "eof-newline",
                    "C# files must end with exactly one newline."));
            }
        }

        private static void AuditTopLevelTypes(string assetPath, string source,
            IReadOnlyList<object> nodes, int maxTypeLines, bool forbidPartial,
            ReviewReport report)
        {
            var topLevelTypes = nodes.Where(IsTopLevelTypeDeclaration).ToList();
            if (topLevelTypes.Count > 1)
            {
                GetLocation(source, topLevelTypes[1], out int line, out int column);
                report.Record(new ReviewIssue(assetPath, line, column,
                    "one-top-level-type-per-file",
                    $"File declares {topLevelTypes.Count} top-level types. Keep exactly one top-level type per file."));
            }

            foreach (object node in nodes)
            {
                string typeName = node.GetType().Name;
                bool isClassOrRecord = typeName == "ClassDeclarationSyntax" ||
                                       typeName == "RecordDeclarationSyntax";
                if (!isClassOrRecord)
                    continue;

                GetLocation(source, node, out int line, out int column);
                int typeLines = CountNodeLines(source, node);
                if (typeLines > maxTypeLines)
                {
                    report.Record(new ReviewIssue(assetPath, line, column,
                        "type-line-limit",
                        $"{GetIdentifier(node, typeName)} spans {typeLines} lines, above the {maxTypeLines} line limit."));
                }

                if (forbidPartial && HasModifier(node, "partial"))
                {
                    report.Record(new ReviewIssue(assetPath, line, column,
                        "partial-type",
                        $"{GetIdentifier(node, typeName)} is partial. New or modified review scope must use a single owned declaration."));
                }
            }
        }

        private static void AuditAttributes(string assetPath, string source,
            IEnumerable<object> nodes, ReviewReport report)
        {
            foreach (object node in nodes.Where(value =>
                         value.GetType().Name == "AttributeSyntax"))
            {
                object nameNode = GetProperty(node, "Name");
                string authored = RemoveWhitespace(nameNode?.ToString() ?? "");
                string normalized = authored.Replace("global::", "");
                if (!string.Equals(normalized, "Serializable", StringComparison.Ordinal) &&
                    (normalized.EndsWith(".Serializable", StringComparison.Ordinal) ||
                     normalized.EndsWith("SerializableAttribute", StringComparison.Ordinal)))
                {
                    GetLocation(source, node, out int line, out int column);
                    report.Record(new ReviewIssue(assetPath, line, column,
                        "serializable-attribute-style",
                        "Use [Serializable] instead of a qualified or Attribute-suffixed spelling."));
                }
            }
        }

        private static void AuditForbiddenSyntax(string assetPath, string source,
            IEnumerable<object> nodes, IReadOnlyCollection<string> forbiddenMethods,
            ISet<string> forbiddenMembers, IReadOnlyCollection<string> forbiddenGenericInvocations,
            ReviewReport report)
        {
            foreach (object node in nodes)
            {
                string typeName = node.GetType().Name;
                if (typeName == "MethodDeclarationSyntax")
                {
                    string identifier = GetIdentifierValue(node);
                    if (forbiddenMethods.Contains(identifier))
                    {
                        GetLocation(source, node, out int line, out int column);
                        report.Record(new ReviewIssue(assetPath, line, column,
                            "forbidden-method", $"Method '{identifier}' is forbidden by this review policy."));
                    }
                }
                else if (typeName == "MemberAccessExpressionSyntax")
                {
                    string expression = RemoveWhitespace(node.ToString());
                    if (forbiddenMembers.Contains(expression))
                    {
                        GetLocation(source, node, out int line, out int column);
                        report.Record(new ReviewIssue(assetPath, line, column,
                            "forbidden-member-access", $"Member access '{expression}' is forbidden by this review policy."));
                    }
                }
                else if (typeName == "InvocationExpressionSyntax" &&
                         forbiddenGenericInvocations.Count > 0)
                {
                    string expression = RemoveWhitespace(
                        GetProperty(node, "Expression")?.ToString() ?? "");
                    string matched = forbiddenGenericInvocations.FirstOrDefault(pattern =>
                        string.Equals(expression, pattern, StringComparison.Ordinal) ||
                        expression.EndsWith("." + pattern, StringComparison.Ordinal));
                    if (!string.IsNullOrEmpty(matched))
                    {
                        GetLocation(source, node, out int line, out int column);
                        report.Record(new ReviewIssue(assetPath, line, column,
                            "forbidden-generic-invocation",
                            $"Generic invocation '{matched}' is forbidden by this review policy."));
                    }
                }
            }
        }

        private static object ParseSyntaxTree(Assembly csharpAssembly, string source)
        {
            Type syntaxTreeType = csharpAssembly.GetType(
                "Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree", true);
            Type parseOptionsType = csharpAssembly.GetType(
                "Microsoft.CodeAnalysis.CSharp.CSharpParseOptions");
            Type languageVersionType = csharpAssembly.GetType(
                "Microsoft.CodeAnalysis.CSharp.LanguageVersion");
            object parseOptions = null;
            if (parseOptionsType != null && languageVersionType != null)
            {
                object defaults = parseOptionsType.GetProperty("Default",
                    BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                MethodInfo withLanguageVersion = parseOptionsType.GetMethod(
                    "WithLanguageVersion", BindingFlags.Public | BindingFlags.Instance,
                    null, new[] { languageVersionType }, null);
                if (defaults != null && withLanguageVersion != null)
                {
                    parseOptions = withLanguageVersion.Invoke(defaults,
                        new[] { Enum.Parse(languageVersionType, "Preview") });
                }
            }

            MethodInfo parseText = syntaxTreeType
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(method => method.Name == "ParseText")
                .Where(method => method.GetParameters().Length > 0 &&
                                 method.GetParameters()[0].ParameterType == typeof(string))
                .OrderByDescending(method => parseOptions != null && method.GetParameters()
                    .Any(parameter => parameter.ParameterType == parseOptionsType))
                .ThenBy(method => method.GetParameters().Length)
                .FirstOrDefault();
            if (parseText == null)
                throw new MissingMethodException(syntaxTreeType.FullName, "ParseText");

            ParameterInfo[] parameters = parseText.GetParameters();
            var invokeArgs = new object[parameters.Length];
            invokeArgs[0] = source;
            for (int index = 1; index < parameters.Length; index++)
            {
                invokeArgs[index] = parseOptions != null &&
                                    parameters[index].ParameterType == parseOptionsType
                    ? parseOptions
                    : DefaultParameterValue(parameters[index]);
            }
            return parseText.Invoke(null, invokeArgs);
        }

        private static object InvokeWithDefaults(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethods(BindingFlags.Public |
                                                            BindingFlags.Instance)
                .Where(candidate => candidate.Name == methodName)
                .OrderBy(candidate => candidate.GetParameters().Length)
                .FirstOrDefault();
            if (method == null)
                throw new MissingMethodException(target.GetType().FullName, methodName);
            ParameterInfo[] parameters = method.GetParameters();
            return method.Invoke(target, parameters.Select(DefaultParameterValue).ToArray());
        }

        private static IEnumerable<object> DescendantNodes(object root)
        {
            object values = InvokeWithDefaults(root, "DescendantNodes");
            if (values is IEnumerable enumerable)
            {
                foreach (object value in enumerable)
                    yield return value;
            }
        }

        private static object DefaultParameterValue(ParameterInfo parameter)
        {
            if (parameter.HasDefaultValue)
                return parameter.DefaultValue;
            return parameter.ParameterType.IsValueType
                ? Activator.CreateInstance(parameter.ParameterType)
                : null;
        }

        private static bool IsTopLevelTypeDeclaration(object node)
        {
            string typeName = node.GetType().Name;
            if (typeName != "ClassDeclarationSyntax" &&
                typeName != "RecordDeclarationSyntax" &&
                typeName != "StructDeclarationSyntax" &&
                typeName != "InterfaceDeclarationSyntax" &&
                typeName != "EnumDeclarationSyntax" &&
                typeName != "DelegateDeclarationSyntax")
                return false;
            object parent = GetProperty(node, "Parent");
            string parentType = parent?.GetType().Name ?? "";
            return parentType == "CompilationUnitSyntax" ||
                   parentType == "NamespaceDeclarationSyntax" ||
                   parentType == "FileScopedNamespaceDeclarationSyntax";
        }

        private static bool HasModifier(object node, string modifier)
        {
            object modifiers = GetProperty(node, "Modifiers");
            if (!(modifiers is IEnumerable enumerable))
                return false;
            foreach (object token in enumerable)
            {
                string value = GetProperty(token, "ValueText")?.ToString() ?? token.ToString();
                if (string.Equals(value, modifier, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        private static string GetIdentifier(object node, string typeName)
        {
            string identifier = GetIdentifierValue(node);
            return string.IsNullOrWhiteSpace(identifier) ? typeName : $"Type '{identifier}'";
        }

        private static string GetIdentifierValue(object node)
        {
            object token = GetProperty(node, "Identifier");
            return GetProperty(token, "ValueText")?.ToString() ?? token?.ToString() ?? "";
        }

        private static object GetProperty(object target, string name)
        {
            return target?.GetType().GetProperty(name, BindingFlags.Public |
                                                       BindingFlags.Instance)?.GetValue(target);
        }

        private static int CountNodeLines(string source, object node)
        {
            GetSpan(node, out int start, out int end);
            start = Math.Max(0, Math.Min(source.Length, start));
            end = Math.Max(start, Math.Min(source.Length, end));
            return CountLines(source.Substring(start, end - start));
        }

        private static void GetLocation(string source, object node, out int line,
            out int column)
        {
            int start = Convert.ToInt32(GetProperty(node, "SpanStart") ?? 0);
            start = Math.Max(0, Math.Min(source.Length, start));
            int previousLineBreak = start > 0 ? source.LastIndexOf('\n', start - 1) : -1;
            line = 1;
            for (int index = 0; index < start; index++)
            {
                if (source[index] == '\n')
                    line++;
            }
            column = start - previousLineBreak;
        }

        private static void GetSpan(object node, out int start, out int end)
        {
            object fullSpan = GetProperty(node, "FullSpan");
            start = Convert.ToInt32(GetProperty(fullSpan, "Start") ?? 0);
            end = Convert.ToInt32(GetProperty(fullSpan, "End") ?? start);
        }

        private static int CountLines(string value)
        {
            if (string.IsNullOrEmpty(value))
                return 1;
            int result = 1;
            for (int index = 0; index < value.Length; index++)
            {
                if (value[index] == '\n')
                    result++;
            }
            return result;
        }

        private static List<string> ResolveFiles(Dictionary<string, object> args,
            string projectRoot, bool changedOnly, ICollection<string> errors)
        {
            var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<string> paths = GetStringList(args, "paths");
            if (paths.Count > 0)
            {
                foreach (string path in paths)
                    AddPath(projectRoot, path, candidates, errors);
            }
            else if (changedOnly)
            {
                foreach (string path in ReadChangedPaths(projectRoot,
                             GetString(args, "gitBase"), errors))
                    AddPath(projectRoot, path, candidates, errors);
            }
            else
            {
                List<string> roots = GetStringList(args, "roots");
                if (roots.Count == 0)
                    roots.Add("Assets");
                foreach (string root in roots)
                    AddPath(projectRoot, root, candidates, errors);
            }

            var excluded = GetStringList(args, "excludePaths")
                .Select(path => NormalizeProjectPath(path).TrimEnd('/') + "/").ToList();
            return candidates.Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                .Where(path => !excluded.Any(prefix =>
                    (ToProjectPath(projectRoot, path) + "/").StartsWith(prefix,
                        StringComparison.OrdinalIgnoreCase)))
                .OrderBy(path => path, StringComparer.Ordinal).ToList();
        }

        private static void AddPath(string projectRoot, string path,
            ISet<string> result, ICollection<string> errors)
        {
            if (string.IsNullOrWhiteSpace(path))
                return;
            string fullPath = Path.GetFullPath(Path.IsPathRooted(path)
                ? path
                : Path.Combine(projectRoot, path));
            string rootPrefix = projectRoot.TrimEnd(Path.DirectorySeparatorChar,
                                    Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Review path is outside the Unity project: {path}");
                return;
            }
            if (File.Exists(fullPath))
            {
                result.Add(fullPath);
                return;
            }
            if (!Directory.Exists(fullPath))
            {
                errors.Add($"Review path does not exist: {path}");
                return;
            }
            foreach (string file in Directory.EnumerateFiles(fullPath, "*.cs",
                         SearchOption.AllDirectories))
            {
                string normalized = file.Replace('\\', '/');
                if (normalized.Contains("/Library/") || normalized.Contains("/.git/") ||
                    normalized.Contains("/Temp/") || normalized.Contains("/obj/") ||
                    normalized.Contains("/bin/"))
                    continue;
                result.Add(Path.GetFullPath(file));
                if (result.Count > MaxFiles)
                    return;
            }
        }

        private static IEnumerable<string> ReadChangedPaths(string projectRoot,
            string gitBase, ICollection<string> errors)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string status = RunGit(projectRoot,
                "status --porcelain=v1 --untracked-files=all", errors);
            foreach (string line in status.Split(new[] { '\r', '\n' },
                         StringSplitOptions.RemoveEmptyEntries))
            {
                string path = line.Length > 3 ? line.Substring(3).Trim() : "";
                int arrow = path.LastIndexOf(" -> ", StringComparison.Ordinal);
                if (arrow >= 0)
                    path = path.Substring(arrow + 4);
                if (path.Length > 1 && path[0] == '"' && path[path.Length - 1] == '"')
                    path = path.Substring(1, path.Length - 2);
                if (!string.IsNullOrWhiteSpace(path))
                    result.Add(path.Replace('\\', '/'));
            }
            if (!string.IsNullOrWhiteSpace(gitBase))
            {
                string diff = RunGit(projectRoot,
                    $"diff --name-only {QuoteGitArgument(gitBase.Trim())}...HEAD --", errors);
                foreach (string path in diff.Split(new[] { '\r', '\n' },
                             StringSplitOptions.RemoveEmptyEntries))
                    result.Add(path.Trim().Replace('\\', '/'));
            }
            return result;
        }

        private static string RunGit(string projectRoot, string arguments,
            ICollection<string> errors)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = $"-C {QuoteGitArgument(projectRoot)} {arguments}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                using (Process process = Process.Start(startInfo))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    if (process.ExitCode != 0)
                        errors.Add($"git {arguments} failed: {error.Trim()}");
                    return process.ExitCode == 0 ? output : "";
                }
            }
            catch (Exception exception)
            {
                errors.Add($"Failed to inspect Git changes: {exception.Message}");
                return "";
            }
        }

        private static string QuoteGitArgument(string value)
        {
            return "\"" + (value ?? "").Replace("\"", "\\\"") + "\"";
        }

        private static string ToProjectPath(string projectRoot, string fullPath)
        {
            string relative = fullPath.Substring(projectRoot.TrimEnd('\\', '/').Length)
                .TrimStart('\\', '/');
            return relative.Replace('\\', '/');
        }

        private static string NormalizeProjectPath(string value)
        {
            return (value ?? "").Replace('\\', '/').TrimStart('.', '/');
        }

        private static string RemoveWhitespace(string value)
        {
            return new string((value ?? "").Where(character => !char.IsWhiteSpace(character))
                .ToArray());
        }

        private static Exception Unwrap(Exception exception)
        {
            return exception is TargetInvocationException invocation &&
                   invocation.InnerException != null
                ? invocation.InnerException
                : exception;
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
            private readonly string assetPath;
            private readonly int line;
            private readonly int column;
            private readonly string rule;
            private readonly string message;

            internal ReviewIssue(string assetPath, int line, int column, string rule,
                string message)
            {
                this.assetPath = assetPath;
                this.line = line;
                this.column = column;
                this.rule = rule;
                this.message = message;
            }

            internal Dictionary<string, object> ToDictionary()
            {
                return new Dictionary<string, object>
                {
                    { "assetPath", assetPath },
                    { "line", line },
                    { "column", column },
                    { "rule", rule },
                    { "severity", "error" },
                    { "message", message }
                };
            }
        }
    }
}
#endif
