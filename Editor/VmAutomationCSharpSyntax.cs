using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationCSharpSyntax
    {
        internal static object Parse(Assembly csharp, string source, object options)
        {
            Type type = csharp.GetType("Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree", true);
            Type optionsType = csharp.GetType("Microsoft.CodeAnalysis.CSharp.CSharpParseOptions", true);
            var method = type.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(item => item.Name == "ParseText" && item.GetParameters().Length > 0 && item.GetParameters()[0].ParameterType == typeof(string))
                .OrderByDescending(item => item.GetParameters().Any(parameter => parameter.ParameterType == optionsType))
                .ThenBy(item => item.GetParameters().Length).First();
            var parameters = method.GetParameters();
            var args = parameters.Select(Default).ToArray(); args[0] = source;
            for (int i = 1; i < parameters.Length; i++) if (parameters[i].ParameterType == optionsType) args[i] = options;
            return method.Invoke(null, args);
        }

        internal static object Invoke(object target, string name, params object[] arguments)
        {
            var method = target.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(item => item.Name == name && !item.IsGenericMethod && Accepts(item.GetParameters(), arguments))
                .OrderBy(item => item.GetParameters().Length).First();
            var parameters = method.GetParameters();
            var args = parameters.Select(Default).ToArray(); Array.Copy(arguments, args, arguments.Length);
            return method.Invoke(target, args);
        }

        private static bool Accepts(ParameterInfo[] parameters, object[] args)
        {
            if (parameters.Length < args.Length) return false;
            for (int i = 0; i < args.Length; i++)
                if (args[i] != null && !parameters[i].ParameterType.IsInstanceOfType(args[i])) return false;
            for (int i = args.Length; i < parameters.Length; i++)
                if (!parameters[i].IsOptional && !parameters[i].HasDefaultValue) return false;
            return true;
        }

        internal static object Property(object target, string name) => target.GetType().GetProperty(name).GetValue(target);
        internal static IEnumerable<object> Nodes(object root) => ((IEnumerable)Invoke(root, "DescendantNodes")).Cast<object>();
        internal static object Default(ParameterInfo parameter) => parameter.HasDefaultValue ? parameter.DefaultValue :
            parameter.ParameterType.IsValueType ? Activator.CreateInstance(parameter.ParameterType) : null;
    }
}
