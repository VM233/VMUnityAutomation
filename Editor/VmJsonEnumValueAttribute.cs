using System;

namespace VMUnityAutomation.Editor
{
    [AttributeUsage(AttributeTargets.Field, Inherited = false)]
    public sealed class VmJsonEnumValueAttribute : Attribute
    {
        public string Value { get; }

        public VmJsonEnumValueAttribute(string value)
        {
            Value = string.IsNullOrWhiteSpace(value)
                ? throw new ArgumentException("Enum JSON value is required.", nameof(value))
                : value.Trim();
        }
    }
}
