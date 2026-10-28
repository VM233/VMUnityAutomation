using System;
using System.Globalization;
using UnityEditor;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationSerializedFloatingValue
    {
        internal static object Read(SerializedProperty property)
        {
            if (IsDouble(property))
                return property.doubleValue;
            return property.floatValue;
        }

        internal static void Write(SerializedProperty property, object value)
        {
            if (IsDouble(property))
            {
                double number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                if (double.IsNaN(number) || double.IsInfinity(number))
                    throw new ArgumentOutOfRangeException(nameof(value), "Serialized Double values must be finite.");
                property.doubleValue = number;
            }
            else
            {
                float number = Convert.ToSingle(value, CultureInfo.InvariantCulture);
                if (float.IsNaN(number) || float.IsInfinity(number))
                    throw new ArgumentOutOfRangeException(nameof(value), "Serialized Float values must be finite.");
                property.floatValue = number;
            }
        }

        private static bool IsDouble(SerializedProperty property)
        {
#if UNITY_2022_2_OR_NEWER
            return property.numericType == SerializedPropertyNumericType.Double;
#else
            return string.Equals(property.type, "double", StringComparison.OrdinalIgnoreCase);
#endif
        }
    }
}
