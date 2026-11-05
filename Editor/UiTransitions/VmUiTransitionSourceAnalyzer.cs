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
        private readonly VmUiNativeCompilationSnapshot nativeSnapshot;
        private readonly VmUiTransitionSourceProtocol protocol;
        private readonly object methodSymbolKind, anonymousFunctionKind, localFunctionKind, delegateInvokeKind;
        private readonly VmUiTransitionSourceReport report = new();
        private readonly Dictionary<string, string> source = new(StringComparer.Ordinal);
        private readonly Dictionary<string, object> trees = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int[]> lines = new(StringComparer.Ordinal);
        private readonly HashSet<string> registeredMethods = new(StringComparer.Ordinal);
        private readonly Dictionary<string, VmUiTransitionSourceEdge> calls = new(StringComparer.Ordinal);
        private readonly Dictionary<object, string> treePaths = new();
        private readonly VmUiTransitionDriverContract drivers = new();

        internal VmUiTransitionSourceAnalyzer(VmUiTransitionSourceRequest request, string project,
            Assembly csharp, Assembly core, object options, string[] references, CancellationToken token, VmUiNativeCompilationSnapshot nativeSnapshot,
            VmUiTransitionSourceProtocol protocol)
        {
            this.request = request; this.project = project; this.csharp = csharp; this.core = core;
            this.options = options; this.references = references; this.token = token;
            this.nativeSnapshot = nativeSnapshot;
            this.protocol = protocol;
            if ((request.SemanticMode == VmUiTransitionSemanticMode.NativeAssemblies) != (nativeSnapshot != null))
                throw new ArgumentException("UI semantic mode must match its actual native or declared-fixture context.");
            methodSymbolKind = Enum.Parse(core.GetType("Microsoft.CodeAnalysis.SymbolKind", true), "Method");
            var methodKind = core.GetType("Microsoft.CodeAnalysis.MethodKind", true);
            anonymousFunctionKind = Enum.Parse(methodKind, "AnonymousFunction");
            localFunctionKind = Enum.Parse(methodKind, "LocalFunction");
            delegateInvokeKind = Enum.Parse(methodKind, "DelegateInvoke");
        }

        internal VmUiTransitionSourceReport Run()
        {
            report.SourceRoots = request.SourceRoots; report.ExcludeRoots = request.ExcludeRoots;
            report.RuntimeProtocol = protocol;
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
            var compilations = new VmUiTransitionCompilationSet(project, paths.Select(Absolute).ToArray(), csharp, core, options,
                references, nativeSnapshot, token);
            report.SemanticMode = request.SemanticMode.ToString(); report.SemanticAssemblies = compilations.AssemblyNames;
            report.SemanticContextSha256 = compilations.Sha256; report.SemanticSourceFiles = compilations.FileCount;
            report.SemanticSourceBytes = compilations.SourceBytes; report.SemanticSyntaxNodes = compilations.SyntaxNodes;
            report.SemanticReferences = compilations.ReferenceCount; report.SemanticReferenceBytes = compilations.ReferenceBytes;
            report.SemanticReferenceViewBytes = compilations.ReferenceViewBytes;
            foreach (var pair in compilations.TreePaths) treePaths.Add(pair.Key, pair.Value);
            long bytes = 0;
            foreach (string path in paths.OrderBy(value => value, StringComparer.Ordinal))
            {
                token.ThrowIfCancellationRequested();
                var info = new FileInfo(Absolute(path)); bytes += info.Length;
                if (bytes > 16L * 1024 * 1024) throw new InvalidOperationException("UI source graph exceeds16MiB.");
                string text = compilations.Text(info.FullName); source.Add(path, text);
                var starts = new List<int> { 0 };
                for (int i = 0; i < text.Length; i++) if (text[i] == '\n') starts.Add(i + 1);
                lines.Add(path, starts.ToArray());
                object tree = compilations.Tree(info.FullName);
                foreach (object diagnostic in (IEnumerable)VmAutomationCSharpSyntax.Invoke(tree, "GetDiagnostics"))
                    if (VmAutomationCSharpSyntax.Property(diagnostic, "Severity").ToString() == "Error")
                        report.Issues.Add(path + " syntax error: " + diagnostic);
                trees.Add(path, tree);
            }
            report.ScannedFiles = trees.Count;
            foreach (var pair in trees.OrderBy(value => value.Key, StringComparer.Ordinal))
            {
                token.ThrowIfCancellationRequested();
                var semantic = compilations.Semantic(Absolute(pair.Key));
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
                if (site.Kind == "HierarchyMutation" && registeredMethods.Contains(site.CallerSymbol)) site.Registered = true;
                if (site.Kind is "Directdisplay" or "Directvisibility")
                {
                    StateEdge(site, "hidden", "shown"); StateEdge(site, "shown", "hidden");
                }
                else if (site.Kind is "Visibility" or "ContentReplacement")
                {
                    var arcs = site.Kind == "Visibility" ? protocol.Visibility : protocol.Content;
                    if (arcs.Length == 0) { site.Registered = false; site.RegistrationIssue = "The loaded runtime has no current phase protocol."; }
                    foreach (var arc in arcs) StateEdge(site, arc.From, arc.To);
                }
            }
            SelectScope();
            report.UnresolvedCalls = report.UnresolvedCallSites.Count;
            report.CallGraphComplete = report.UnresolvedCalls == 0;
            foreach (var site in report.UnresolvedCallSites)
                report.Issues.Add(site.ID + " unresolved " + site.Kind + ": " + site.Expression);
            foreach (var site in report.Sites.Where(value => !value.Registered))
                report.Issues.Add(site.ID + " unregistered " + site.Kind + " in " + site.Owner + "." + site.Member + ": " + site.Expression +
                    (string.IsNullOrEmpty(site.RegistrationIssue) ? "" : " | " + site.RegistrationIssue));
            compilations.VerifyUnchanged();
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
            object binding = semantic.Binding(model, node);
            object symbol = VmAutomationCSharpSyntax.Property(binding, "Symbol");
            string name = VmUiCSharpSemanticContext.SymbolName(symbol);
            if (symbol == null)
            {
                if (semantic.IsNameOf(model, node)) { report.CompilerIntrinsicCalls++; return; }
                var unresolved = UnresolvedSite(path, node, model, binding, "Call");
                report.UnresolvedCallSites.Add(unresolved);
                return;
            }
            report.ResolvedCalls++;
            string owner = VmUiCSharpSemanticContext.ContainingType(symbol);
            string method = VmUiCSharpSemanticContext.InterfaceProperty(symbol, "Name").ToString();
            var site = Site(path, node, model, name);
            CallEdge(site, symbol, "Call");
            if (VmUiCSharpSemanticContext.InterfaceProperty(symbol, "MethodKind").Equals(delegateInvokeKind))
            { site.Kind = "DelegateInvocation"; report.DelegateCallSites.Add(site); }
            if (owner == "VM233.UIElements.UiPresentation")
            {
                if (method is not ("Display" or "Replace" or "Clear" or "Remove")) return;
                site.Kind = method == "Display" ? "Visibility" : method == "Replace" ? "ContentReplacement" : "InitialOrRetiredChildren";
                site.Registered = true;
                object arguments = VmAutomationCSharpSyntax.Property(VmAutomationCSharpSyntax.Property(node, "ArgumentList"), "Arguments");
                var values = ((IEnumerable)arguments).Cast<object>().ToArray();
                site.Target = VmAutomationCSharpSyntax.Property(values[0], "Expression").ToString();
                object declaration = VmAutomationCSharpSyntax.Property(values[method == "Replace" ? 3 : method is "Clear" or "Remove" ? 1 : 2], "Expression");
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
                        if (target != null)
                        {
                            registeredMethods.Add(SymbolIdentity(target));
                            CallEdge(site, target, "PublicationCallback");
                        }
                    }
                }
            }
            else if (VmUiCSharpSemanticContext.IsVisualElement(symbol) && method is "Clear" or "RemoveFromHierarchy" or "RemoveAt" or "Insert")
            {
                site.Kind = "HierarchyMutation"; site.Target = expression.ToString();
                site.Registered = drivers.Read(node, model, semantic, site) || EnclosedReplacement(node, model, semantic) || IsConstructor(node);
                report.Sites.Add(site);
            }
            else if (method is "Open" or "TryOpen" or "Close" or "TryClose" &&
                (owner.Contains("UIPanel") || ImplementsPanel(symbol)))
            {
                site.Kind = "PanelNavigation"; site.Target = expression.ToString();
                site.Registered = drivers.Read(node, model, semantic, site) || EnclosedReplacement(node, model, semantic);
                report.Sites.Add(site);
            }
        }

        private void Creation(string path, object node, object model, VmUiCSharpSemanticContext semantic)
        {
            object binding = semantic.Binding(model, node);
            object symbol = VmAutomationCSharpSyntax.Property(binding, "Symbol");
            if (symbol == null)
            {
                var unresolved = UnresolvedSite(path, node, model, binding, "Construction");
                report.UnresolvedCallSites.Add(unresolved); return;
            }
            report.ResolvedCalls++;
            var site = Site(path, node, model, symbol.ToString());
            CallEdge(site, symbol, "Construction");
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
            site.Registered = drivers.Read(node, model, semantic, site) || IsConstructor(node);
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
                CallerSymbol = SymbolIdentity(enclosing), Expression = node.ToString(), Symbol = symbol };
        }

        private VmUiTransitionSourceSite UnresolvedSite(string path, object node, object model, object binding, string kind)
        {
            var site = Site(path, node, model, ""); site.Kind = kind;
            site.CandidateReason = VmAutomationCSharpSyntax.Property(binding, "CandidateReason").ToString();
            var candidates = ((IEnumerable)VmAutomationCSharpSyntax.Property(binding, "CandidateSymbols")).Cast<object>().ToArray();
            if (candidates.Length > 128) throw new InvalidOperationException("An unresolved UI call exceeds128 candidate symbols.");
            site.CandidateSymbols = candidates.Select(VmUiCSharpSemanticContext.SymbolName).ToArray();
            return site;
        }

        private string SymbolIdentity(object symbol)
        {
            if (VmUiCSharpSemanticContext.InterfaceProperty(symbol, "Kind").Equals(methodSymbolKind))
            {
                object reduced = VmUiCSharpSemanticContext.InterfaceProperty(symbol, "ReducedFrom");
                if (reduced != null) symbol = reduced;
                object kind = VmUiCSharpSemanticContext.InterfaceProperty(symbol, "MethodKind");
                bool anonymous = kind.Equals(anonymousFunctionKind);
                if (anonymous || kind.Equals(localFunctionKind))
                {
                    var declarations = ((IEnumerable)VmUiCSharpSemanticContext.InterfaceProperty(symbol, "DeclaringSyntaxReferences")).Cast<object>().ToArray();
                    if (declarations.Length != 1) throw new InvalidOperationException("UI call graph requires one actual local-function declaration.");
                    var declaration = declarations[0];
                    string path = treePaths[VmAutomationCSharpSyntax.Property(declaration, "SyntaxTree")];
                    object span = VmAutomationCSharpSyntax.Property(declaration, "Span");
                    return SymbolIdentity(VmUiCSharpSemanticContext.InterfaceProperty(symbol, "ContainingSymbol")) +
                        "/" + (anonymous ? "AnonymousFunction" : "LocalFunction") + "@" + path + ":" + VmAutomationCSharpSyntax.Property(span, "Start");
                }
            }
            object definition = VmUiCSharpSemanticContext.InterfaceProperty(symbol, "OriginalDefinition");
            object assembly = VmUiCSharpSemanticContext.InterfaceProperty(definition, "ContainingAssembly");
            return VmUiCSharpSemanticContext.SymbolName(assembly) + "::" + definition;
        }

        private void CallEdge(VmUiTransitionSourceSite site, object target, string kind)
        {
            string called = SymbolIdentity(target), identity = kind + "\n" + site.CallerSymbol + "\n" + called;
            if (!calls.TryGetValue(identity, out var edge))
            {
                RequireEdgeBudget();
                edge = new VmUiTransitionSourceEdge { From = site.CallerSymbol, To = called, FromOwner = site.Owner,
                    ToOwner = VmUiCSharpSemanticContext.ContainingType(target), Kind = kind };
                calls.Add(identity, edge); report.Edges.Add(edge);
            }
            edge.Locations.Add(Location(site));
        }

        private static VmUiTransitionSourceLocation Location(VmUiTransitionSourceSite site) => new()
        { Site = site.ID, Path = site.Path, Line = site.Line, Column = site.Column };

        private void RequireEdgeBudget()
        {
            if (report.Edges.Count == 12000) throw new InvalidOperationException("UI source graph exceeds12000 structural call/transition edges.");
        }

        private static bool ImplementsPanel(object symbol)
        {
            object type = VmUiCSharpSemanticContext.InterfaceProperty(symbol, "ContainingType");
            return ((IEnumerable)VmUiCSharpSemanticContext.InterfaceProperty(type, "AllInterfaces")).Cast<object>()
                .Any(value => value.ToString() == "VMFramework.UI.IUIPanel");
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
            var methods = new HashSet<string>(report.Sites.Where(site => selected.Contains(site.Path)).Select(site => site.CallerSymbol), StringComparer.Ordinal);
            methods.UnionWith(report.Edges.Where(edge => edge.Kind != "StateTransition" && edge.Locations.Any(location => selected.Contains(location.Path))).Select(edge => edge.From));
            var methodPaths = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (var site in report.Sites)
            {
                string method = site.CallerSymbol;
                if (!methodPaths.TryGetValue(method, out var paths)) methodPaths.Add(method, paths = new HashSet<string>(StringComparer.Ordinal));
                paths.Add(site.Path);
            }
            foreach (var edge in report.Edges.Where(edge => edge.Kind != "StateTransition"))
            {
                if (!methodPaths.TryGetValue(edge.From, out var paths)) methodPaths.Add(edge.From, paths = new HashSet<string>(StringComparer.Ordinal));
                paths.UnionWith(edge.Locations.Select(location => location.Path));
            }
            foreach (var edge in report.Edges)
                if (methods.Contains(edge.From) || methods.Contains(edge.To))
                {
                    selected.UnionWith(edge.Locations.Select(location => location.Path));
                    if (methodPaths.TryGetValue(edge.To, out var targets)) selected.UnionWith(targets);
                }
            report.SelectedPaths = selected.OrderBy(value => value, StringComparer.Ordinal).ToArray();
            report.UnresolvedCallSites = report.UnresolvedCallSites.Where(site => selected.Contains(site.Path)).ToList();
            report.DelegateCallSites = report.DelegateCallSites.Where(site => selected.Contains(site.Path)).ToList();
            report.Sites = report.Sites.Where(site => selected.Contains(site.Path)).ToList();
            report.Edges = report.Edges.Where(edge => edge.Locations.Any(location => selected.Contains(location.Path)) || methods.Contains(edge.From) || methods.Contains(edge.To)).ToList();
        }

        private void StateEdge(VmUiTransitionSourceSite site, string from, string to)
        {
            RequireEdgeBudget();
            report.Edges.Add(new VmUiTransitionSourceEdge { From = site.ID + "/" + from, To = site.ID + "/" + to,
                FromOwner = site.Owner, ToOwner = site.Owner, Locations = new() { Location(site) }, Kind = "StateTransition" });
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
