using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationSerializedIntegerVectorValue
    {
        internal static object Read(SerializedProperty property)
        {
            if (property.propertyType == SerializedPropertyType.Vector2Int)
            {
                var vector = property.vector2IntValue;
                return new Dictionary<string, object> { { "x", vector.x }, { "y", vector.y } };
            }

            var vector3 = property.vector3IntValue;
            return new Dictionary<string, object> { { "x", vector3.x }, { "y", vector3.y }, { "z", vector3.z } };
        }

        internal static void Write(SerializedProperty property, object value)
        {
            int dimensions = property.propertyType == SerializedPropertyType.Vector2Int ? 2 : 3;
            if (!(value is Dictionary<string, object> components) || components.Count != dimensions ||
                !components.ContainsKey("x") || !components.ContainsKey("y") ||
                (dimensions == 3 && !components.ContainsKey("z")))
                throw new ArgumentException($"Integer vector '{property.propertyPath}' requires exactly {dimensions} coordinates.");

            int x = ReadCoordinate(components["x"], "x");
            int y = ReadCoordinate(components["y"], "y");
            if (dimensions == 2)
                property.vector2IntValue = new Vector2Int(x, y);
            else
                property.vector3IntValue = new Vector3Int(x, y, ReadCoordinate(components["z"], "z"));
        }

        private static int ReadCoordinate(object value, string coordinate)
        {
            if (!(value is sbyte || value is byte || value is short || value is ushort ||
                  value is int || value is uint || value is long || value is ulong ||
                  value is float || value is double || value is decimal))
                throw new ArgumentException($"Integer vector coordinate '{coordinate}' requires a number.");

            decimal number = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
            if (number != decimal.Truncate(number) || number < int.MinValue || number > int.MaxValue)
                throw new ArgumentException($"Integer vector coordinate '{coordinate}' must be an exact Int32.");
            return decimal.ToInt32(number);
        }
    }
}
