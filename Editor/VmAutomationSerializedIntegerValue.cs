using System;
using System.Globalization;
using UnityEditor;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationSerializedIntegerValue
    {
        internal static object Read(SerializedProperty property) => IsSignedInt64(property)
            ? property.longValue.ToString(CultureInfo.InvariantCulture)
            : (object)property.intValue;

        internal static void Write(SerializedProperty property, object value)
        {
            if (IsSignedInt64(property))
                property.longValue = value is string text
                    ? long.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture)
                    : Convert.ToInt64(value, CultureInfo.InvariantCulture);
            else
                property.intValue = Convert.ToInt32(value);
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
