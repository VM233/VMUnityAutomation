using System;
using UnityEditor;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationSerializedEnumValue
    {
        internal static object Read(SerializedProperty property)
        {
            RequireEnumProperty(property);

            int index = property.enumValueIndex;
            string[] names = property.enumNames;
            if (index >= 0 && index < names.Length)
                return names[index];

            return property.intValue;
        }

        internal static void Write(SerializedProperty property, object value)
        {
            RequireEnumProperty(property);

            if (value is string enumName)
            {
                int index = Array.FindIndex(property.enumNames,
                    name => string.Equals(name, enumName, StringComparison.OrdinalIgnoreCase));
                if (index < 0)
                {
                    throw new ArgumentException(
                        $"Enum property '{property.propertyPath}' does not define '{enumName}'. " +
                        $"Available values: {string.Join(", ", property.enumNames)}.");
                }

                property.enumValueIndex = index;
                return;
            }

            property.intValue = Convert.ToInt32(value);
        }

        private static void RequireEnumProperty(SerializedProperty property)
        {
            if (property == null)
                throw new ArgumentNullException(nameof(property));
            if (property.propertyType != SerializedPropertyType.Enum)
            {
                throw new ArgumentException(
                    $"Serialized property '{property.propertyPath}' is {property.propertyType}, not Enum.",
                    nameof(property));
            }
        }
    }
}
