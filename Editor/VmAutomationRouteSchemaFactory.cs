using System.Collections.Generic;
using System.Linq;

namespace VMUnityAutomation.Editor
{
    /// <summary>Small exact JSON-Schema vocabulary for built-in route descriptors.</summary>
    internal static class VmAutomationRouteSchemaFactory
    {
        internal static KeyValuePair<string, object> Field(
            string name, Dictionary<string, object> schema)
        {
            return new KeyValuePair<string, object>(name, schema);
        }

        internal static KeyValuePair<string, object> Field(string name, string type)
        {
            return Field(name, Type(type));
        }

        internal static Dictionary<string, object> Type(string type)
        {
            return new Dictionary<string, object> { { "type", type } };
        }

        internal static Dictionary<string, object> Describe(
            Dictionary<string, object> schema, string description)
        {
            schema["description"] = description;
            return schema;
        }

        internal static Dictionary<string, object> Constrain(
            Dictionary<string, object> schema, int? minimum = null,
            int? maximum = null, int? minItems = null,
            bool uniqueItems = false)
        {
            if (minimum.HasValue)
                schema["minimum"] = minimum.Value;
            if (maximum.HasValue)
                schema["maximum"] = maximum.Value;
            if (minItems.HasValue)
                schema["minItems"] = minItems.Value;
            if (uniqueItems)
                schema["uniqueItems"] = true;
            return schema;
        }

        internal static Dictionary<string, object> Nullable(string type)
        {
            return new Dictionary<string, object>
            {
                { "type", new List<object> { type, "null" } },
            };
        }

        internal static Dictionary<string, object> Enum(params string[] values)
        {
            return new Dictionary<string, object>
            {
                { "type", "string" },
                { "enum", values.Cast<object>().ToList() },
            };
        }

        internal static Dictionary<string, object> Array(Dictionary<string, object> item)
        {
            return new Dictionary<string, object>
            {
                { "type", "array" },
                { "items", item },
            };
        }

        internal static Dictionary<string, object> Map(
            Dictionary<string, object> valueSchema)
        {
            return new Dictionary<string, object>
            {
                { "type", "object" },
                { "additionalProperties", valueSchema },
            };
        }

        internal static Dictionary<string, object> Object(
            IEnumerable<KeyValuePair<string, object>> fields,
            params string[] required)
        {
            return VmAutomationToolSchemaFactory.ObjectSchema(
                fields.ToDictionary(pair => pair.Key, pair => pair.Value), required);
        }

        internal static Dictionary<string, object> Object(
            params KeyValuePair<string, object>[] fields)
        {
            return Object(fields.AsEnumerable());
        }

        internal static Dictionary<string, object> OneOf(
            params Dictionary<string, object>[] variants)
        {
            return new Dictionary<string, object>
            {
                { "oneOf", variants.Cast<object>().ToList() },
            };
        }

        internal static Dictionary<string, object> RequireAnyOf(
            Dictionary<string, object> schema, params string[] requiredProperties)
        {
            schema["anyOf"] = requiredProperties
                .Select(property => (object)new Dictionary<string, object>
                {
                    { "required", new List<object> { property } },
                })
                .ToList();
            return schema;
        }

        internal static Dictionary<string, object> Root(Dictionary<string, object> schema)
        {
            return VmAutomationToolSchemaFactory.WithJsonValueDefinition(schema);
        }

        internal static Dictionary<string, object> JsonValue()
        {
            return new Dictionary<string, object>
            {
                { "$ref", VmAutomationToolSchemaFactory.JsonValueReference },
            };
        }

        internal static Dictionary<string, object> JsonMap()
        {
            return Map(JsonValue());
        }

        internal static Dictionary<string, object> Error(params string[] optionalFields)
        {
            var fields = new List<KeyValuePair<string, object>>
            {
                Field("error", "string"),
            };
            foreach (string name in optionalFields ?? new string[0])
                fields.Add(Field(name, JsonValue()));
            return Object(fields, "error");
        }

        internal static Dictionary<string, object> VmAutomationError()
        {
            return Object(new[]
            {
                Field("success", "boolean"),
                Field("error", "string"),
                Field("message", "string"),
                Field("errorCode", "string"),
                Field("retryable", "boolean"),
            }, "success", "error", "message", "errorCode", "retryable");
        }
    }
}
