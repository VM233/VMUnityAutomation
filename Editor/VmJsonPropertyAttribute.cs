using System;

namespace VMUnityAutomation.Editor
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = true)]
    public sealed class VmJsonPropertyAttribute : Attribute
    {
        public string Name { get; }

        public VmJsonPropertyAttribute(string name)
        {
            Name = string.IsNullOrWhiteSpace(name)
                ? throw new ArgumentException("JSON property name is required.", nameof(name))
                : name.Trim();
        }
    }
}
