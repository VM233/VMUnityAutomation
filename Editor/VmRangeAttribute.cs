using System;

namespace VMUnityAutomation.Editor
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, Inherited = true)]
    public sealed class VmRangeAttribute : Attribute
    {
        public double Minimum { get; }

        public double Maximum { get; }

        public VmRangeAttribute(double minimum, double maximum)
        {
            if (double.IsNaN(minimum) || double.IsInfinity(minimum))
                throw new ArgumentOutOfRangeException(nameof(minimum));
            if (double.IsNaN(maximum) || double.IsInfinity(maximum))
                throw new ArgumentOutOfRangeException(nameof(maximum));
            if (minimum > maximum)
                throw new ArgumentException("minimum cannot exceed maximum.");
            Minimum = minimum;
            Maximum = maximum;
        }
    }
}
