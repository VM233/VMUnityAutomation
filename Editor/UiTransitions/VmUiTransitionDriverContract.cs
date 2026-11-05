using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace VMUnityAutomation.Editor
{
    internal sealed class VmUiTransitionDriverContract
    {
        private sealed class Declaration
        {
            internal bool Valid;
            internal string Phase, Retirement, Error;
        }
        private readonly Dictionary<object, Declaration> declarations = new();

        internal bool Read(object node, object model, VmUiCSharpSemanticContext semantic, VmUiTransitionSourceSite site)
        {
            int depth = 0;
            for (object current = node; current != null; current = VmAutomationCSharpSyntax.Property(current, "Parent"))
            {
                if (++depth > 128) throw new InvalidOperationException("Native driver source ancestry exceeds128 nodes.");
                if (current.GetType().Name is "ClassDeclarationSyntax" or "MethodDeclarationSyntax")
                    foreach (object list in (IEnumerable)VmAutomationCSharpSyntax.Property(current, "AttributeLists"))
                        foreach (object attribute in (IEnumerable)VmAutomationCSharpSyntax.Property(list, "Attributes"))
                        {
                            object symbol = semantic.Symbol(model, attribute);
                            if (symbol == null || VmUiCSharpSemanticContext.ContainingType(symbol) != "VM233.UIElements.UiTransitionDriverAttribute") continue;
                            if (!declarations.TryGetValue(attribute, out var declaration))
                            {
                                if (declarations.Count == 4096) throw new InvalidOperationException("UI graph exceeds4096 native driver declarations.");
                                declaration = Resolve(attribute, node, model, semantic); declarations.Add(attribute, declaration);
                            }
                            site.DriverPhaseMember = declaration.Phase; site.DriverRetirementMember = declaration.Retirement;
                            site.RegistrationIssue = declaration.Error;
                            return declaration.Valid;
                        }
            }
            return false;
        }

        private static Declaration Resolve(object attribute, object node, object model, VmUiCSharpSemanticContext semantic)
        {
            var result = new Declaration();
            var arguments = ((IEnumerable)VmAutomationCSharpSyntax.Property(VmAutomationCSharpSyntax.Property(attribute, "ArgumentList"), "Arguments")).Cast<object>().ToArray();
            if (arguments.Length != 2) { result.Error = "A native driver requires its actual phase and retirement members."; return result; }
            string phase = Constant(arguments[0]), retirement = Constant(arguments[1]);
            if (string.IsNullOrEmpty(phase) || string.IsNullOrEmpty(retirement) || phase.Length > 128 || retirement.Length > 128)
            { result.Error = "Native phase/retirement names must be bounded constant strings."; return result; }
            int start = Convert.ToInt32(VmAutomationCSharpSyntax.Property(VmAutomationCSharpSyntax.Property(node, "Span"), "Start"));
            object enclosing = VmAutomationCSharpSyntax.Invoke(model, "GetEnclosingSymbol", start);
            object type = VmUiCSharpSemanticContext.InterfaceProperty(enclosing, "ContainingType");
            var phases = Members(type, phase).Where(IsPhase).ToArray();
            var retirements = Members(type, retirement).Where(IsRetirement).ToArray();
            if (phases.Length != 1 || retirements.Length != 1)
            { result.Error = "Native driver must resolve one bool/number/enum phase and one zero-argument retirement operation or Task completion property: " + phase + "/" + retirement; return result; }
            result.Valid = true; result.Phase = phase; result.Retirement = retirement;
            return result;

            string Constant(object argument)
            {
                object expression = VmAutomationCSharpSyntax.Property(argument, "Expression");
                object value = semantic.Constant(model, expression);
                return Convert.ToBoolean(VmAutomationCSharpSyntax.Property(value, "HasValue"))
                    ? VmAutomationCSharpSyntax.Property(value, "Value") as string : null;
            }
        }

        private static object[] Members(object type, string name)
        {
            int depth = 0;
            while (type != null)
            {
                if (++depth > 64) throw new InvalidOperationException("Native driver membership exceeds64 base types.");
                var contract = type.GetType().GetInterfaces().Single(item => item.FullName == "Microsoft.CodeAnalysis.INamespaceOrTypeSymbol");
                var values = ((IEnumerable)contract.GetMethod("GetMembers", new[] { typeof(string) }).Invoke(type, new object[] { name })).Cast<object>().Take(129).ToArray();
                if (values.Length > 128) throw new InvalidOperationException("A native driver member exceeds128 overloads.");
                if (values.Length > 0) return values;
                type = VmUiCSharpSemanticContext.InterfaceProperty(type, "BaseType");
            }
            return Array.Empty<object>();
        }

        private static bool IsPhase(object symbol)
        {
            string kind = VmUiCSharpSemanticContext.InterfaceProperty(symbol, "Kind").ToString();
            if (kind is not ("Field" or "Property")) return false;
            object type = VmUiCSharpSemanticContext.InterfaceProperty(symbol, "Type");
            return VmUiCSharpSemanticContext.InterfaceProperty(type, "TypeKind").ToString() == "Enum" ||
                VmUiCSharpSemanticContext.InterfaceProperty(type, "SpecialType").ToString() is
                    "System_Boolean" or "System_Byte" or "System_Int16" or "System_Int32" or "System_Int64" or "System_Single" or "System_Double";
        }

        private static bool IsRetirement(object symbol)
        {
            string kind = VmUiCSharpSemanticContext.InterfaceProperty(symbol, "Kind").ToString();
            if (kind == "Method") return !((IEnumerable)VmUiCSharpSemanticContext.InterfaceProperty(symbol, "Parameters")).Cast<object>().Any() &&
                (Convert.ToBoolean(VmUiCSharpSemanticContext.InterfaceProperty(symbol, "ReturnsVoid")) ||
                    VmUiCSharpSemanticContext.InterfaceProperty(symbol, "ReturnType").ToString().StartsWith("System.Threading.Tasks.Task", StringComparison.Ordinal));
            return kind == "Property" && VmUiCSharpSemanticContext.InterfaceProperty(symbol, "Type").ToString().StartsWith("System.Threading.Tasks.Task", StringComparison.Ordinal);
        }
    }
}
