using System;

namespace VMUnityAutomation.Editor
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = true)]
    public sealed class VmMinLengthAttribute : Attribute
    {
        public int Length { get; }

        public VmMinLengthAttribute(int length)
        {
            Length = length >= 0
                ? length
                : throw new ArgumentOutOfRangeException(nameof(length));
        }
    }
}
