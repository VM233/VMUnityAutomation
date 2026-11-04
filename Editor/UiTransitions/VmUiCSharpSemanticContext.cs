using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace VMUnityAutomation.Editor
{
    internal sealed class VmUiCSharpSemanticContext
    {
        private readonly object compilation;
        private readonly Type extensions;
        private readonly CancellationToken token;

        internal VmUiCSharpSemanticContext(Assembly csharp, Assembly core, object[] trees, string[] references, CancellationToken token,
            string assemblyName = "UiTransitionFixture", bool allowUnsafe = false)
        {
            this.token = token;
            var syntaxType = core.GetType("Microsoft.CodeAnalysis.SyntaxTree", true);
            var referenceType = core.GetType("Microsoft.CodeAnalysis.MetadataReference", true);
            Array syntaxTrees = Array.CreateInstance(syntaxType, trees.Length);
            Array.Copy(trees, syntaxTrees, trees.Length);
            var createReference = referenceType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(method => method.Name == "CreateFromFile" && method.GetParameters()[0].ParameterType == typeof(string));
            Array metadata = Array.CreateInstance(referenceType, references.Length);
            for (int i = 0; i < references.Length; i++)
            {
                token.ThrowIfCancellationRequested();
                var args = createReference.GetParameters().Select(VmAutomationCSharpSyntax.Default).ToArray();
                args[0] = references[i]; metadata.SetValue(createReference.Invoke(null, args), i);
            }
            var output = core.GetType("Microsoft.CodeAnalysis.OutputKind", true);
            var optionsType = csharp.GetType("Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions", true);
            var constructor = optionsType.GetConstructors().OrderBy(item => item.GetParameters().Length).First();
            var optionsArgs = constructor.GetParameters().Select(VmAutomationCSharpSyntax.Default).ToArray();
            optionsArgs[0] = Enum.Parse(output, "DynamicallyLinkedLibrary");
            object options = constructor.Invoke(optionsArgs);
            options = VmAutomationCSharpSyntax.Invoke(options, "WithAllowUnsafe", allowUnsafe);
            var create = csharp.GetType("Microsoft.CodeAnalysis.CSharp.CSharpCompilation", true)
                .GetMethods(BindingFlags.Public | BindingFlags.Static).Single(method => method.Name == "Create" && method.GetParameters().Length == 4);
            compilation = create.Invoke(null, new object[] { assemblyName, syntaxTrees, metadata, options });
            extensions = csharp.GetType("Microsoft.CodeAnalysis.CSharp.CSharpExtensions", true);
        }

        internal object Model(object tree) => VmAutomationCSharpSyntax.Invoke(compilation, "GetSemanticModel", tree, true);

        internal bool IsNameOf(object model, object node)
        {
            object operation = VmAutomationCSharpSyntax.Invoke(model, "GetOperation", node, token);
            if (operation == null) return false;
            object kind = InterfaceProperty(operation, "Kind");
            return kind.Equals(Enum.Parse(kind.GetType(), "NameOf"));
        }

        internal object Symbol(object model, object expression) =>
            VmAutomationCSharpSyntax.Property(Binding(model, expression), "Symbol");

        internal object Binding(object model, object expression)
        {
            var method = extensions.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(item => item.Name == "GetSymbolInfo" && item.GetParameters().Length >= 2 &&
                    item.GetParameters()[1].ParameterType.IsInstanceOfType(expression))
                .OrderBy(item => item.GetParameters().Length).First();
            var args = method.GetParameters().Select(VmAutomationCSharpSyntax.Default).ToArray();
            args[0] = model; args[1] = expression;
            var parameters = method.GetParameters();
            for (int i = 2; i < parameters.Length; i++) if (parameters[i].ParameterType == typeof(CancellationToken)) args[i] = token;
            object info;
            try { info = method.Invoke(null, args); }
            catch (TargetInvocationException error) when (error.InnerException != null)
            { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
            return info;
        }

        internal static object InterfaceProperty(object value, string name)
        {
            var property = value.GetType().GetProperty(name) ?? value.GetType().GetInterfaces()
                .Select(type => type.GetProperty(name)).First(item => item != null);
            return property.GetValue(value);
        }

        internal static string SymbolName(object symbol) => symbol == null ? "" : symbol.ToString();

        internal static string ContainingType(object symbol)
        {
            var type = InterfaceProperty(symbol, "ContainingType");
            return type == null ? "" : type.ToString();
        }

        internal static bool IsVisualElement(object symbol)
        {
            var type = InterfaceProperty(symbol, "ContainingType");
            while (type != null)
            {
                if (type.ToString() == "UnityEngine.UIElements.VisualElement") return true;
                type = InterfaceProperty(type, "BaseType");
            }
            return false;
        }
    }
}
