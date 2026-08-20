using System.Collections.Generic;

namespace VMUnityAutomation.Editor
{
    internal sealed class VmAutomationToolProfile
    {
        internal bool ReadOnly;
        internal bool MutatesAssets;
        internal bool MutatesRuntime;
        internal bool Dangerous;
        internal bool LongRunning;
        internal bool MayReloadDomain;
        internal bool RequiresPlayMode;
        internal VmAutomationTransactionProfile Transaction;

        internal static VmAutomationToolProfile Create(bool readOnly = false,
            bool mutatesAssets = false, bool mutatesRuntime = false,
            bool dangerous = false, bool longRunning = false,
            bool mayReloadDomain = false, bool requiresPlayMode = false,
            VmAutomationTransactionProfile transaction = null)
        {
            return new VmAutomationToolProfile
            {
                ReadOnly = readOnly,
                MutatesAssets = mutatesAssets,
                MutatesRuntime = mutatesRuntime,
                Dangerous = dangerous,
                LongRunning = longRunning,
                MayReloadDomain = mayReloadDomain,
                RequiresPlayMode = requiresPlayMode,
                Transaction = transaction?.Clone(),
            };
        }

        internal VmAutomationToolProfile Clone()
        {
            return new VmAutomationToolProfile
            {
                ReadOnly = ReadOnly,
                MutatesAssets = MutatesAssets,
                MutatesRuntime = MutatesRuntime,
                Dangerous = Dangerous,
                LongRunning = LongRunning,
                MayReloadDomain = MayReloadDomain,
                RequiresPlayMode = RequiresPlayMode,
                Transaction = Transaction?.Clone(),
            };
        }

        internal Dictionary<string, object> ToAnnotations()
        {
            var annotations = new Dictionary<string, object>();
            if (ReadOnly)
            {
                annotations["readOnlyHint"] = true;
                annotations["idempotentHint"] = true;
            }

            if (Dangerous)
                annotations["destructiveHint"] = true;
            return annotations;
        }
    }
}
