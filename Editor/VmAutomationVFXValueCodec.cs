using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationVFXValueCodec
    {
        private const int MaxDepth = 12;
        private const int MaxItems = 256;

        internal static object ConvertTo(object rawValue, Type targetType,
            string valuePath)
        {
            if (targetType == null)
                throw new ArgumentException($"{valuePath} target type is unavailable.");
            Type nullableType = Nullable.GetUnderlyingType(targetType);
            if (rawValue == null)
            {
                if (!targetType.IsValueType || nullableType != null)
                    return null;
                throw new ArgumentException(
                    $"{valuePath} cannot be null for {FriendlyType(targetType)}.");
            }
            if (nullableType != null)
                return ConvertTo(rawValue, nullableType, valuePath);
            if (targetType.IsInstanceOfType(rawValue))
                return rawValue;

            if (targetType == typeof(Type))
                return ConvertTypeReference(rawValue, valuePath);
            if (typeof(UnityEngine.Object).IsAssignableFrom(targetType))
                return ConvertAssetReference(rawValue, targetType, valuePath);
            if (targetType.IsEnum)
                return ConvertEnum(rawValue, targetType, valuePath);
            if (targetType == typeof(string))
                return Convert.ToString(rawValue, CultureInfo.InvariantCulture);
            if (targetType == typeof(bool))
                return ConvertBoolean(rawValue, valuePath);
            if (IsNumeric(targetType))
                return ConvertNumber(rawValue, targetType, valuePath);

            Dictionary<string, object> dictionary = AsDictionary(rawValue);
            if (targetType == typeof(Vector2))
                return new Vector2(Number(dictionary, "x", valuePath),
                    Number(dictionary, "y", valuePath));
            if (targetType == typeof(Vector2Int))
                return new Vector2Int(Integer(dictionary, "x", valuePath),
                    Integer(dictionary, "y", valuePath));
            if (targetType == typeof(Vector3))
                return new Vector3(Number(dictionary, "x", valuePath),
                    Number(dictionary, "y", valuePath),
                    Number(dictionary, "z", valuePath));
            if (targetType == typeof(Vector3Int))
                return new Vector3Int(Integer(dictionary, "x", valuePath),
                    Integer(dictionary, "y", valuePath),
                    Integer(dictionary, "z", valuePath));
            if (targetType == typeof(Vector4))
                return new Vector4(Number(dictionary, "x", valuePath),
                    Number(dictionary, "y", valuePath),
                    Number(dictionary, "z", valuePath),
                    Number(dictionary, "w", valuePath));
            if (targetType == typeof(Color))
                return new Color(Number(dictionary, "r", valuePath),
                    Number(dictionary, "g", valuePath),
                    Number(dictionary, "b", valuePath),
                    OptionalNumber(dictionary, "a", 1f, valuePath));
            if (targetType == typeof(Color32))
                return new Color32(Byte(dictionary, "r", valuePath),
                    Byte(dictionary, "g", valuePath),
                    Byte(dictionary, "b", valuePath),
                    OptionalByte(dictionary, "a", byte.MaxValue, valuePath));
            if (targetType == typeof(Quaternion))
                return new Quaternion(Number(dictionary, "x", valuePath),
                    Number(dictionary, "y", valuePath),
                    Number(dictionary, "z", valuePath),
                    Number(dictionary, "w", valuePath));
            if (targetType == typeof(Rect))
                return new Rect(Number(dictionary, "x", valuePath),
                    Number(dictionary, "y", valuePath),
                    Number(dictionary, "width", valuePath),
                    Number(dictionary, "height", valuePath));
            if (targetType == typeof(RectInt))
                return new RectInt(Integer(dictionary, "x", valuePath),
                    Integer(dictionary, "y", valuePath),
                    Integer(dictionary, "width", valuePath),
                    Integer(dictionary, "height", valuePath));
            if (targetType == typeof(Bounds))
                return new Bounds((Vector3)ConvertTo(Required(dictionary, "center",
                        valuePath), typeof(Vector3), valuePath + ".center"),
                    (Vector3)ConvertTo(Required(dictionary, "size", valuePath),
                        typeof(Vector3), valuePath + ".size"));
            if (targetType == typeof(BoundsInt))
                return new BoundsInt((Vector3Int)ConvertTo(Required(dictionary,
                        "position", valuePath), typeof(Vector3Int),
                        valuePath + ".position"),
                    (Vector3Int)ConvertTo(Required(dictionary, "size", valuePath),
                        typeof(Vector3Int), valuePath + ".size"));
            if (targetType == typeof(Matrix4x4))
                return ConvertMatrix(rawValue, dictionary, valuePath);
            if (targetType == typeof(AnimationCurve))
                return ConvertCurve(dictionary, valuePath);
            if (targetType == typeof(Gradient))
                return ConvertGradient(dictionary, valuePath);

            if (targetType.IsArray)
                return ConvertArray(rawValue, targetType.GetElementType(), valuePath);
            if (TryGetListElementType(targetType, out Type elementType))
                return ConvertList(rawValue, targetType, elementType, valuePath);
            if (dictionary != null && (targetType.IsValueType ||
                                        targetType.GetConstructor(Type.EmptyTypes) != null))
                return ConvertObject(dictionary, targetType, valuePath);

            throw new ArgumentException(
                $"{valuePath} cannot be converted to {FriendlyType(targetType)}.");
        }

        internal static object Sanitize(object value, int depth = 0)
        {
            if (value == null)
                return null;
            if (depth >= MaxDepth)
            {
                return new Dictionary<string, object>
                {
                    { "type", FriendlyType(value.GetType()) },
                    { "truncated", true },
                    { "reason", "maxDepth" },
                };
            }
            if (value is UnityEngine.Object unityObject)
            {
                if (unityObject == null)
                    return null;
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(unityObject,
                    out string guid, out long localId);
                return new Dictionary<string, object>
                {
                    { "name", unityObject.name ?? "" },
                    { "type", unityObject.GetType().FullName },
                    { "assetPath", AssetDatabase.GetAssetPath(unityObject) ?? "" },
                    { "guid", guid ?? "" },
                    { "localId", localId.ToString() },
                    { "instanceId", VmObjectId.Get(unityObject) },
                };
            }
            if (value is Type typeValue)
            {
                return new Dictionary<string, object>
                {
                    { "fullTypeName", typeValue.FullName ?? "" },
                    { "assemblyName", typeValue.Assembly.GetName().Name ?? "" },
                };
            }
            if (value is Enum enumValue)
                return enumValue.ToString();
            if (value is string || value is bool || IsNumeric(value.GetType()))
                return value;
            if (value is Vector2 vector2)
                return Fields(("x", vector2.x), ("y", vector2.y));
            if (value is Vector2Int vector2Int)
                return Fields(("x", vector2Int.x), ("y", vector2Int.y));
            if (value is Vector3 vector3)
                return Fields(("x", vector3.x), ("y", vector3.y),
                    ("z", vector3.z));
            if (value is Vector3Int vector3Int)
                return Fields(("x", vector3Int.x), ("y", vector3Int.y),
                    ("z", vector3Int.z));
            if (value is Vector4 vector4)
                return Fields(("x", vector4.x), ("y", vector4.y),
                    ("z", vector4.z), ("w", vector4.w));
            if (value is Quaternion quaternion)
                return Fields(("x", quaternion.x), ("y", quaternion.y),
                    ("z", quaternion.z), ("w", quaternion.w));
            if (value is Color color)
                return Fields(("r", color.r), ("g", color.g),
                    ("b", color.b), ("a", color.a));
            if (value is Color32 color32)
                return Fields(("r", color32.r), ("g", color32.g),
                    ("b", color32.b), ("a", color32.a));
            if (value is Rect rect)
                return Fields(("x", rect.x), ("y", rect.y),
                    ("width", rect.width), ("height", rect.height));
            if (value is RectInt rectInt)
                return Fields(("x", rectInt.x), ("y", rectInt.y),
                    ("width", rectInt.width), ("height", rectInt.height));
            if (value is Bounds bounds)
                return Fields(("center", Sanitize(bounds.center, depth + 1)),
                    ("size", Sanitize(bounds.size, depth + 1)));
            if (value is BoundsInt boundsInt)
                return Fields(("position", Sanitize(boundsInt.position,
                        depth + 1)),
                    ("size", Sanitize(boundsInt.size, depth + 1)));
            if (value is Matrix4x4 matrix)
            {
                var items = new List<object>(16);
                for (int row = 0; row < 4; row++)
                for (int column = 0; column < 4; column++)
                    items.Add(matrix[row, column]);
                return new Dictionary<string, object> { { "values", items } };
            }
            if (value is AnimationCurve curve)
            {
                return new Dictionary<string, object>
                {
                    { "keys", curve.keys.Take(MaxItems).Select(key =>
                        (object)Fields(("time", key.time), ("value", key.value),
                            ("inTangent", key.inTangent),
                            ("outTangent", key.outTangent),
                            ("inWeight", key.inWeight),
                            ("outWeight", key.outWeight),
                            ("weightedMode", key.weightedMode.ToString()))).ToList() },
                    { "preWrapMode", curve.preWrapMode.ToString() },
                    { "postWrapMode", curve.postWrapMode.ToString() },
                    { "truncated", curve.length > MaxItems },
                };
            }
            if (value is Gradient gradient)
            {
                return new Dictionary<string, object>
                {
                    { "colorKeys", gradient.colorKeys.Take(MaxItems).Select(key =>
                        (object)Fields(("color", Sanitize(key.color, depth + 1)),
                            ("time", key.time))).ToList() },
                    { "alphaKeys", gradient.alphaKeys.Take(MaxItems).Select(key =>
                        (object)Fields(("alpha", key.alpha),
                            ("time", key.time))).ToList() },
                    { "mode", gradient.mode.ToString() },
                };
            }
            if (value is IDictionary dictionary)
            {
                var result = new Dictionary<string, object>();
                int count = 0;
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (count++ >= MaxItems)
                        break;
                    result[entry.Key?.ToString() ?? "null"] =
                        Sanitize(entry.Value, depth + 1);
                }
                if (dictionary.Count > MaxItems)
                    result["_truncated"] = true;
                return result;
            }
            if (value is IEnumerable enumerable)
            {
                var result = new List<object>();
                int count = 0;
                foreach (object item in enumerable)
                {
                    if (count++ < MaxItems)
                        result.Add(Sanitize(item, depth + 1));
                }
                if (count <= MaxItems)
                    return result;
                return new Dictionary<string, object>
                {
                    { "items", result },
                    { "totalItems", count },
                    { "truncated", true },
                };
            }

            Type type = value.GetType();
            var objectResult = new Dictionary<string, object>
            {
                { "type", FriendlyType(type) },
            };
            int memberCount = 0;
            foreach (FieldInfo field in type.GetFields(BindingFlags.Instance |
                         BindingFlags.Public))
            {
                if (memberCount++ >= MaxItems)
                    break;
                objectResult[field.Name] = Sanitize(field.GetValue(value), depth + 1);
            }
            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance |
                         BindingFlags.Public)
                         .Where(property => property.CanRead &&
                                            property.GetIndexParameters().Length == 0))
            {
                if (objectResult.ContainsKey(property.Name))
                    continue;
                if (memberCount++ >= MaxItems)
                    break;
                try
                {
                    objectResult[property.Name] = Sanitize(property.GetValue(value, null),
                        depth + 1);
                }
                catch
                {
                    objectResult[property.Name] = new Dictionary<string, object>
                    {
                        { "unreadable", true },
                    };
                }
            }
            return objectResult;
        }

        internal static string FriendlyType(Type type)
        {
            return type?.FullName ?? "";
        }

        private static UnityEngine.Object ConvertAssetReference(object rawValue,
            Type targetType, string valuePath)
        {
            Dictionary<string, object> dictionary = AsDictionary(rawValue);
            if (dictionary == null)
                throw new ArgumentException(
                    $"{valuePath} must be an asset reference object with assetPath and optional type.");
            string unknown = dictionary.Keys.FirstOrDefault(key =>
                key != "assetPath" && key != "type");
            if (unknown != null)
                throw new ArgumentException(
                    $"{valuePath}.{unknown} is not part of the asset reference contract.");
            string assetPath = dictionary.TryGetValue("assetPath",
                out object pathValue) ? pathValue?.ToString() : null;
            assetPath = VmAutomationVFXAssetPath.RequireFile(assetPath, true,
                valuePath + ".assetPath");
            UnityEngine.Object mainAsset = AssetDatabase.LoadMainAssetAtPath(
                assetPath);
            if (mainAsset == null)
                throw VmAutomationVFXError.Create("asset_not_found",
                    $"{valuePath} asset '{assetPath}' was not found as {FriendlyType(targetType)}.");
            UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath(assetPath,
                targetType);
            if (asset == null)
                throw VmAutomationVFXError.Create("asset_type_mismatch",
                    $"{valuePath} asset '{assetPath}' is {FriendlyType(mainAsset.GetType())}, not {FriendlyType(targetType)}.");
            if (dictionary.TryGetValue("type", out object rawType))
            {
                string typeName = rawType?.ToString();
                Type declaredType = VmAutomationAssetGraphUtility.FindType(typeName);
                if (declaredType == null ||
                    !typeof(UnityEngine.Object).IsAssignableFrom(declaredType))
                    throw new ArgumentException(
                        $"{valuePath}.type '{typeName}' is not a UnityEngine.Object type.");
                if (!declaredType.IsInstanceOfType(asset))
                    throw new ArgumentException(
                        $"{valuePath}.type '{typeName}' does not match asset '{assetPath}' ({asset.GetType().FullName}).");
            }
            return asset;
        }

        private static object ConvertEnum(object rawValue, Type targetType,
            string valuePath)
        {
            try
            {
                if (rawValue is string name)
                    return Enum.Parse(targetType, name, true);
                Type underlying = Enum.GetUnderlyingType(targetType);
                object numeric = ConvertNumber(rawValue, underlying, valuePath);
                return Enum.ToObject(targetType, numeric);
            }
            catch (Exception exception)
            {
                throw new ArgumentException(
                    $"{valuePath} is not a valid {FriendlyType(targetType)} value. " +
                    $"Allowed names: {string.Join(", ", Enum.GetNames(targetType))}.",
                    exception);
            }
        }

        private static bool ConvertBoolean(object rawValue, string valuePath)
        {
            if (rawValue is bool boolean)
                return boolean;
            if (rawValue is string text && bool.TryParse(text, out bool parsed))
                return parsed;
            throw new ArgumentException($"{valuePath} must be a Boolean.");
        }

        private static object ConvertNumber(object rawValue, Type targetType,
            string valuePath)
        {
            try
            {
                if (rawValue is bool)
                    throw new InvalidCastException();
                if (IsIntegral(targetType))
                {
                    decimal numeric = Convert.ToDecimal(rawValue,
                        CultureInfo.InvariantCulture);
                    if (decimal.Truncate(numeric) != numeric)
                        throw new InvalidCastException();
                    return Convert.ChangeType(numeric, targetType,
                        CultureInfo.InvariantCulture);
                }
                object converted = Convert.ChangeType(rawValue, targetType,
                    CultureInfo.InvariantCulture);
                if (converted is float single && (float.IsNaN(single) ||
                    float.IsInfinity(single)))
                    throw new InvalidCastException();
                if (converted is double number && (double.IsNaN(number) ||
                    double.IsInfinity(number)))
                    throw new InvalidCastException();
                return converted;
            }
            catch (Exception exception)
            {
                throw new ArgumentException(
                    $"{valuePath} must be a {FriendlyType(targetType)} number.",
                    exception);
            }
        }

        private static Matrix4x4 ConvertMatrix(object rawValue,
            Dictionary<string, object> dictionary, string valuePath)
        {
            object valuesRaw = dictionary != null &&
                               dictionary.TryGetValue("values", out object nested)
                ? nested
                : rawValue;
            List<object> values = AsList(valuesRaw);
            if (values == null || values.Count != 16)
                throw new ArgumentException($"{valuePath}.values must contain 16 numbers.");
            var matrix = new Matrix4x4();
            for (int index = 0; index < 16; index++)
                matrix[index / 4, index % 4] = (float)ConvertNumber(values[index],
                    typeof(float), $"{valuePath}.values[{index}]");
            return matrix;
        }

        private static Type ConvertTypeReference(object rawValue,
            string valuePath)
        {
            string fullTypeName;
            if (rawValue is string text)
            {
                fullTypeName = text;
            }
            else
            {
                Dictionary<string, object> dictionary = AsDictionary(rawValue);
                if (dictionary == null || dictionary.Count != 1 ||
                    !dictionary.TryGetValue("fullTypeName", out object rawName))
                    throw new ArgumentException(
                        $"{valuePath} must be a full type name or an object containing only fullTypeName.");
                fullTypeName = rawName?.ToString();
            }
            if (string.IsNullOrWhiteSpace(fullTypeName))
                throw new ArgumentException(
                    $"{valuePath}.fullTypeName must not be empty.");
            return VmAutomationAssetGraphUtility.FindType(fullTypeName) ??
                   throw new ArgumentException(
                       $"{valuePath} type '{fullTypeName}' was not found.");
        }

        private static AnimationCurve ConvertCurve(Dictionary<string, object> dictionary,
            string valuePath)
        {
            List<object> keys = AsList(Required(dictionary, "keys", valuePath));
            if (keys == null)
                throw new ArgumentException($"{valuePath}.keys must be an array.");
            EnsureItemCount(keys.Count, valuePath + ".keys");
            var curve = new AnimationCurve(keys.Select((raw, index) =>
            {
                Dictionary<string, object> key = AsDictionary(raw) ??
                    throw new ArgumentException(
                        $"{valuePath}.keys[{index}] must be an object.");
                var frame = new Keyframe(Number(key, "time", valuePath),
                    Number(key, "value", valuePath),
                    OptionalNumber(key, "inTangent", 0f, valuePath),
                    OptionalNumber(key, "outTangent", 0f, valuePath),
                    OptionalNumber(key, "inWeight", 0f, valuePath),
                    OptionalNumber(key, "outWeight", 0f, valuePath));
                if (key.TryGetValue("weightedMode", out object weightedMode))
                    frame.weightedMode = (WeightedMode)ConvertEnum(weightedMode,
                        typeof(WeightedMode), valuePath + ".weightedMode");
                return frame;
            }).ToArray());
            if (dictionary.TryGetValue("preWrapMode", out object preWrapMode))
                curve.preWrapMode = (WrapMode)ConvertEnum(preWrapMode,
                    typeof(WrapMode), valuePath + ".preWrapMode");
            if (dictionary.TryGetValue("postWrapMode", out object postWrapMode))
                curve.postWrapMode = (WrapMode)ConvertEnum(postWrapMode,
                    typeof(WrapMode), valuePath + ".postWrapMode");
            return curve;
        }

        private static Gradient ConvertGradient(Dictionary<string, object> dictionary,
            string valuePath)
        {
            List<object> rawColorKeys = AsList(Required(dictionary, "colorKeys",
                valuePath));
            List<object> rawAlphaKeys = AsList(Required(dictionary, "alphaKeys",
                valuePath));
            if (rawColorKeys == null || rawAlphaKeys == null)
                throw new ArgumentException(
                    $"{valuePath}.colorKeys and alphaKeys must be arrays.");
            EnsureItemCount(rawColorKeys.Count, valuePath + ".colorKeys");
            EnsureItemCount(rawAlphaKeys.Count, valuePath + ".alphaKeys");
            GradientColorKey[] colorKeys = rawColorKeys.Select((raw, index) =>
            {
                Dictionary<string, object> key = AsDictionary(raw) ??
                    throw new ArgumentException(
                        $"{valuePath}.colorKeys[{index}] must be an object.");
                return new GradientColorKey(
                    (Color)ConvertTo(Required(key, "color", valuePath),
                        typeof(Color), valuePath + $".colorKeys[{index}].color"),
                    Number(key, "time", valuePath));
            }).ToArray();
            GradientAlphaKey[] alphaKeys = rawAlphaKeys.Select((raw, index) =>
            {
                Dictionary<string, object> key = AsDictionary(raw) ??
                    throw new ArgumentException(
                        $"{valuePath}.alphaKeys[{index}] must be an object.");
                return new GradientAlphaKey(Number(key, "alpha", valuePath),
                    Number(key, "time", valuePath));
            }).ToArray();
            var gradient = new Gradient();
            gradient.SetKeys(colorKeys, alphaKeys);
            if (dictionary.TryGetValue("mode", out object mode))
                gradient.mode = (GradientMode)ConvertEnum(mode,
                    typeof(GradientMode), valuePath + ".mode");
            return gradient;
        }

        private static Array ConvertArray(object rawValue, Type elementType,
            string valuePath)
        {
            List<object> values = AsList(rawValue);
            if (values == null)
                throw new ArgumentException($"{valuePath} must be an array.");
            EnsureItemCount(values.Count, valuePath);
            Array result = Array.CreateInstance(elementType, values.Count);
            for (int index = 0; index < values.Count; index++)
                result.SetValue(ConvertTo(values[index], elementType,
                    $"{valuePath}[{index}]"), index);
            return result;
        }

        private static object ConvertList(object rawValue, Type targetType,
            Type elementType, string valuePath)
        {
            List<object> values = AsList(rawValue);
            if (values == null)
                throw new ArgumentException($"{valuePath} must be an array.");
            EnsureItemCount(values.Count, valuePath);
            Type concreteType = !targetType.IsInterface && !targetType.IsAbstract
                ? targetType
                : typeof(List<>).MakeGenericType(elementType);
            var result = (IList)Activator.CreateInstance(concreteType);
            for (int index = 0; index < values.Count; index++)
                result.Add(ConvertTo(values[index], elementType,
                    $"{valuePath}[{index}]"));
            return result;
        }

        private static object ConvertObject(Dictionary<string, object> dictionary,
            Type targetType, string valuePath)
        {
            object result = Activator.CreateInstance(targetType);
            var consumed = new HashSet<string>(StringComparer.Ordinal);
            foreach (FieldInfo field in targetType.GetFields(BindingFlags.Instance |
                         BindingFlags.Public))
            {
                if (!dictionary.TryGetValue(field.Name, out object raw))
                    continue;
                field.SetValue(result, ConvertTo(raw, field.FieldType,
                    valuePath + "." + field.Name));
                consumed.Add(field.Name);
            }
            foreach (PropertyInfo property in targetType.GetProperties(
                         BindingFlags.Instance | BindingFlags.Public)
                         .Where(property => property.CanWrite &&
                                            property.GetIndexParameters().Length == 0))
            {
                if (!dictionary.TryGetValue(property.Name, out object raw))
                    continue;
                property.SetValue(result, ConvertTo(raw, property.PropertyType,
                    valuePath + "." + property.Name), null);
                consumed.Add(property.Name);
            }
            string unknown = dictionary.Keys.FirstOrDefault(key =>
                !consumed.Contains(key));
            if (!string.IsNullOrEmpty(unknown))
                throw new ArgumentException(
                    $"{valuePath}.{unknown} is not a public member of {FriendlyType(targetType)}.");
            return result;
        }

        private static bool IsNumeric(Type type)
        {
            if (type == null || type.IsEnum)
                return false;
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.Decimal:
                case TypeCode.Double:
                case TypeCode.Single:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsIntegral(Type type)
        {
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                    return true;
                default:
                    return false;
            }
        }

        private static void EnsureItemCount(int count, string valuePath)
        {
            if (count > MaxItems)
                throw new ArgumentException(
                    $"{valuePath} contains {count} items; the maximum is {MaxItems}.");
        }

        private static bool TryGetListElementType(Type type, out Type elementType)
        {
            elementType = null;
            if (type.IsGenericType &&
                type.GetGenericTypeDefinition() == typeof(List<>))
            {
                elementType = type.GetGenericArguments()[0];
                return true;
            }
            Type enumerable = type.GetInterfaces()
                .FirstOrDefault(candidate => candidate.IsGenericType &&
                    candidate.GetGenericTypeDefinition() == typeof(IList<>));
            if (enumerable == null)
                return false;
            elementType = enumerable.GetGenericArguments()[0];
            return true;
        }

        private static Dictionary<string, object> AsDictionary(object value)
        {
            if (value is Dictionary<string, object> typed)
                return typed;
            if (!(value is IDictionary dictionary))
                return null;
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (DictionaryEntry entry in dictionary)
                result[entry.Key?.ToString() ?? ""] = entry.Value;
            return result;
        }

        private static List<object> AsList(object value)
        {
            if (value is List<object> typed)
                return typed;
            if (!(value is IEnumerable enumerable) || value is string)
                return null;
            return enumerable.Cast<object>().ToList();
        }

        private static object Required(Dictionary<string, object> values, string key,
            string valuePath)
        {
            if (values == null || !values.TryGetValue(key, out object value))
                throw new ArgumentException($"{valuePath}.{key} is required.");
            return value;
        }

        private static float Number(Dictionary<string, object> values, string key,
            string valuePath)
        {
            return (float)ConvertNumber(Required(values, key, valuePath),
                typeof(float), valuePath + "." + key);
        }

        private static float OptionalNumber(Dictionary<string, object> values,
            string key, float defaultValue, string valuePath)
        {
            return values != null && values.TryGetValue(key, out object value)
                ? (float)ConvertNumber(value, typeof(float), valuePath + "." + key)
                : defaultValue;
        }

        private static int Integer(Dictionary<string, object> values, string key,
            string valuePath)
        {
            return (int)ConvertNumber(Required(values, key, valuePath),
                typeof(int), valuePath + "." + key);
        }

        private static byte Byte(Dictionary<string, object> values, string key,
            string valuePath)
        {
            return (byte)ConvertNumber(Required(values, key, valuePath),
                typeof(byte), valuePath + "." + key);
        }

        private static byte OptionalByte(Dictionary<string, object> values,
            string key, byte defaultValue, string valuePath)
        {
            return values != null && values.TryGetValue(key, out object value)
                ? (byte)ConvertNumber(value, typeof(byte), valuePath + "." + key)
                : defaultValue;
        }

        private static Dictionary<string, object> Fields(
            params (string key, object value)[] fields)
        {
            return fields.ToDictionary(field => field.key, field => field.value,
                StringComparer.Ordinal);
        }
    }
}
