using System;
using System.Collections.Generic;
using System.Linq;

namespace VMUnityAutomation.Editor
{
    /// <summary>
    /// Neutral transaction capability contract. Values describe mechanics and evidence only;
    /// consumer-specific business meaning remains outside the framework catalog.
    /// </summary>
    internal sealed class MCPTransactionProfile
    {
        internal string Scope { get; set; }
        internal string Atomicity { get; set; }
        internal string Isolation { get; set; }
        internal string Durability { get; set; }
        internal string RollbackKind { get; set; }
        internal IReadOnlyList<string> CommitEvidence { get; set; }

        internal static MCPTransactionProfile Create(string scope, string atomicity,
            string isolation, string durability, string rollbackKind,
            params string[] commitEvidence)
        {
            var profile = new MCPTransactionProfile
            {
                Scope = scope,
                Atomicity = atomicity,
                Isolation = isolation,
                Durability = durability,
                RollbackKind = rollbackKind,
                CommitEvidence = (commitEvidence ?? new string[0])
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value.Trim())
                    .Distinct(StringComparer.Ordinal)
                    .ToList(),
            };
            string validationError = VmTransactionMechanics.Validate(profile);
            if (validationError != null)
                throw new ArgumentException(validationError, nameof(scope));
            return profile;
        }

        internal MCPTransactionProfile Clone()
        {
            return new MCPTransactionProfile
            {
                Scope = Scope,
                Atomicity = Atomicity,
                Isolation = Isolation,
                Durability = Durability,
                RollbackKind = RollbackKind,
                CommitEvidence = CommitEvidence?.ToList() ?? new List<string>(),
            };
        }

        internal Dictionary<string, object> ToDictionary()
        {
            return new Dictionary<string, object>
            {
                { "scope", Scope ?? "" },
                { "atomicity", Atomicity ?? "" },
                { "isolation", Isolation ?? "" },
                { "durability", Durability ?? "" },
                { "rollbackKind", RollbackKind ?? "" },
                { "commitEvidence", (CommitEvidence ?? new List<string>()).Cast<object>().ToList() },
            };
        }
    }
}
