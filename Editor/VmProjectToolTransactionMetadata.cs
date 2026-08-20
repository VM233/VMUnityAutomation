using System;
using System.Linq;

namespace VMUnityAutomation.Editor
{
    /// <summary>Builds and validates neutral transaction metadata declared by project tools.</summary>
    internal static class VmProjectToolTransactionMetadata
    {
        internal static VmAutomationTransactionProfile Build(VmProjectToolAttribute attribute)
        {
            if (attribute == null) throw new ArgumentNullException(nameof(attribute));
            string[] fields =
            {
                attribute.TransactionScope,
                attribute.TransactionAtomicity,
                attribute.TransactionIsolation,
                attribute.TransactionDurability,
                attribute.TransactionRollbackKind,
            };
            bool any = fields.Any(value => !string.IsNullOrWhiteSpace(value)) ||
                       attribute.TransactionCommitEvidence?.Any(value =>
                           !string.IsNullOrWhiteSpace(value)) == true;
            if (!any) return null;
            return new VmAutomationTransactionProfile
            {
                Scope = attribute.TransactionScope?.Trim() ?? "",
                Atomicity = attribute.TransactionAtomicity?.Trim() ?? "",
                Isolation = attribute.TransactionIsolation?.Trim() ?? "",
                Durability = attribute.TransactionDurability?.Trim() ?? "",
                RollbackKind = attribute.TransactionRollbackKind?.Trim() ?? "",
                CommitEvidence = (attribute.TransactionCommitEvidence ?? Array.Empty<string>())
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value.Trim())
                    .Distinct(StringComparer.Ordinal)
                    .ToList(),
            };
        }

        internal static string Validate(VmAutomationTransactionProfile transaction,
            bool readOnly, string toolName)
        {
            if (transaction == null) return null;
            if (readOnly)
                return $"Read-only project tool '{toolName}' cannot declare a transaction contract.";
            if (string.IsNullOrWhiteSpace(transaction.Scope) ||
                string.IsNullOrWhiteSpace(transaction.Atomicity) ||
                string.IsNullOrWhiteSpace(transaction.Isolation) ||
                string.IsNullOrWhiteSpace(transaction.Durability) ||
                string.IsNullOrWhiteSpace(transaction.RollbackKind) ||
                transaction.CommitEvidence == null || transaction.CommitEvidence.Count == 0)
            {
                return $"Project tool '{toolName}' must declare all transaction fields and at least one commit-evidence item.";
            }
            string mechanicsError = VmTransactionMechanics.Validate(transaction);
            if (mechanicsError != null)
                return $"Project tool '{toolName}' has invalid transaction metadata: {mechanicsError}";
            return null;
        }
    }
}
