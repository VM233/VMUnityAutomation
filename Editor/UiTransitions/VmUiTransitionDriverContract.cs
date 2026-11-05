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
        private readonly Dictionary<object, Declaration> callableDeclarations = new();

        internal bool ReadCallable(object symbol, VmUiTransitionSourceSite site)
        {
            if (!IsTask(VmUiCSharpSemanticContext.InterfaceProperty(symbol, "ReturnType"))) return false;
            if (!callableDeclarations.TryGetValue(symbol, out var declaration))
            {
                if (callableDeclarations.Count == 2048) throw new InvalidOperationException("Native UI callbacks exceed2048 callable descriptors.");
                object type = VmUiCSharpSemanticContext.InterfaceProperty(symbol, "ContainingType");
                declaration = ReadAttributes(symbol, type) ?? ReadAttributes(type, type) ?? new Declaration();
                callableDeclarations.Add(symbol, declaration);
            }
            site.DriverPhaseMember = declaration.Phase; site.DriverRetirementMember = declaration.Retirement;
            site.RegistrationIssue = declaration.Error;
            return declaration.Valid;
        }

        private static Declaration ReadAttributes(object symbol, object type)
        {
            var contract = symbol.GetType().GetInterfaces().Single(value => value.FullName == "Microsoft.CodeAnalysis.ISymbol");
            var attributes = ((IEnumerable)contract.GetMethod("GetAttributes").Invoke(symbol, null)).Cast<object>().Take(33).ToArray();
            if (attributes.Length > 32) throw new InvalidOperationException("Native UI callable exceeds32 attributes.");
            foreach (object attribute in attributes)
            {
                Type attributeContract = attribute.GetType();
                int depth = 0;
                while (attributeContract != null && attributeContract.FullName != "Microsoft.CodeAnalysis.AttributeData")
                {
                    if (++depth > 16) throw new InvalidOperationException("Native attribute contract exceeds16 base types.");
                    attributeContract = attributeContract.BaseType;
                }
                if (attributeContract == null) throw new InvalidOperationException("The compiler did not expose its public AttributeData contract.");
                object attributeClass = attributeContract.GetProperty("AttributeClass").GetValue(attribute);
                if (attributeClass?.ToString() != "VM233.UIElements.UiTransitionDriverAttribute") continue;
                var arguments = ((IEnumerable)attributeContract.GetProperty("ConstructorArguments").GetValue(attribute)).Cast<object>().ToArray();
                if (arguments.Length != 2) return new Declaration { Error = "A native driver requires its actual phase and retirement members." };
                return ResolveMembers(type, VmAutomationCSharpSyntax.Property(arguments[0], "Value") as string,
                    VmAutomationCSharpSyntax.Property(arguments[1], "Value") as string);
            }
            return null;
        }

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
            var arguments = ((IEnumerable)VmAutomationCSharpSyntax.Property(VmAutomationCSharpSyntax.Property(attribute, "ArgumentList"), "Arguments")).Cast<object>().ToArray();
            if (arguments.Length != 2) return new Declaration { Error = "A native driver requires its actual phase and retirement members." };
            string phase = Constant(arguments[0]), retirement = Constant(arguments[1]);
            int start = Convert.ToInt32(VmAutomationCSharpSyntax.Property(VmAutomationCSharpSyntax.Property(node, "Span"), "Start"));
            object enclosing = VmAutomationCSharpSyntax.Invoke(model, "GetEnclosingSymbol", start);
            object type = VmUiCSharpSemanticContext.InterfaceProperty(enclosing, "ContainingType");
            return ResolveMembers(type, phase, retirement);

            string Constant(object argument)
            {
                object expression = VmAutomationCSharpSyntax.Property(argument, "Expression");
                object value = semantic.Constant(model, expression);
                return Convert.ToBoolean(VmAutomationCSharpSyntax.Property(value, "HasValue"))
                    ? VmAutomationCSharpSyntax.Property(value, "Value") as string : null;
            }
        }

        private static Declaration ResolveMembers(object type, string phase, string retirement)
        {
            var result = new Declaration { Phase = phase, Retirement = retirement };
            if (string.IsNullOrEmpty(phase) || string.IsNullOrEmpty(retirement) || phase.Length > 128 || retirement.Length > 128)
            { result.Error = "Native phase/retirement names must be bounded constant strings."; return result; }
            var phases = Members(type, phase).Where(IsPhase).ToArray();
            var retirements = Members(type, retirement).Where(IsRetirement).ToArray();
            if (phases.Length != 1 || retirements.Length != 1)
            { result.Error = "Native driver must resolve one bool/number/enum phase and one zero-argument retirement operation or Task completion property: " + phase + "/" + retirement; return result; }
            result.Valid = true;
            return result;
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
                    IsTask(VmUiCSharpSemanticContext.InterfaceProperty(symbol, "ReturnType")));
            return kind == "Property" && IsTask(VmUiCSharpSemanticContext.InterfaceProperty(symbol, "Type"));
        }

        internal static bool IsTask(object type)
        {
            int depth = 0;
            while (type != null && VmUiCSharpSemanticContext.InterfaceProperty(type, "TypeKind").ToString() == "Class")
            {
                if (++depth > 64) throw new InvalidOperationException("Native Task identity exceeds64 base types.");
                if (VmUiCSharpSemanticContext.InterfaceProperty(type, "Name").ToString() == "Task" &&
                    VmUiCSharpSemanticContext.InterfaceProperty(type, "ContainingNamespace").ToString() == "System.Threading.Tasks") return true;
                type = VmUiCSharpSemanticContext.InterfaceProperty(type, "BaseType");
            }
            return false;
        }
    }
}
