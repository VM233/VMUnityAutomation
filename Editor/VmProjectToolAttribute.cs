using System;

namespace VMUnityAutomation.Editor
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false,
        Inherited = false)]
    public sealed class VmProjectToolAttribute : Attribute
    {
        public string ToolName { get; }

        public string Description { get; set; }

        public string ShortName { get; set; }

        public string InputSchemaJson { get; set; }

        public string OutputSchemaJson { get; set; }

        public string CleanupToolName { get; set; }

        public VmProjectToolSideEffect SideEffects { get; set; }

        public string[] ErrorCodes { get; set; }

        public bool ReadOnly { get; set; }

        public bool MutatesAssets { get; set; }

        public bool MutatesRuntime { get; set; }

        public bool MutatesProjectFiles { get; set; }

        public bool Dangerous { get; set; }

        public bool LongRunning { get; set; }

        public bool MayReloadDomain { get; set; }

        public bool RequiresPlayMode { get; set; }

        public string ModuleId { get; set; }

        public string Capability { get; set; }

        public string OperationKind { get; set; }

        public string WhenToUse { get; set; }

        public string NotFor { get; set; }

        public string CompletionEvidence { get; set; }

        public string[] Aliases { get; set; }

        public string[] SearchTerms { get; set; }

        public string[] Preconditions { get; set; }

        public string TransactionScope { get; set; }

        public string TransactionAtomicity { get; set; }

        public string TransactionIsolation { get; set; }

        public string TransactionDurability { get; set; }

        public string TransactionRollbackKind { get; set; }

        public string[] TransactionCommitEvidence { get; set; }

        public VmProjectToolAttribute(string toolName)
        {
            ToolName = toolName;
        }
    }
}
