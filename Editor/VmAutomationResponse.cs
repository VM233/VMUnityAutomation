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
        /// Envelope normalization retains the established project-tool unwrap. A successful
        /// root discriminator is removed only when the published owner contract omits it.
        /// </summary>
        public static object CompactForTransport(object data,
            Dictionary<string, object> outputSchema)
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
            bool declaresRootSuccess = outputSchema.TryGetValue("properties", out object properties) &&
                                       ToDictionary(properties).ContainsKey("success");
            if (root.TryGetValue("success", out object success) && ToBool(success) &&
                !carriesObservedError && !declaresRootSuccess)
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
}
