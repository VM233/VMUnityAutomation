using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationResponse
    {
        public static Dictionary<string, object> Error(string message, string errorCode = "error",
            bool retryable = false, Dictionary<string, object> extra = null)
        {
            var response = new Dictionary<string, object>
            {
                { "success", false },
                { "error", message ?? "Unknown error" },
                { "message", message ?? "Unknown error" },
                { "errorCode", string.IsNullOrEmpty(errorCode) ? "error" : errorCode },
                { "retryable", retryable },
            };

            if (extra != null)
            {
                foreach (var pair in extra)
                    response[pair.Key] = pair.Value;
            }

            return response;
        }

        public static Dictionary<string, object> Success(object result = null, Dictionary<string, object> extra = null)
        {
            var response = new Dictionary<string, object>
            {
                { "success", true },
            };

            if (result != null)
                response["result"] = result;

            if (extra != null)
            {
                foreach (var pair in extra)
                    response[pair.Key] = pair.Value;
            }

            return response;
        }

        public static bool TryGetError(object data, out string message, out string errorCode, out bool retryable)
        {
            message = null;
            errorCode = null;
            retryable = false;

            var dictionary = ToDictionary(data);
            if (dictionary == null)
                return false;

            bool hasExplicitSuccess = dictionary.TryGetValue("success", out var explicitSuccess);
            if (hasExplicitSuccess && ToBool(explicitSuccess))
                return false;

            if (dictionary.TryGetValue("retryable", out var retryableValue))
                retryable = ToBool(retryableValue);

            if (dictionary.TryGetValue("errorCode", out var codeValue) && codeValue != null)
                errorCode = codeValue.ToString();

            if (dictionary.TryGetValue("error", out var errorValue) && errorValue != null)
            {
                if (errorValue is string errorText)
                {
                    message = errorText;
                    if (string.IsNullOrEmpty(errorCode))
                        errorCode = "error";
                    return !string.IsNullOrEmpty(message);
                }

                if (hasExplicitSuccess && !ToBool(explicitSuccess) &&
                    TryGetError(errorValue, out string nestedMessage, out string nestedCode,
                        out bool nestedRetryable))
                {
                    message = nestedMessage;
                    if (string.IsNullOrEmpty(errorCode))
                        errorCode = nestedCode;
                    retryable |= nestedRetryable;
                    return true;
                }
            }

            if (dictionary.TryGetValue("success", out var successValue) && ToBool(successValue) == false)
            {
                if (dictionary.TryGetValue("message", out var messageValue) && messageValue != null)
                    message = messageValue.ToString();

                if (string.IsNullOrEmpty(message))
                    message = "Operation failed.";

                if (string.IsNullOrEmpty(errorCode))
                    errorCode = "operation_failed";

                return true;
            }

            return false;
        }

        public static Dictionary<string, object> NormalizeError(object data, string fallbackCode = "error",
            bool fallbackRetryable = false)
        {
            if (!TryGetError(data, out var message, out var errorCode, out var retryable))
                return Error("Operation failed.", fallbackCode, fallbackRetryable);

            var dictionary = ToDictionary(data);
            var response = dictionary != null
                ? new Dictionary<string, object>(dictionary)
                : new Dictionary<string, object>();

            response["success"] = false;
            response["error"] = message;
            response["message"] = message;
            response["errorCode"] = string.IsNullOrEmpty(errorCode) ? fallbackCode : errorCode;
            response["retryable"] = retryable || fallbackRetryable;
            return response;
        }

        /// <summary>
        /// Convert CLR and Unity values into JSON-compatible structures without deleting,
        /// renaming, or stringifying members declared by the published output contract.
        /// The only envelope normalization is the established project-tool unwrap and the
        /// successful root discriminator consumed by the Node response boundary.
        /// </summary>
        public static object CompactForTransport(object data)
        {
            Dictionary<string, object> source = ToDictionary(data);
            if (source != null && IsProjectToolSuccessEnvelope(source))
                return PreserveProjectToolSchemaShape(source["result"]);

            if (source != null && TryGetCompletedProjectToolTicketResult(
                    source, out object projectToolResult))
            {
                var ticket = (Dictionary<string, object>)
                    PreserveProjectToolSchemaShape(source);
                ticket["result"] = PreserveProjectToolSchemaShape(projectToolResult);
                return ticket;
            }

            object transported = PreserveProjectToolSchemaShape(data);
            if (!(transported is Dictionary<string, object> root))
                return transported;

            bool carriesObservedError = root.TryGetValue("error", out object observedError) &&
                                        observedError != null;
            if (root.TryGetValue("success", out object success) && ToBool(success) &&
                !carriesObservedError)
                root.Remove("success");
            return root;
        }

        public static Dictionary<string, object> ToDictionary(object data)
        {
            if (data == null)
                return null;

            if (data is Dictionary<string, object> typed)
                return typed;

            if (data is IDictionary dictionary)
            {
                var result = new Dictionary<string, object>();
                foreach (DictionaryEntry entry in dictionary)
                    result[entry.Key.ToString()] = entry.Value;
                return result;
            }

            var type = data.GetType();
            if (type.IsPrimitive || data is string || data is decimal)
                return null;

            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            if (properties.Length == 0)
                return null;

            var reflected = new Dictionary<string, object>();
            foreach (var property in properties)
            {
                if (!property.CanRead)
                    continue;

                try
                {
                    reflected[property.Name] = property.GetValue(data, null);
                }
                catch
                {
                    reflected[property.Name] = null;
                }
            }

            return reflected;
        }

        private static object PreserveProjectToolSchemaShape(object value)
        {
            if (value == null || value is string || value is decimal)
                return value;

            Type valueType = value.GetType();
            if (valueType.IsPrimitive || valueType.IsEnum)
                return value;

            if (value is IList list)
            {
                var transportedList = new List<object>(list.Count);
                foreach (object item in list)
                    transportedList.Add(PreserveProjectToolSchemaShape(item));
                return transportedList;
            }

            if (VmAutomationUnityValueFormatter.TryStructureUnityValue(value, out object structuredValue))
                return PreserveProjectToolSchemaShape(structuredValue);

            Dictionary<string, object> source = ToDictionary(value);
            if (source == null)
                return value;

            var transported = new Dictionary<string, object>();
            foreach (KeyValuePair<string, object> pair in source)
            {
                if (pair.Key == "$unityStruct")
                    continue;
                transported[pair.Key] =
                    PreserveProjectToolSchemaShape(pair.Value);
            }
            return transported;
        }

        private static bool IsProjectToolSuccessEnvelope(Dictionary<string, object> dictionary)
        {
            if (!dictionary.TryGetValue("success", out object success) || !ToBool(success) ||
                !dictionary.ContainsKey("result") || !dictionary.ContainsKey("toolName"))
                return false;

            foreach (string key in dictionary.Keys)
            {
                if (key != "success" && key != "result" && key != "toolName")
                    return false;
            }

            return true;
        }

        private static bool TryGetCompletedProjectToolTicketResult(
            Dictionary<string, object> ticket, out object result)
        {
            result = null;
            if (!ticket.ContainsKey("ticketId") ||
                !ticket.TryGetValue("actionName", out object actionName) ||
                !ticket.TryGetValue("status", out object status) ||
                !string.Equals(status?.ToString(), "Completed",
                    StringComparison.Ordinal) ||
                !ticket.TryGetValue("result", out object envelopeValue))
                return false;

            string action = actionName?.ToString();
            if (string.IsNullOrEmpty(action) ||
                !action.StartsWith("project-tools/call/", StringComparison.Ordinal))
                return false;

            Dictionary<string, object> envelope = ToDictionary(envelopeValue);
            if (envelope == null || !IsProjectToolSuccessEnvelope(envelope))
                return false;

            result = envelope["result"];
            return true;
        }

        private static bool ToBool(object value)
        {
            if (value is bool boolValue)
                return boolValue;

            return value != null && bool.TryParse(value.ToString(), out var parsed) && parsed;
        }
    }

    /// <summary>
    /// Converts supported Unity value structs to either the explicitly requested compact
    /// execute-code format or a typed JSON structure for normal Automation transport.
    /// </summary>
    internal static class VmAutomationUnityValueFormatter
    {
        public static bool TryFormatUnityValue(object value, out string formatted)
        {
            switch (value)
            {
                case Vector2 vector2:
                    formatted = FormatTuple(vector2.x, vector2.y);
                    return true;
                case Vector2Int vector2Int:
                    formatted = FormatTuple(vector2Int.x, vector2Int.y);
                    return true;
                case Vector3 vector3:
                    formatted = FormatTuple(vector3.x, vector3.y, vector3.z);
                    return true;
                case Vector3Int vector3Int:
                    formatted = FormatTuple(vector3Int.x, vector3Int.y, vector3Int.z);
                    return true;
                case Vector4 vector4:
                    formatted = FormatTuple(vector4.x, vector4.y, vector4.z, vector4.w);
                    return true;
                case Quaternion quaternion:
                    formatted = FormatTuple(quaternion.x, quaternion.y, quaternion.z, quaternion.w);
                    return true;
                case Rect rect:
                    formatted = FormatRect(rect.xMin, rect.yMin, rect.xMax, rect.yMax,
                        rect.width, rect.height);
                    return true;
                case RectInt rectInt:
                    formatted = FormatRect(rectInt.xMin, rectInt.yMin, rectInt.xMax, rectInt.yMax,
                        rectInt.width, rectInt.height);
                    return true;
                case Bounds bounds:
                    formatted = FormatBounds(bounds.min, bounds.max, bounds.size);
                    return true;
                case BoundsInt boundsInt:
                    formatted = FormatBounds(boundsInt.min, boundsInt.max, boundsInt.size);
                    return true;
                case Color color:
                    formatted = FormatColor(color.r, color.g, color.b, color.a);
                    return true;
                case Color32 color32:
                    formatted = FormatColor(color32.r, color32.g, color32.b, color32.a);
                    return true;
                case RectOffset offset:
                    formatted = FormatEdges(offset.left, offset.top, offset.right, offset.bottom);
                    return true;
                case Matrix4x4 matrix:
                    formatted = FormatMatrix(matrix);
                    return true;
                case Ray ray:
                    formatted = $"origin:{FormatTuple(ray.origin.x, ray.origin.y, ray.origin.z)}," +
                                $"direction:{FormatTuple(ray.direction.x, ray.direction.y, ray.direction.z)}";
                    return true;
                case Ray2D ray2D:
                    formatted = $"origin:{FormatTuple(ray2D.origin.x, ray2D.origin.y)}," +
                                $"direction:{FormatTuple(ray2D.direction.x, ray2D.direction.y)}";
                    return true;
                case Plane plane:
                    formatted = $"normal:{FormatTuple(plane.normal.x, plane.normal.y, plane.normal.z)}," +
                                $"distance:{FormatNumber(plane.distance)}";
                    return true;
                case Pose pose:
                    formatted = $"position:{FormatTuple(pose.position.x, pose.position.y, pose.position.z)}," +
                                $"rotation:{FormatTuple(pose.rotation.x, pose.rotation.y, pose.rotation.z, pose.rotation.w)}";
                    return true;
            }

            formatted = null;
            return false;
        }

        public static bool TryStructureUnityValue(object value, out object structured)
        {
            switch (value)
            {
                case Vector2 vector2:
                    structured = Structure("Vector2", ("x", vector2.x), ("y", vector2.y));
                    return true;
                case Vector2Int vector2Int:
                    structured = Structure("Vector2Int", ("x", vector2Int.x), ("y", vector2Int.y));
                    return true;
                case Vector3 vector3:
                    structured = Structure("Vector3", ("x", vector3.x), ("y", vector3.y), ("z", vector3.z));
                    return true;
                case Vector3Int vector3Int:
                    structured = Structure("Vector3Int", ("x", vector3Int.x), ("y", vector3Int.y),
                        ("z", vector3Int.z));
                    return true;
                case Vector4 vector4:
                    structured = Structure("Vector4", ("x", vector4.x), ("y", vector4.y), ("z", vector4.z),
                        ("w", vector4.w));
                    return true;
                case Quaternion quaternion:
                    structured = Structure("Quaternion", ("x", quaternion.x), ("y", quaternion.y),
                        ("z", quaternion.z), ("w", quaternion.w));
                    return true;
                case Rect rect:
                    structured = Structure("Rect", ("x", rect.x), ("y", rect.y), ("width", rect.width),
                        ("height", rect.height));
                    return true;
                case RectInt rectInt:
                    structured = Structure("RectInt", ("x", rectInt.x), ("y", rectInt.y),
                        ("width", rectInt.width), ("height", rectInt.height));
                    return true;
                case Bounds bounds:
                    structured = Structure("Bounds",
                        ("center", Structure("Vector3", ("x", bounds.center.x), ("y", bounds.center.y),
                            ("z", bounds.center.z))),
                        ("size", Structure("Vector3", ("x", bounds.size.x), ("y", bounds.size.y),
                            ("z", bounds.size.z))));
                    return true;
                case BoundsInt boundsInt:
                    structured = Structure("BoundsInt",
                        ("position", Structure("Vector3Int", ("x", boundsInt.position.x), ("y", boundsInt.position.y),
                            ("z", boundsInt.position.z))),
                        ("size", Structure("Vector3Int", ("x", boundsInt.size.x), ("y", boundsInt.size.y),
                            ("z", boundsInt.size.z))));
                    return true;
                case Color color:
                    structured = Structure("Color", ("r", color.r), ("g", color.g), ("b", color.b), ("a", color.a));
                    return true;
                case Color32 color32:
                    structured = Structure("Color32", ("r", color32.r), ("g", color32.g), ("b", color32.b),
                        ("a", color32.a));
                    return true;
                case RectOffset offset:
                    structured = Structure("RectOffset", ("left", offset.left), ("top", offset.top),
                        ("right", offset.right), ("bottom", offset.bottom));
                    return true;
                case Matrix4x4 matrix:
                    var values = new List<object>(16);
                    for (int row = 0; row < 4; row++)
                    {
                        for (int column = 0; column < 4; column++)
                            values.Add(matrix[row, column]);
                    }
                    structured = Structure("Matrix4x4", ("rowMajor", values));
                    return true;
                case Ray ray:
                    structured = Structure("Ray",
                        ("origin", Structure("Vector3", ("x", ray.origin.x), ("y", ray.origin.y),
                            ("z", ray.origin.z))),
                        ("direction", Structure("Vector3", ("x", ray.direction.x), ("y", ray.direction.y),
                            ("z", ray.direction.z))));
                    return true;
                case Ray2D ray2D:
                    structured = Structure("Ray2D",
                        ("origin", Structure("Vector2", ("x", ray2D.origin.x), ("y", ray2D.origin.y))),
                        ("direction", Structure("Vector2", ("x", ray2D.direction.x), ("y", ray2D.direction.y))));
                    return true;
                case Plane plane:
                    structured = Structure("Plane",
                        ("normal", Structure("Vector3", ("x", plane.normal.x), ("y", plane.normal.y),
                            ("z", plane.normal.z))),
                        ("distance", plane.distance));
                    return true;
                case Pose pose:
                    structured = Structure("Pose",
                        ("position", Structure("Vector3", ("x", pose.position.x), ("y", pose.position.y),
                            ("z", pose.position.z))),
                        ("rotation", Structure("Quaternion", ("x", pose.rotation.x), ("y", pose.rotation.y),
                            ("z", pose.rotation.z), ("w", pose.rotation.w))));
                    return true;
                default:
                    structured = null;
                    return false;
            }
        }

        private static Dictionary<string, object> Structure(string type,
            params (string key, object value)[] values)
        {
            var result = new Dictionary<string, object>
            {
                { "$unityStruct", true },
                { "type", type },
            };
            foreach ((string key, object value) in values)
                result[key] = value;
            return result;
        }

        private static string FormatNumber(object value)
        {
            switch (Type.GetTypeCode(value.GetType()))
            {
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                case TypeCode.UInt64:
                    return Convert.ToString(value, CultureInfo.InvariantCulture);
                case TypeCode.Decimal:
                    return ((decimal)value).ToString("G29", CultureInfo.InvariantCulture);
                default:
                    return FormatNumber(Convert.ToDouble(value, CultureInfo.InvariantCulture));
            }
        }

        private static string FormatNumber(double value)
        {
            if (value == 0d)
                return "0";

            double absolute = Math.Abs(value);
            string format = absolute >= 0.000001d && absolute < 1000000000d
                ? "0.######"
                : "0.######E+0";
            return value.ToString(format, CultureInfo.InvariantCulture);
        }

        private static string FormatTuple(params object[] values)
        {
            var formatted = new string[values.Length];
            for (int index = 0; index < values.Length; index++)
                formatted[index] = FormatNumber(values[index]);
            return $"({string.Join(",", formatted)})";
        }

        private static string FormatRect(
            object xMin, object yMin, object xMax, object yMax, object width, object height)
        {
            return $"{FormatTuple(xMin, yMin)}-{FormatTuple(xMax, yMax)}," +
                   $"size:{FormatTuple(width, height)}";
        }

        private static string FormatBounds(Vector3 min, Vector3 max, Vector3 size)
        {
            return $"{FormatTuple(min.x, min.y, min.z)}-{FormatTuple(max.x, max.y, max.z)}," +
                   $"size:{FormatTuple(size.x, size.y, size.z)}";
        }

        private static string FormatBounds(Vector3Int min, Vector3Int max, Vector3Int size)
        {
            return $"{FormatTuple(min.x, min.y, min.z)}-{FormatTuple(max.x, max.y, max.z)}," +
                   $"size:{FormatTuple(size.x, size.y, size.z)}";
        }

        private static string FormatColor(object red, object green, object blue, object alpha)
        {
            return $"rgba({FormatNumber(red)},{FormatNumber(green)}," +
                   $"{FormatNumber(blue)},{FormatNumber(alpha)})";
        }

        private static string FormatEdges(object left, object top, object right, object bottom)
        {
            return $"LTRB({FormatNumber(left)},{FormatNumber(top)}," +
                   $"{FormatNumber(right)},{FormatNumber(bottom)})";
        }

        private static string FormatMatrix(Matrix4x4 matrix)
        {
            return $"[{FormatTuple(matrix.m00, matrix.m01, matrix.m02, matrix.m03)};" +
                   $"{FormatTuple(matrix.m10, matrix.m11, matrix.m12, matrix.m13)};" +
                   $"{FormatTuple(matrix.m20, matrix.m21, matrix.m22, matrix.m23)};" +
                   $"{FormatTuple(matrix.m30, matrix.m31, matrix.m32, matrix.m33)}]";
        }

    }
}
