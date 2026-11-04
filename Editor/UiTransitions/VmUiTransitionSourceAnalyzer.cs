using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace VMUnityAutomation.Editor
{
    internal sealed class VmUiTransitionSourceAnalyzer
    {
        private readonly VmUiTransitionSourceRequest request;
        private readonly string project;
        private readonly Assembly csharp, core;
        private readonly object options;
        private readonly string[] references;
        private readonly CancellationToken token;
        private readonly VmUiTransitionSourceReport report = new();
        private readonly Dictionary<string, string> source = new(StringComparer.Ordinal);
        private readonly Dictionary<string, object> trees = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int[]> lines = new(StringComparer.Ordinal);
        private readonly HashSet<string> registeredMethods = new(StringComparer.Ordinal);

        internal VmUiTransitionSourceAnalyzer(VmUiTransitionSourceRequest request, string project,
            Assembly csharp, Assembly core, object options, string[] references, CancellationToken token)
        {
            this.request = request; this.project = project; this.csharp = csharp; this.core = core;
            this.options = options; this.references = references; this.token = token;
        }

        internal VmUiTransitionSourceReport Run()
        {
            report.SourceRoots = request.SourceRoots; report.ExcludeRoots = request.ExcludeRoots;
            report.Coverage = request.AffectedPaths.Length == 0 ? "FullDeclaredSourceGraph" : "AffectedSourceGraphAndAdjacentCalls";
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string declared in request.SourceRoots)
            {
                string root = Absolute(declared);
                IEnumerable<string> files = File.Exists(root) ? new[] { root } : Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories);
                foreach (string file in files)
                {
                    token.ThrowIfCancellationRequested();
                    string path = Relative(file);
                    if (request.ExcludeRoots.Any(exclude => InRoot(path, exclude))) continue;
                    paths.Add(path);
                    if (paths.Count > 1024) throw new InvalidOperationException("UI source graph exceeds1024 files.");
                }
            }
            if (paths.Count == 0) throw new ArgumentException("The declared UI source graph is empty.");
            long bytes = 0;
            foreach (string path in paths.OrderBy(value => value, StringComparer.Ordinal))
            {
                token.ThrowIfCancellationRequested();
                var info = new FileInfo(Absolute(path)); bytes += info.Length;
                if (bytes > 16L * 1024 * 1024) throw new InvalidOperationException("UI source graph exceeds16MiB.");
                string text = File.ReadAllText(info.FullName); source.Add(path, text);
                var starts = new List<int> { 0 };
                for (int i = 0; i < text.Length; i++) if (text[i] == '\n') starts.Add(i + 1);
                lines.Add(path, starts.ToArray());
                object tree = VmAutomationCSharpSyntax.Parse(csharp, text, options);
                foreach (object diagnostic in (IEnumerable)VmAutomationCSharpSyntax.Invoke(tree, "GetDiagnostics"))
                    if (VmAutomationCSharpSyntax.Property(diagnostic, "Severity").ToString() == "Error")
                        report.Issues.Add(path + " syntax error: " + diagnostic);
                trees.Add(path, tree);
            }
            report.ScannedFiles = trees.Count;
            var semantic = new VmUiCSharpSemanticContext(csharp, core, trees.Values.ToArray(), references, token);
            foreach (var pair in trees.OrderBy(value => value.Key, StringComparer.Ordinal))
            {
                token.ThrowIfCancellationRequested();
                object root = VmAutomationCSharpSyntax.Invoke(pair.Value, "GetRoot");
                object model = semantic.Model(pair.Value);
                foreach (object node in VmAutomationCSharpSyntax.Nodes(root))
                {
                    if (++report.SyntaxNodes > 250000) throw new InvalidOperationException("UI source graph exceeds250000 syntax nodes.");
                    token.ThrowIfCancellationRequested();
                    string kind = node.GetType().Name;
                    if (kind == "InvocationExpressionSyntax") Invocation(pair.Key, node, model, semantic);
                    else if (kind is "ObjectCreationExpressionSyntax" or "ImplicitObjectCreationExpressionSyntax") Creation(pair.Key, node, model, semantic);
                    else if (kind == "AssignmentExpressionSyntax") Assignment(pair.Key, node, model, semantic);
                }
            }
            foreach (var site in report.Sites)
            {
                if (site.Kind == "HierarchyMutation" && registeredMethods.Contains(site.Owner + "." + site.Member)) site.Registered = true;
                if (site.Kind is "Visibility" or "Directdisplay" or "Directvisibility")
                {
                    StateEdge(site, "hidden", "shown"); StateEdge(site, "shown", "hidden");
                }
                else if (site.Kind == "ContentReplacement")
                {
                    StateEdge(site, "published", "outgoing"); StateEdge(site, "outgoing", "publication");
                    StateEdge(site, "publication", "incoming"); StateEdge(site, "incoming", "published");
                }
            }
            SelectScope();
            foreach (var site in report.Sites.Where(value => !value.Registered))
                report.Issues.Add(site.ID + " unregistered " + site.Kind + " in " + site.Owner + "." + site.Member + ": " + site.Expression);
            foreach (var pair in source)
                if (File.ReadAllText(Absolute(pair.Key)) != pair.Value) throw new InvalidOperationException("Source changed during UI graph analysis: " + pair.Key);
            using (var sha = SHA256.Create())
            {
                string content = string.Join("\n", source.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Key + "\n" + pair.Value));
                report.SourceSha256 = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(content))).Replace("-", "").ToLowerInvariant();
            }
            report.Complete = true; report.SelectedPassed = report.Issues.Count == 0;
            report.Passed = request.AffectedPaths.Length == 0 && report.SelectedPassed;
            return report;
        }

        private void Invocation(string path, object node, object model, VmUiCSharpSemanticContext semantic)
        {
            object expression = VmAutomationCSharpSyntax.Property(node, "Expression");
            object symbol = semantic.Symbol(model, expression);
            string name = VmUiCSharpSemanticContext.SymbolName(symbol);
            if (symbol == null)
            {
                report.UnresolvedCalls++;
                string unresolved = expression.ToString();
                if (unresolved.EndsWith(".Open", StringComparison.Ordinal) || unresolved.EndsWith(".Close", StringComparison.Ordinal) ||
                    unresolved.EndsWith(".Clear", StringComparison.Ordinal) || unresolved.StartsWith("UiPresentation.", StringComparison.Ordinal))
                    report.Issues.Add(path + " unresolved presentation call: " + unresolved);
                return;
            }
            report.ResolvedCalls++;
            string owner = VmUiCSharpSemanticContext.ContainingType(symbol);
            string method = VmUiCSharpSemanticContext.InterfaceProperty(symbol, "Name").ToString();
            var site = Site(path, node, model, name);
            string caller = site.Owner + "." + site.Member;
            if (report.Edges.Count == 12000) throw new InvalidOperationException("UI source graph exceeds12000 call/transition edges.");
            report.Edges.Add(new VmUiTransitionSourceEdge { From = caller, To = owner + "." + method, Site = site.ID, Path = path, Kind = "Call" });
            if (owner == "VM233.UIElements.UiPresentation")
            {
                if (method is not ("Display" or "Replace" or "Clear")) return;
                site.Kind = method == "Display" ? "Visibility" : method == "Replace" ? "ContentReplacement" : "InitialOrRetiredChildren";
                site.Registered = true;
                object arguments = VmAutomationCSharpSyntax.Property(VmAutomationCSharpSyntax.Property(node, "ArgumentList"), "Arguments");
                var values = ((IEnumerable)arguments).Cast<object>().ToArray();
                site.Target = VmAutomationCSharpSyntax.Property(values[0], "Expression").ToString();
                object declaration = VmAutomationCSharpSyntax.Property(values[method == "Replace" ? 3 : method == "Clear" ? 1 : 2], "Expression");
                if (declaration.GetType().Name != "LiteralExpressionSyntax" ||
                    VmAutomationCSharpSyntax.Property(VmAutomationCSharpSyntax.Property(declaration, "Token"), "ValueText").ToString() != site.Target)
                { site.Registered = false; site.Kind = "MismatchedSourceTarget"; }
                report.Sites.Add(site);
                if (method == "Replace")
                {
                    object callback = VmAutomationCSharpSyntax.Property(values[2], "Expression");
                    if (callback.GetType().Name is "IdentifierNameSyntax" or "MemberAccessExpressionSyntax")
                    {
                        object target = semantic.Symbol(model, callback);
                        if (target != null) registeredMethods.Add(VmUiCSharpSemanticContext.ContainingType(target) + "." + VmUiCSharpSemanticContext.InterfaceProperty(target, "Name"));
                    }
                }
            }
            else if (VmUiCSharpSemanticContext.IsVisualElement(symbol) && method is "Clear" or "RemoveFromHierarchy" or "RemoveAt" or "Insert")
            {
                site.Kind = "HierarchyMutation"; site.Target = expression.ToString();
                site.Registered = NativeDriver(node, model, semantic) || EnclosedReplacement(node, model, semantic) || IsConstructor(node);
                report.Sites.Add(site);
            }
            else if (method is "Open" or "TryOpen" or "Close" or "TryClose" &&
                (owner.Contains("UIPanel") || ImplementsPanel(symbol)))
            {
                site.Kind = "PanelNavigation"; site.Target = expression.ToString();
                site.Registered = NativeDriver(node, model, semantic) || EnclosedReplacement(node, model, semantic);
                report.Sites.Add(site);
            }
        }

        private void Creation(string path, object node, object model, VmUiCSharpSemanticContext semantic)
        {
            object symbol = semantic.Symbol(model, node);
            if (symbol == null) { report.UnresolvedCalls++; return; }
            report.ResolvedCalls++;
            var site = Site(path, node, model, symbol.ToString());
            if (report.Edges.Count == 12000) throw new InvalidOperationException("UI source graph exceeds12000 call/transition edges.");
            report.Edges.Add(new VmUiTransitionSourceEdge { From = site.Owner + "." + site.Member,
                To = VmUiCSharpSemanticContext.ContainingType(symbol) + ".ctor", Site = site.ID, Path = path, Kind = "Construction" });
        }

        private void Assignment(string path, object node, object model, VmUiCSharpSemanticContext semantic)
        {
            object left = VmAutomationCSharpSyntax.Property(node, "Left");
            object symbol = semantic.Symbol(model, left);
            if (symbol == null || VmUiCSharpSemanticContext.ContainingType(symbol) != "UnityEngine.UIElements.IStyle") return;
            string property = VmUiCSharpSemanticContext.InterfaceProperty(symbol, "Name").ToString();
            if (property is not ("display" or "visibility")) return;
            var site = Site(path, node, model, symbol.ToString());
            site.Kind = "Direct" + property; site.Target = left.ToString();
            site.Registered = NativeDriver(node, model, semantic) || IsConstructor(node);
            report.Sites.Add(site);
        }

        private VmUiTransitionSourceSite Site(string path, object node, object model, string symbol)
        {
            int start = Convert.ToInt32(VmAutomationCSharpSyntax.Property(VmAutomationCSharpSyntax.Property(node, "Span"), "Start"));
            object enclosing = VmAutomationCSharpSyntax.Invoke(model, "GetEnclosingSymbol", start);
            int index = Array.BinarySearch(lines[path], start);
            int line = index >= 0 ? index + 1 : ~index;
            int column = start - lines[path][line - 1] + 1;
            return new VmUiTransitionSourceSite { ID = path + ":" + line + ":" + column, Path = path, Line = line, Column = column,
                Owner = VmUiCSharpSemanticContext.ContainingType(enclosing), Member = VmUiCSharpSemanticContext.InterfaceProperty(enclosing, "Name").ToString(),
                Expression = node.ToString(), Symbol = symbol };
        }

        private static bool ImplementsPanel(object symbol)
        {
            object type = VmUiCSharpSemanticContext.InterfaceProperty(symbol, "ContainingType");
            return ((IEnumerable)VmUiCSharpSemanticContext.InterfaceProperty(type, "AllInterfaces")).Cast<object>()
                .Any(value => value.ToString() == "VMFramework.UI.IUIPanel");
        }

        private static bool NativeDriver(object node, object model, VmUiCSharpSemanticContext semantic)
        {
            for (object current = node; current != null; current = VmAutomationCSharpSyntax.Property(current, "Parent"))
                if (current.GetType().Name is "ClassDeclarationSyntax" or "MethodDeclarationSyntax")
                {
                    foreach (object list in (IEnumerable)VmAutomationCSharpSyntax.Property(current, "AttributeLists"))
                        foreach (object attribute in (IEnumerable)VmAutomationCSharpSyntax.Property(list, "Attributes"))
                        {
                            object symbol = semantic.Symbol(model, attribute);
                            if (symbol != null && VmUiCSharpSemanticContext.ContainingType(symbol) == "VM233.UIElements.UiTransitionDriverAttribute") return true;
                        }
                }
            return false;
        }

        private static bool EnclosedReplacement(object node, object model, VmUiCSharpSemanticContext semantic)
        {
            for (object current = VmAutomationCSharpSyntax.Property(node, "Parent"); current != null; current = VmAutomationCSharpSyntax.Property(current, "Parent"))
            {
                if (current.GetType().Name == "MethodDeclarationSyntax") return false;
                if (current.GetType().Name == "InvocationExpressionSyntax")
                {
                    object symbol = semantic.Symbol(model, VmAutomationCSharpSyntax.Property(current, "Expression"));
                    if (symbol != null && VmUiCSharpSemanticContext.ContainingType(symbol) == "VM233.UIElements.UiPresentation" &&
                        VmUiCSharpSemanticContext.InterfaceProperty(symbol, "Name").ToString() == "Replace") return true;
                }
            }
            return false;
        }

        private static bool IsConstructor(object node)
        {
            for (object current = node; current != null; current = VmAutomationCSharpSyntax.Property(current, "Parent"))
            {
                if (current.GetType().Name == "ConstructorDeclarationSyntax") return true;
                if (current.GetType().Name == "MethodDeclarationSyntax") return false;
            }
            return false;
        }

        private void SelectScope()
        {
            if (request.AffectedPaths.Length == 0) { report.SelectedPaths = trees.Keys.ToArray(); return; }
            var selected = new HashSet<string>(trees.Keys.Where(path => request.AffectedPaths.Any(affected => InRoot(path, affected))), StringComparer.Ordinal);
            if (selected.Count == 0) throw new ArgumentException("Affected paths do not select any declared source.");
            var methods = new HashSet<string>(report.Sites.Where(site => selected.Contains(site.Path)).Select(site => site.Owner + "." + site.Member), StringComparer.Ordinal);
            methods.UnionWith(report.Edges.Where(edge => selected.Contains(edge.Path)).Select(edge => edge.From));
            var methodPaths = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (var site in report.Sites)
            {
                string method = site.Owner + "." + site.Member;
                if (!methodPaths.TryGetValue(method, out var paths)) methodPaths.Add(method, paths = new HashSet<string>(StringComparer.Ordinal));
                paths.Add(site.Path);
            }
            foreach (var edge in report.Edges)
                if (methods.Contains(edge.From) || methods.Contains(edge.To))
                {
                    selected.Add(edge.Path);
                    if (methodPaths.TryGetValue(edge.To, out var targets)) selected.UnionWith(targets);
                }
            report.SelectedPaths = selected.OrderBy(value => value, StringComparer.Ordinal).ToArray();
            report.Sites = report.Sites.Where(site => selected.Contains(site.Path)).ToList();
            report.Edges = report.Edges.Where(edge => selected.Contains(edge.Path) || methods.Contains(edge.From) || methods.Contains(edge.To)).ToList();
        }

        private void StateEdge(VmUiTransitionSourceSite site, string from, string to)
        {
            if (report.Edges.Count == 12000) throw new InvalidOperationException("UI source graph exceeds12000 call/transition edges.");
            report.Edges.Add(new VmUiTransitionSourceEdge { From = site.ID + "/" + from, To = site.ID + "/" + to,
                Path = site.Path, Site = site.ID, Kind = "StateTransition" });
        }

        private string Absolute(string path)
        {
            string absolute = Path.GetFullPath(Path.Combine(project, path));
            if (!absolute.StartsWith(project + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("UI source roots must remain in the bound project.");
            return absolute;
        }
        private string Relative(string path) => path.Substring(project.Length + 1).Replace('\\', '/');
        private static bool InRoot(string path, string root) => path == root.TrimEnd('/') || path.StartsWith(root.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase);
    }
}
