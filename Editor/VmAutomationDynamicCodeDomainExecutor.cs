using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace VMUnityAutomation.Editor
{
    public sealed class VmAutomationDynamicCodeDomainExecutor : MarshalByRefObject
    {
        public Dictionary<string, object> Execute(byte[] assemblyBytes,
            Dictionary<string, object> args)
        {
            try
            {
                Assembly assembly = Assembly.Load(assemblyBytes);
                if (VmAutomationEditorCommands.CanUseIsolatedAppDomain(
                        assembly.GetReferencedAssemblies().Select(reference => reference.Name),
                        out string reason) == false)
                {
                    return new Dictionary<string, object>
                    {
                        { "requiresDefaultDomain", true },
                        { "assemblyIsolationReason", reason },
                    };
                }

                Type compiledType = assembly.GetType("VmAutomationDynamicCode");
                MethodInfo method = compiledType?.GetMethod(
                    "Execute", BindingFlags.Public | BindingFlags.Static);
                if (method == null)
                {
                    return VmAutomationResponse.Error(
                        "Compiled dynamic-code entry point was not found.",
                        "execute_code_entry_missing");
                }

                object result = method.Invoke(null, null);
                return VmAutomationEditorCommands.SerializeResult(result, args);
            }
            catch (TargetInvocationException exception)
            {
                return VmAutomationEditorCommands.BuildExecuteCodeError(
                    exception.InnerException ?? exception, args);
            }
            catch (Exception exception)
            {
                return VmAutomationEditorCommands.BuildExecuteCodeError(exception, args);
            }
        }
    }
}
