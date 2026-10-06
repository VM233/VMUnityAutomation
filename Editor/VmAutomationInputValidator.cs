using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace VMUnityAutomation.Editor
{
    /// <summary>Evaluates the catalog's JSON input dialect before owner admission.</summary>
    internal sealed class VmAutomationInputValidator
    {
        internal const int MaximumWorkUnits = 65536;
        internal const int MaximumInputDepth = 64;
        private const int MaximumSchemaDepth = 256;
        private readonly Dictionary<string, object> root;
        private readonly List<PathSegment> path = new List<PathSegment>();
        private int workUnits;
        private string message;
        private Dictionary<string, object> details;

        private VmAutomationInputValidator(Dictionary<string, object> schema)
        {
            root = schema;
        }

        internal static bool TryValidate(object value, Dictionary<string, object> schema,
            out string errorCode, out string errorMessage,
            out Dictionary<string, object> errorDetails)
        {
            var validator = new VmAutomationInputValidator(schema);
            bool valid;
            errorCode = "invalid_arguments";
            try
            {
                valid = validator.Validate(value, schema, true, 0);
            }
            catch (EvaluationLimitException exception)
            {
                valid = validator.Reject(exception.Keyword, exception.Message, true);
                errorCode = "input_validation_limit";
            }
            errorMessage = validator.message;
            errorDetails = validator.details;
            return valid;
        }

        private bool Validate(object value, Dictionary<string, object> schema,
            bool report, int schemaDepth)
        {
            Consume();
            if (schemaDepth > MaximumSchemaDepth)
                throw new EvaluationLimitException("schemaDepth", "Input schema evaluation exceeds its depth capacity.");
            if (schema.TryGetValue("$ref", out object reference))
            {
                Dictionary<string, object> target = ResolveReference((string)reference);
                if (!Validate(value, target, report, schemaDepth + 1))
                    return false;
            }
            if (schema.TryGetValue("type", out object type) && !MatchesType(value, type))
                return Reject("type", "Value has a different JSON type from the published input contract.", report);
            if (schema.TryGetValue("const", out object constant) && !ValuesEqual(value, constant))
                return Reject("const", "Value must equal the published constant.", report);
            if (schema.TryGetValue("enum", out object choices) && !MatchesEnum(value, (IEnumerable)choices))
                return Reject("enum", "Value must equal one of the published enum values, including case.", report);

            if (value is IDictionary<string, object> members && !ValidateObject(members, schema, report, schemaDepth))
                return false;
            if (value is IList items && !ValidateArray(items, schema, report, schemaDepth))
                return false;
            if (value is string text && !ValidateString(text, schema, report))
                return false;
            if (TryNumber(value, out double number))
            {
                if (double.IsNaN(number) || double.IsInfinity(number))
                    return Reject("type", "JSON numbers must be finite.", report);
                if (!ValidateBounds(number, schema, "minimum", "maximum", report))
                    return false;
            }
            if (!ValidateBranches(value, schema, "allOf", report, schemaDepth) ||
                !ValidateBranches(value, schema, "anyOf", report, schemaDepth) ||
                !ValidateBranches(value, schema, "oneOf", report, schemaDepth))
                return false;
            if (schema.TryGetValue("not", out object excluded) &&
                Validate(value, (Dictionary<string, object>)excluded, false, schemaDepth + 1))
                return Reject("not", "Value matches an excluded input combination.", report);
            return true;
        }

        private bool ValidateObject(IDictionary<string, object> value,
            Dictionary<string, object> schema, bool report, int depth)
        {
            if (schema.TryGetValue("required", out object required))
            {
                foreach (string name in (IEnumerable)required)
                {
                    Consume();
                    if (!value.ContainsKey(name))
                        return Reject("required", "Required input property '" + name + "' is missing.", report);
                }
            }
            var properties = schema.TryGetValue("properties", out object declared)
                ? (Dictionary<string, object>)declared : null;
            schema.TryGetValue("additionalProperties", out object additional);
            if (properties == null && additional == null)
                return true;
            foreach (KeyValuePair<string, object> member in value)
            {
                Consume();
                object child = null;
                bool known = properties != null && properties.TryGetValue(member.Key, out child);
                if (!known && additional is bool allowed && !allowed)
                {
                    Push(member.Key, -1);
                    bool result = Reject("additionalProperties", "Input property is not declared by this command.", report);
                    path.RemoveAt(path.Count - 1);
                    return result;
                }
                if (!known)
                    child = additional as Dictionary<string, object>;
                if (child is Dictionary<string, object> childSchema)
                {
                    Push(member.Key, -1);
                    bool result = Validate(member.Value, childSchema, report, depth + 1);
                    path.RemoveAt(path.Count - 1);
                    if (!result)
                        return false;
                }
            }
            return true;
        }

        private bool ValidateArray(IList value, Dictionary<string, object> schema,
            bool report, int depth)
        {
            if (!ValidateBounds(value.Count, schema, "minItems", "maxItems", report))
                return false;
            HashSet<string> unique = schema.TryGetValue("uniqueItems", out object distinct) && distinct is true
                ? new HashSet<string>(StringComparer.Ordinal) : null;
            schema.TryGetValue("items", out object itemSchema);
            for (int index = 0; index < value.Count; index++)
            {
                Consume();
                Push(null, index);
                bool valid = !(itemSchema is Dictionary<string, object> child) ||
                    Validate(value[index], child, report, depth + 1);
                if (valid && unique != null)
                {
                    MeasureUniqueValue(value[index], 0);
                    if (!unique.Add(VmAutomationCanonicalJson.Serialize(value[index])))
                        valid = Reject("uniqueItems", "Input array contains a duplicate JSON value.", report);
                }
                path.RemoveAt(path.Count - 1);
                if (!valid)
                    return false;
            }
            return true;
        }

        private bool ValidateString(string value, Dictionary<string, object> schema, bool report)
        {
            if (!ValidateBounds(value.Length, schema, "minLength", "maxLength", report))
                return false;
            if (!schema.TryGetValue("pattern", out object pattern))
                return true;
            try
            {
                if (!Regex.IsMatch(value, (string)pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(50)))
                    return Reject("pattern", "String does not match the published input pattern.", report);
            }
            catch (RegexMatchTimeoutException)
            {
                throw new EvaluationLimitException("pattern", "Input pattern evaluation exceeds its time capacity.");
            }
            return true;
        }

        private bool ValidateBounds(double value, Dictionary<string, object> schema,
            string minimum, string maximum, bool report)
        {
            if (schema.TryGetValue(minimum, out object lower) && value < Convert.ToDouble(lower, CultureInfo.InvariantCulture))
                return Reject(minimum, "Value is below the published minimum.", report);
            if (schema.TryGetValue(maximum, out object upper) && value > Convert.ToDouble(upper, CultureInfo.InvariantCulture))
                return Reject(maximum, "Value exceeds the published maximum.", report);
            return true;
        }

        private bool ValidateBranches(object value, Dictionary<string, object> schema,
            string keyword, bool report, int depth)
        {
            if (!schema.TryGetValue(keyword, out object variants))
                return true;
            int count = 0;
            foreach (Dictionary<string, object> variant in (IEnumerable)variants)
            {
                Consume();
                bool valid = Validate(value, variant, keyword == "allOf" && report, depth + 1);
                if (keyword == "allOf" && !valid)
                    return false;
                if (valid)
                    count++;
            }
            if ((keyword == "anyOf" && count == 0) || (keyword == "oneOf" && count != 1))
                return Reject(keyword, "Input must match " + (keyword == "oneOf" ? "exactly one" : "at least one") + " published variant.", report);
            return true;
        }

        private bool MatchesEnum(object value, IEnumerable choices)
        {
            foreach (object choice in choices)
            {
                Consume();
                if (ValuesEqual(value, choice))
                    return true;
            }
            return false;
        }

        private bool MatchesType(object value, object type)
        {
            if (type is string name)
            {
                switch (name)
                {
                    case "null": return value == null;
                    case "object": return value is IDictionary<string, object>;
                    case "array": return value is IList;
                    case "string": return value is string;
                    case "boolean": return value is bool;
                    case "number": return TryNumber(value, out _);
                    case "integer": return TryNumber(value, out double number) && Math.Floor(number) == number;
                    default: throw new InvalidOperationException("Unsupported catalog JSON type '" + name + "'.");
                }
            }
            foreach (object alternative in (IEnumerable)type)
            {
                Consume();
                if (MatchesType(value, alternative))
                    return true;
            }
            return false;
        }

        private Dictionary<string, object> ResolveReference(string reference)
        {
            if (!reference.StartsWith("#/", StringComparison.Ordinal))
                throw new InvalidOperationException("Catalog input references must be local JSON pointers.");
            object target = root;
            foreach (string segment in reference.Substring(2).Split('/'))
            {
                Consume();
                target = ((Dictionary<string, object>)target)[segment.Replace("~1", "/").Replace("~0", "~")];
            }
            return (Dictionary<string, object>)target;
        }

        private void MeasureUniqueValue(object value, int depth)
        {
            Consume();
            if (depth > MaximumInputDepth)
                throw new EvaluationLimitException("inputDepth", "Unique JSON value exceeds its depth capacity.");
            if (value is IDictionary<string, object> members)
                foreach (object child in members.Values)
                    MeasureUniqueValue(child, depth + 1);
            else if (value is IList items)
                foreach (object child in items)
                    MeasureUniqueValue(child, depth + 1);
        }

        private void Consume()
        {
            if (++workUnits > MaximumWorkUnits)
                throw new EvaluationLimitException("workUnits", "Input validation exceeds the published work capacity.");
        }

        private void Push(string key, int index)
        {
            if (path.Count >= MaximumInputDepth)
                throw new EvaluationLimitException("inputDepth", "Input exceeds the published nesting capacity.");
            path.Add(new PathSegment { Key = key, Index = index });
        }

        private bool Reject(string keyword, string reason, bool report)
        {
            if (report)
            {
                var location = new StringBuilder("$");
                foreach (PathSegment segment in path)
                {
                    location.Append('[');
                    location.Append(segment.Key == null ? segment.Index.ToString(CultureInfo.InvariantCulture) : MiniJson.Serialize(segment.Key));
                    location.Append(']');
                }
                message = location + ": " + reason;
                details = new Dictionary<string, object>
                {
                    { "stage", "input-validation" }, { "path", location.ToString() }, { "keyword", keyword }
                };
            }
            return false;
        }

        private static bool TryNumber(object value, out double number)
        {
            number = 0;
            if (value == null)
                return false;
            switch (Type.GetTypeCode(value.GetType()))
            {
                case TypeCode.Byte: case TypeCode.SByte: case TypeCode.Int16: case TypeCode.UInt16:
                case TypeCode.Int32: case TypeCode.UInt32: case TypeCode.Int64: case TypeCode.UInt64:
                case TypeCode.Single: case TypeCode.Double: case TypeCode.Decimal:
                    number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                    return true;
                default: return false;
            }
        }

        private static bool ValuesEqual(object left, object right)
        {
            if (TryNumber(left, out _) && TryNumber(right, out _))
                return VmAutomationCanonicalJson.Serialize(left) == VmAutomationCanonicalJson.Serialize(right);
            return Equals(left, right);
        }

        private struct PathSegment
        {
            internal string Key;
            internal int Index;
        }

        private sealed class EvaluationLimitException : Exception
        {
            internal readonly string Keyword;
            internal EvaluationLimitException(string keyword, string message) : base(message)
            {
                Keyword = keyword;
            }
        }
    }
}
