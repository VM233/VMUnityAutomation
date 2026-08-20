using System;

namespace VMUnityAutomation.Editor
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
    public sealed class VmDataProductAttribute : Attribute
    {
        public string ContractId { get; }

        public VmDataProductAttribute(string contractId)
        {
            ContractId = string.IsNullOrWhiteSpace(contractId)
                ? throw new ArgumentException("Data product contract id is required.", nameof(contractId))
                : contractId.Trim();
        }
    }
}
