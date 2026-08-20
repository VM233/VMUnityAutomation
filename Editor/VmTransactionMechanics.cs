using System;
using System.Collections.Generic;
using System.Linq;

namespace VMUnityAutomation.Editor
{
    /// <summary>Canonical, semantically neutral transaction mechanics vocabulary.</summary>
    public static class VmTransactionMechanics
    {
        public static class Atomicity
        {
            public const string VerifiedRollback = "verified-rollback";
            public const string StagedSingleAsset = "staged-single-asset";
            public const string BestEffortEditorSession = "best-effort-editor-session";
            public const string VerifiedFileRollback = "verified-file-rollback";
            public const string VerifiedSingleAssetRollback = "verified-single-asset-rollback";
        }

        public static class Isolation
        {
            public const string WorkspaceExclusive = "workspace-exclusive";
            public const string RequestOwnedPrefabSession = "request-owned-prefab-session";
            public const string RequestUndoGroup = "request-undo-group";
            public const string RequestSerialized = "request-serialized";
            public const string RequestOwnedWrapperSnapshot = "request-owned-wrapper-snapshot";
        }

        public static class Durability
        {
            public const string ReloadResumableJob = "reload-resumable-job";
            public const string EditorSession = "editor-session";
        }

        public static class RollbackKind
        {
            public const string DurableByteSnapshot = "durable-byte-snapshot";
            public const string DiscardUncommittedPrefabSession =
                "discard-uncommitted-prefab-session";
            public const string UnityUndo = "unity-undo";
            public const string InMemoryTextSnapshot = "in-memory-text-snapshot";
            public const string UnityUndoOrValueRestore = "unity-undo-or-value-restore";
            public const string AtomicByteSnapshot = "atomic-byte-snapshot";
        }

        private static readonly HashSet<string> AtomicityValues = Values(typeof(Atomicity));
        private static readonly HashSet<string> IsolationValues = Values(typeof(Isolation));
        private static readonly HashSet<string> DurabilityValues = Values(typeof(Durability));
        private static readonly HashSet<string> RollbackKindValues = Values(typeof(RollbackKind));

        internal static string Validate(MCPTransactionProfile transaction)
        {
            if (transaction == null)
                return null;
            if (!IsIdentifier(transaction.Scope))
                return "transaction scope must be a kebab-case mechanical identifier.";
            if (!AtomicityValues.Contains(transaction.Atomicity))
                return $"Unsupported transaction atomicity '{transaction.Atomicity}'.";
            if (!IsolationValues.Contains(transaction.Isolation))
                return $"Unsupported transaction isolation '{transaction.Isolation}'.";
            if (!DurabilityValues.Contains(transaction.Durability))
                return $"Unsupported transaction durability '{transaction.Durability}'.";
            if (!RollbackKindValues.Contains(transaction.RollbackKind))
                return $"Unsupported transaction rollback kind '{transaction.RollbackKind}'.";
            if (transaction.CommitEvidence == null || transaction.CommitEvidence.Count == 0 ||
                transaction.CommitEvidence.Any(value => !IsIdentifier(value)))
            {
                return "transaction commit evidence must contain kebab-case mechanical identifiers.";
            }
            return null;
        }

        private static HashSet<string> Values(Type type)
        {
            return new HashSet<string>(type.GetFields()
                .Where(field => field.IsLiteral && field.FieldType == typeof(string))
                .Select(field => (string)field.GetRawConstantValue()), StringComparer.Ordinal);
        }

        private static bool IsIdentifier(string value)
        {
            if (string.IsNullOrEmpty(value) || value[0] == '-' || value[value.Length - 1] == '-')
                return false;
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if ((character >= 'a' && character <= 'z') ||
                    (character >= '0' && character <= '9') || character == '-')
                    continue;
                return false;
            }
            return !value.Contains("--");
        }
    }
}
