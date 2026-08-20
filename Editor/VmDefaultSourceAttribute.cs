using System;

namespace VMUnityAutomation.Editor
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = true)]
    public sealed class VmDefaultSourceAttribute : Attribute
    {
        public string Source { get; }

        public bool ExplicitValueWins { get; }

        public VmDefaultSourceAttribute(string source, bool explicitValueWins = true)
        {
            Source = string.IsNullOrWhiteSpace(source)
                ? throw new ArgumentException("Default source is required.", nameof(source))
                : source.Trim();
            ExplicitValueWins = explicitValueWins;
        }
    }
}
