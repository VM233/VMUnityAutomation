using System;

namespace VMUnityAutomation.Editor
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = true)]
    public sealed class VmMinItemsAttribute : Attribute
    {
        public int Count { get; }

        public VmMinItemsAttribute(int count)
        {
            Count = count >= 0
                ? count
                : throw new ArgumentOutOfRangeException(nameof(count));
        }
    }
}
