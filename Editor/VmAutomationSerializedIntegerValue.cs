using System;
using System.Globalization;
using UnityEditor;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationSerializedIntegerValue
    {
        internal static object Read(SerializedProperty property)
        {
            if (IsUnsignedInt32(property))
            {
#if UNITY_2022_2_OR_NEWER
                return property.uintValue;
#else
                return checked((uint)property.longValue);
#endif
            }
            return IsSignedInt64(property)
                ? property.longValue.ToString(CultureInfo.InvariantCulture)
                : (object)property.intValue;
        }

        internal static void Write(SerializedProperty property, object value)
        {
            if (IsUnsignedInt32(property))
            {
                uint unsigned = Convert.ToUInt32(value, CultureInfo.InvariantCulture);
#if UNITY_2022_2_OR_NEWER
                property.uintValue = unsigned;
#else
                property.longValue = unsigned;
#endif
            }
            else if (IsSignedInt64(property))
                property.longValue = value is string text
                    ? long.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture)
                    : Convert.ToInt64(value, CultureInfo.InvariantCulture);
            else
                property.intValue = Convert.ToInt32(value);
        }

        private static bool IsUnsignedInt32(SerializedProperty property)
        {
#if UNITY_2022_2_OR_NEWER
            return property.numericType == SerializedPropertyNumericType.UInt32;
#else
            string type = property.type;
            return string.Equals(type, "unsigned int", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(type, "uint", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(type, "UInt32", StringComparison.OrdinalIgnoreCase);
#endif
        }

        private static bool IsSignedInt64(SerializedProperty property)
        {
            string type = property.type;
            return string.Equals(type, "long", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(type, "Int64", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(type, "SInt64", StringComparison.OrdinalIgnoreCase);
        }
    }
}
