using System.Collections.Generic;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationCodePolicyInputSchema
    {
        internal static Dictionary<string, object> Create()
        {
            return VmAutomationToolSchemaFactory.Schema(VmAutomationToolSchemaFactory.Props(
                        new KeyValuePair<string, object>("preprocessorSymbols", new Dictionary<string, object>
                        {
                            { "type", "array" }, { "maxItems", 2048 },
                            { "items", new Dictionary<string, object> { { "type", "string" }, { "minLength", 1 }, { "maxLength", 128 } } },
                            { "description", "Explicit exact compilation symbols. Omit for the active Editor context; an empty array intentionally enables no conditional symbols. Combined symbol length is bounded to65536 characters. The result reports the exact set and its source." }
                        }),
                        VmAutomationToolSchemaFactory.ArrayProp("paths", "string", "Optional project-relative C# files or directories. Explicit paths take precedence over roots and changedOnly."),
                        VmAutomationToolSchemaFactory.ArrayProp("roots", "string", "Project-relative roots scanned when paths are omitted and changedOnly is false. Defaults to Assets."),
                        VmAutomationToolSchemaFactory.ArrayProp("excludePaths", "string", "Project-relative path prefixes excluded from the selected source set."),
                        VmAutomationToolSchemaFactory.Prop("changedOnly", "boolean", "Review only working-tree and untracked Git paths, plus gitBase...HEAD when gitBase is supplied. Defaults to false."),
                        VmAutomationToolSchemaFactory.Prop("gitBase", "string", "Optional trusted Git base ref used with changedOnly."),
                        VmAutomationToolSchemaFactory.Prop("forbidPartial", "boolean", "Reject ordinary partial class and record declarations. Source-generation exceptions are Unity UxmlElement declarations and classes explicitly deriving from Unity.Entities.SystemBase (including global:: qualification). Defaults to true for explicit or Git-changed scope and false for an unrestricted root scan."),
                        VmAutomationToolSchemaFactory.Prop("maxTypeLines", "number", "Maximum physical lines per class or record. Defaults to 1500."),
                        VmAutomationToolSchemaFactory.ArrayProp("forbiddenMethodNames", "string", "Exact method identifiers that must not be declared."),
                        VmAutomationToolSchemaFactory.ArrayProp("forbiddenMemberAccesses", "string", "Exact whitespace-insensitive member accesses that must not appear."),
                        VmAutomationToolSchemaFactory.ArrayProp("forbiddenGenericInvocations", "string", "Whitespace-insensitive generic invocation names such as LoadAssetAtPath<ItemConfig> that must not be called."),
                        VmAutomationToolSchemaFactory.Prop("maxIssues", "number", "Maximum returned findings. Defaults to 200; capped at 5000.")
                    ));
        }
    }
}
