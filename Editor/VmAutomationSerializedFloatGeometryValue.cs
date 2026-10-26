using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationSerializedFloatGeometryValue
    {
        internal static void Write(SerializedProperty property, object value)
        {
            string[] names = property.propertyType switch
            {
                SerializedPropertyType.Color => new[] { "r", "g", "b", "a" },
                SerializedPropertyType.Vector2 => new[] { "x", "y" },
                SerializedPropertyType.Vector3 => new[] { "x", "y", "z" },
                SerializedPropertyType.Rect => new[] { "x", "y", "width", "height" },
                _ => new[] { "x", "y", "z", "w" }
            };
            var values = new float[names.Length];
            if (property.propertyType == SerializedPropertyType.Color) values[3] = 1;
            if (value is IList array)
            {
                if (array.Count != names.Length) throw Invalid(property, names);
                for (int index = 0; index < names.Length; index++) values[index] = Number(array[index]);
            }
            else if (value is Dictionary<string, object> fields)
            {
                if (fields.Count > names.Length || fields.Keys.Any(key => !names.Contains(key)) ||
                    (property.propertyType == SerializedPropertyType.Quaternion && names.Any(name => !fields.ContainsKey(name))))
                    throw Invalid(property, names);
                for (int index = 0; index < names.Length; index++)
                    if (fields.TryGetValue(names[index], out object coordinate)) values[index] = Number(coordinate);
            }
            else throw Invalid(property, names);
            switch (property.propertyType)
            {
                case SerializedPropertyType.Color: property.colorValue = new Color(values[0], values[1], values[2], values[3]); break;
                case SerializedPropertyType.Vector2: property.vector2Value = new Vector2(values[0], values[1]); break;
                case SerializedPropertyType.Vector3: property.vector3Value = new Vector3(values[0], values[1], values[2]); break;
                case SerializedPropertyType.Vector4: property.vector4Value = new Vector4(values[0], values[1], values[2], values[3]); break;
                case SerializedPropertyType.Quaternion: property.quaternionValue = new Quaternion(values[0], values[1], values[2], values[3]); break;
                case SerializedPropertyType.Rect: property.rectValue = new Rect(values[0], values[1], values[2], values[3]); break;
                default: throw new ArgumentException("Unsupported floating-point geometry property.");
            }
        }

        private static float Number(object value)
        {
            if (value == null || value is bool) throw new ArgumentException("Geometry coordinates must be finite numbers.");
            float number = Convert.ToSingle(value, System.Globalization.CultureInfo.InvariantCulture);
            if (float.IsNaN(number) || float.IsInfinity(number)) throw new ArgumentException("Geometry coordinates must be finite numbers.");
            return number;
        }

        private static ArgumentException Invalid(SerializedProperty property, string[] names) =>
            new ArgumentException($"{property.propertyType} requires an object with {string.Join(",", names)} coordinates or an ordered array of exactly {names.Length} numbers.");
    }
}
