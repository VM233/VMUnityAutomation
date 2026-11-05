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
        private readonly HashSet<object> publicationCallbacks = new();
        private readonly Dictionary<string, VmUiTransitionSourceEdge> calls = new(StringComparer.Ordinal);
        private readonly Dictionary<object, string> treePaths = new();
        private readonly VmUiTransitionDriverContract drivers = new();
        private VmUiTransitionStyleSources styles;
        private int interfaceMembers;
        private readonly HashSet<string> declaredTypes = new(StringComparer.Ordinal);
        private readonly Dictionary<string, object> interfaceTargets = new(StringComparer.Ordinal);
        private readonly Dictionary<string, List<(object Type, object Interface, VmUiTransitionSourceSite Site)>> interfaceTypes = new(StringComparer.Ordinal);

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
            styles = new VmUiTransitionStyleSources(project, request.StyleRoots, token);
            report.StyleRoots = request.StyleRoots; report.StyleSources = styles.Text.Keys.OrderBy(value => value, StringComparer.Ordinal).ToArray();
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
                    if (kind is "ClassDeclarationSyntax" or "StructDeclarationSyntax" or "RecordDeclarationSyntax") Interfaces(pair.Key, node, model, semantic);
                    else if (kind == "MethodDeclarationSyntax") Override(pair.Key, node, model, semantic);
                    else if (kind == "InvocationExpressionSyntax") Invocation(pair.Key, node, model, semantic);
                    else if (kind is "ObjectCreationExpressionSyntax" or "ImplicitObjectCreationExpressionSyntax") Creation(pair.Key, node, model, semantic);
                    else if (kind == "AssignmentExpressionSyntax") Assignment(pair.Key, node, model, semantic);
                    else if (kind is "IdentifierNameSyntax" or "MemberAccessExpressionSyntax") MethodReference(pair.Key, node, model, semantic);
                }
            }
            CompleteDispatch();
            var covered = VmUiTransitionPublicationGraph.Covered(report.Edges);
            foreach (var site in report.Sites)
            {
                if (site.Kind is "HierarchyMutation" or "Directdisplay" or "Directvisibility" or "PresentationClassChange" && covered.Contains(site.CallerSymbol)) site.Registered = true;
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
            styles.VerifyUnchanged(project);
            using (var sha = SHA256.Create())
            {
                string content = string.Join("\n", source.Concat(styles.Text).OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => pair.Key + "\n" + pair.Value));
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
            CallEdge(site, symbol, Convert.ToBoolean(VmUiCSharpSemanticContext.InterfaceProperty(symbol, "IsAsync")) ? "AsyncCall" : "Call");
            if (VmUiCSharpSemanticContext.InterfaceProperty(symbol, "MethodKind").Equals(delegateInvokeKind))
            { site.Kind = "DelegateInvocation"; report.DelegateCallSites.Add(site); }
            if (owner == "VM233.UIElements.UiPresentation")
            {
                if (method is not ("Display" or "Replace" or "Clear" or "Remove" or "Initialize")) return;
                site.Kind = method == "Display" ? "Visibility" : method == "Replace" ? "ContentReplacement" :
                    method == "Initialize" ? "Initialization" : "InitialOrRetiredChildren";
                site.Registered = true;
                site.Target = semantic.Argument(model, node, 0).ToString();
                object declaration = semantic.Argument(model, node, method == "Replace" ? 3 : method is "Clear" or "Remove" ? 1 : 2);
                if (declaration.GetType().Name != "LiteralExpressionSyntax" ||
                    VmAutomationCSharpSyntax.Property(VmAutomationCSharpSyntax.Property(declaration, "Token"), "ValueText").ToString() != site.Target)
                { site.Registered = false; site.Kind = "MismatchedSourceTarget"; }
                report.Sites.Add(site);
                if (method is "Replace" or "Initialize" && site.Registered)
                {
                    object callback = semantic.Argument(model, node, method == "Replace" ? 2 : 1);
                    object operation = semantic.Operation(model, callback);
                    object target = callback.GetType().Name is "SimpleLambdaExpressionSyntax" or "ParenthesizedLambdaExpressionSyntax" or "AnonymousMethodExpressionSyntax"
                        ? VmUiCSharpSemanticContext.InterfaceProperty(operation, "Symbol") : semantic.Symbol(model, callback);
                    if (target == null || Convert.ToBoolean(VmUiCSharpSemanticContext.InterfaceProperty(target, "IsAsync")))
                    { site.Registered = false; site.RegistrationIssue = "Initialization and replacement require an actual synchronous callback."; }
                    else { publicationCallbacks.Add(callback); CallEdge(site, target, method == "Replace" ? "PublicationCallback" : "InitializationCallback"); }
                }
            }
            else if (VmUiCSharpSemanticContext.IsVisualElement(symbol) && method is "Add" or "Clear" or "RemoveFromHierarchy" or "RemoveAt" or "Insert")
            {
                site.Kind = "HierarchyMutation"; site.Target = expression.ToString();
                site.Registered = drivers.Read(node, model, semantic, site) || OwnConstruction(node, symbol);
                report.Sites.Add(site);
            }
            else if (VmUiCSharpSemanticContext.IsVisualElement(symbol) && method is "AddToClassList" or "RemoveFromClassList" or "EnableInClassList" or "ToggleInClassList")
            {
                object constant = semantic.Constant(model, semantic.Argument(model, node, 0));
                bool known = Convert.ToBoolean(VmAutomationCSharpSyntax.Property(constant, "HasValue"));
                if (known)
                {
                    string value = VmAutomationCSharpSyntax.Property(constant, "Value") as string;
                    if (value == null) return;
                    site.StyleSources = styles.For(value);
                    if (site.StyleSources.Length == 0) return;
                }
                site.Kind = "PresentationClassChange"; site.Target = expression.ToString();
                site.Registered = drivers.Read(node, model, semantic, site) || OwnConstruction(node, symbol);
                if (!known && !site.Registered) site.RegistrationIssue = "The dynamic class may change a declared presentation selector.";
                report.Sites.Add(site);
            }
            else if (method is "Open" or "TryOpen" or "Close" or "TryClose" &&
                (owner.Contains("UIPanel") || ImplementsPanel(symbol)))
            {
                site.Kind = "PanelNavigation"; site.Target = expression.ToString();
                site.Registered = drivers.Read(node, model, semantic, site);
                report.Sites.Add(site);
            }
        }

        private void MethodReference(string path, object node, object model, VmUiCSharpSemanticContext semantic)
        {
            if (publicationCallbacks.Contains(node)) return;
            object parent = VmAutomationCSharpSyntax.Property(node, "Parent");
            if (node.GetType().Name == "IdentifierNameSyntax" && parent?.GetType().Name == "MemberAccessExpressionSyntax") return;
            object operation = semantic.Operation(model, node);
            if (operation == null || VmUiCSharpSemanticContext.InterfaceProperty(operation, "Kind").ToString() != "MethodReference") return;
            object target = VmUiCSharpSemanticContext.InterfaceProperty(operation, "Method");
            CallEdge(Site(path, node, model, target.ToString()), target, "DeferredCallback");
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
            if (symbol == null) return;
            string property = VmUiCSharpSemanticContext.InterfaceProperty(symbol, "Name").ToString();
            string owner = VmUiCSharpSemanticContext.ContainingType(symbol);
            bool style = owner == "UnityEngine.UIElements.IStyle" && property is "display" or "visibility";
            bool document = owner == "UnityEngine.UIElements.UIDocument" && property == "visualTreeAsset";
            if (owner == "UnityEngine.Behaviour" && property == "enabled")
            {
                object operation = semantic.Operation(model, left);
                object instance = VmUiCSharpSemanticContext.InterfaceProperty(operation, "Instance");
                document = instance != null && VmUiCSharpSemanticContext.InterfaceProperty(instance, "Type").ToString() == "UnityEngine.UIElements.UIDocument";
            }
            if (!style && !document) return;
            var site = Site(path, node, model, symbol.ToString());
            site.Kind = document ? "DocumentLifecycle" : "Direct" + property; site.Target = left.ToString();
            site.Registered = drivers.Read(node, model, semantic, site);
            report.Sites.Add(site);
        }

        private VmUiTransitionSourceSite Site(string path, object node, object model, string symbol)
        {
            int start = Convert.ToInt32(VmAutomationCSharpSyntax.Property(VmAutomationCSharpSyntax.Property(node, "Span"), "Start"));
            object enclosing = VmAutomationCSharpSyntax.Invoke(model, "GetEnclosingSymbol", start);
            int index = Array.BinarySearch(lines[path], start);
            int line = index >= 0 ? index + 1 : ~index;
            int column = start - lines[path][line - 1] + 1;
            string caller = SymbolIdentity(enclosing);
            return new VmUiTransitionSourceSite { ID = path + ":" + line + ":" + column, Path = path, Line = line, Column = column,
                Owner = VmUiCSharpSemanticContext.ContainingType(enclosing), Member = VmUiCSharpSemanticContext.InterfaceProperty(enclosing, "Name").ToString(),
                CallerSymbol = caller, Expression = node.ToString(), Symbol = symbol };
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
            object type = VmUiCSharpSemanticContext.InterfaceProperty(target, "ContainingType");
            if (kind != "InterfaceDispatch" && VmUiCSharpSemanticContext.InterfaceProperty(type, "TypeKind").ToString() == "Interface")
                interfaceTargets[called] = target;
            if (!calls.TryGetValue(identity, out var edge))
            {
                RequireEdgeBudget();
                edge = new VmUiTransitionSourceEdge { From = site.CallerSymbol, To = called, FromOwner = site.Owner,
                    ToOwner = VmUiCSharpSemanticContext.ContainingType(target), Kind = kind };
                calls.Add(identity, edge); report.Edges.Add(edge);
            }
            edge.Locations.Add(Location(site));
        }

        private void Override(string path, object node, object model, VmUiCSharpSemanticContext semantic)
        {
            object declared = semantic.DeclaredSymbol(model, node);
            object overridden = VmUiCSharpSemanticContext.InterfaceProperty(declared, "OverriddenMethod");
            if (overridden == null) return;
            var site = Site(path, node, model, declared.ToString());
            site.CallerSymbol = SymbolIdentity(overridden);
            site.Owner = VmUiCSharpSemanticContext.ContainingType(overridden);
            CallEdge(site, declared, "VirtualDispatch");
        }

        private void Interfaces(string path, object node, object model, VmUiCSharpSemanticContext semantic)
        {
            object declared = semantic.DeclaredSymbol(model, node);
            if (!declaredTypes.Add(SymbolIdentity(declared))) return;
            if (declaredTypes.Count > 4096) throw new InvalidOperationException("UI dispatch exceeds4096 declared types.");
            var interfaces = ((IEnumerable)VmUiCSharpSemanticContext.InterfaceProperty(declared, "AllInterfaces")).Cast<object>().ToArray();
            if (interfaces.Length > 64) throw new InvalidOperationException("UI dispatch exceeds64 interfaces per declared type.");
            foreach (object type in interfaces)
            {
                string key = SymbolIdentity(type);
                if (!interfaceTypes.TryGetValue(key, out var entries)) interfaceTypes.Add(key, entries = new());
                entries.Add((declared, type, Site(path, node, model, declared.ToString())));
            }
        }

        private void CompleteDispatch()
        {
            foreach (var target in interfaceTargets)
            {
                object containing = VmUiCSharpSemanticContext.InterfaceProperty(target.Value, "ContainingType");
                if (!interfaceTypes.TryGetValue(SymbolIdentity(containing), out var types)) continue;
                string name = VmUiCSharpSemanticContext.InterfaceProperty(target.Value, "Name").ToString();
                foreach (var entry in types)
                {
                    if (++interfaceMembers > 12000) throw new InvalidOperationException("UI dispatch exceeds12000 interface-member resolutions.");
                    token.ThrowIfCancellationRequested();
                    var membersContract = entry.Interface.GetType().GetInterfaces().Single(value => value.FullName == "Microsoft.CodeAnalysis.INamespaceOrTypeSymbol");
                    var candidates = ((IEnumerable)membersContract.GetMethod("GetMembers", new[] { typeof(string) })
                        .Invoke(entry.Interface, new object[] { name })).Cast<object>().Take(129).ToArray();
                    if (candidates.Length > 128) throw new InvalidOperationException("UI dispatch exceeds128 same-name interface members.");
                    object member = candidates.Single(value => SymbolIdentity(value) == target.Key);
                    var contract = entry.Type.GetType().GetInterfaces().Single(type => type.FullName == "Microsoft.CodeAnalysis.ITypeSymbol");
                    object implementation = contract.GetMethod("FindImplementationForInterfaceMember").Invoke(entry.Type, new[] { member });
                    if (implementation == null) continue;
                    if (SymbolIdentity(implementation) == target.Key) continue;
                    var site = new VmUiTransitionSourceSite { CallerSymbol = target.Key, Owner = VmUiCSharpSemanticContext.ContainingType(member),
                        ID = entry.Site.ID, Path = entry.Site.Path, Line = entry.Site.Line, Column = entry.Site.Column };
                    CallEdge(site, implementation, "InterfaceDispatch");
                }
            }
        }

        private static bool OwnConstruction(object node, object called)
        {
            if (!IsConstructor(node) || !VmUiCSharpSemanticContext.IsVisualElement(called)) return false;
            object expression = VmAutomationCSharpSyntax.Property(node, "Expression");
            if (expression.GetType().Name == "IdentifierNameSyntax") return true;
            if (expression.GetType().Name != "MemberAccessExpressionSyntax") return false;
            return VmAutomationCSharpSyntax.Property(expression, "Expression").GetType().Name is "ThisExpressionSyntax" or "BaseExpressionSyntax";
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

        private static bool IsConstructor(object node)
        {
            for (object current = node; current != null; current = VmAutomationCSharpSyntax.Property(current, "Parent"))
            {
                if (current.GetType().Name == "ConstructorDeclarationSyntax") return true;
                if (current.GetType().Name is "MethodDeclarationSyntax" or "SimpleLambdaExpressionSyntax" or "ParenthesizedLambdaExpressionSyntax" or "AnonymousMethodExpressionSyntax") return false;
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
