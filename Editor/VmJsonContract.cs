using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace VMUnityAutomation.Editor
{
    public static class VmJsonContract
    {
        public const string DataProductKeyword = "x-vmAutomationContract";

        public static Dictionary<string, object> CreateSchema(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            return VmAutomationToolSchemaFactory.WithJsonValueDefinition(
                CreateSchema(type, new HashSet<Type>()));
        }

        public static object Bind(object value, Type targetType)
        {
            if (targetType == null)
                throw new ArgumentNullException(nameof(targetType));

            Type nullableType = Nullable.GetUnderlyingType(targetType);
            if (value == null)
            {
                if (targetType.IsValueType && nullableType == null)
                    throw new InvalidOperationException($"Cannot bind null to '{targetType.FullName}'.");
                return null;
            }

            if (nullableType != null)
                return Bind(value, nullableType);
            if (targetType == typeof(object))
                return value;
            if (targetType.IsInstanceOfType(value))
                return value;
            if (targetType == typeof(string))
                return value.ToString();
            if (targetType == typeof(char))
            {
                string character = value.ToString();
                if (character.Length != 1)
                    throw new InvalidOperationException($"'{character}' is not one character.");
                return character[0];
            }
            if (targetType == typeof(Guid))
                return Guid.Parse(value.ToString());
            if (targetType == typeof(DateTime))
                return DateTime.Parse(value.ToString(), CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind);
            if (targetType == typeof(DateTimeOffset))
                return DateTimeOffset.Parse(value.ToString(), CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind);
            if (targetType == typeof(bool))
                return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
            if (targetType.IsEnum)
                return ParseEnum(targetType, value.ToString());
            if (IsNumeric(targetType))
                return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
            if (TryGetDictionaryValueType(targetType, out Type dictionaryValueType))
                return BindDictionary(value, targetType, dictionaryValueType);
            if (targetType.IsArray)
                return BindArray(value, targetType.GetElementType());
            if (TryGetListElementType(targetType, out Type elementType))
                return BindList(value, targetType, elementType);
            if (value is IDictionary<string, object> dictionary)
                return BindObject(dictionary, targetType);
            if (value is IDictionary untypedDictionary)
                return BindObject(ToStringDictionary(untypedDictionary), targetType);

            throw new InvalidOperationException(
                $"Cannot bind '{value.GetType().FullName}' to '{targetType.FullName}'.");
        }

        public static object ToTransportValue(object value)
        {
            return ToTransportValue(value, new HashSet<object>(ReferenceEqualityComparer.Instance));
        }

        private static Dictionary<string, object> CreateSchema(Type type, ISet<Type> activeTypes)
        {
            Type nullableType = Nullable.GetUnderlyingType(type);
            if (nullableType != null)
            {
                Dictionary<string, object> nullableSchema = CreateSchema(nullableType, activeTypes);
                if (!(nullableSchema.TryGetValue("type", out object rawType) &&
                      rawType is string typeName))
                {
                    throw new InvalidOperationException(
                        $"Nullable Automation JSON contract '{type.FullName}' must declare one concrete JSON type.");
                }
                nullableSchema["type"] = new List<object> { typeName, "null" };
                return nullableSchema;
            }

            Dictionary<string, object> schema;
            if (type == typeof(string) || type == typeof(char) || type == typeof(Guid) ||
                type == typeof(DateTime) || type == typeof(DateTimeOffset))
            {
                schema = TypeSchema("string");
            }
            else if (type == typeof(bool))
            {
                schema = TypeSchema("boolean");
            }
            else if (type.IsEnum)
            {
                schema = TypeSchema("string");
                List<FieldInfo> fields = GetEnumFields(type).ToList();
                List<string> values = fields.Select(GetEnumJsonValue).ToList();
                if (values.Distinct(StringComparer.OrdinalIgnoreCase).Count() != values.Count)
                {
                    throw new InvalidOperationException(
                        $"Enum Automation JSON contract '{type.FullName}' declares duplicate JSON values.");
                }
                schema["enum"] = values.Cast<object>().ToList();
            }
            else if (IsInteger(type))
            {
                schema = TypeSchema("integer");
            }
            else if (IsNumeric(type))
            {
                schema = TypeSchema("number");
            }
            else if (TryGetDictionaryValueType(type, out Type dictionaryValueType))
            {
                schema = TypeSchema("object");
                schema["additionalProperties"] = dictionaryValueType == typeof(object)
                    ? new Dictionary<string, object>
                    {
                        { "$ref", VmAutomationToolSchemaFactory.JsonValueReference },
                    }
                    : CreateSchema(dictionaryValueType, activeTypes);
            }
            else if (type.IsArray)
            {
                schema = ArraySchema(type.GetElementType(), activeTypes);
            }
            else if (TryGetListElementType(type, out Type elementType))
            {
                schema = ArraySchema(elementType, activeTypes);
            }
            else if (typeof(IDictionary).IsAssignableFrom(type) ||
                     typeof(IEnumerable).IsAssignableFrom(type))
            {
                throw new InvalidOperationException(
                    $"Automation JSON contract '{type.FullName}' uses an unsupported collection shape. " +
                    "Use a string-keyed dictionary, array, List<T>, IList<T>, or IReadOnlyList<T>.");
            }
            else if (type == typeof(object))
            {
                schema = new Dictionary<string, object>
                {
                    { "$ref", VmAutomationToolSchemaFactory.JsonValueReference },
                };
            }
            else
            {
                schema = ObjectSchema(type, activeTypes);
            }

            VmDataProductAttribute product = type.GetCustomAttribute<VmDataProductAttribute>(false);
            if (product != null)
                schema[DataProductKeyword] = product.ContractId;
            return schema;
        }

        private static Dictionary<string, object> ObjectSchema(Type type, ISet<Type> activeTypes)
        {
            if (!activeTypes.Add(type))
                throw new InvalidOperationException($"Recursive Automation JSON contract '{type.FullName}' is not supported.");

            try
            {
                var properties = new Dictionary<string, object>(StringComparer.Ordinal);
                var required = new List<object>();
                foreach (MemberInfo member in GetSerializableMembers(type))
                {
                    Type memberType = GetMemberType(member);
                    string name = GetJsonName(member);
                    if (properties.ContainsKey(name))
                    {
                        throw new InvalidOperationException(
                            $"Automation JSON contract '{type.FullName}' declares JSON property '{name}' more than once.");
                    }
                    Dictionary<string, object> memberSchema = CreateSchema(memberType, activeTypes);
                    DescriptionAttribute description = member.GetCustomAttribute<DescriptionAttribute>(true);
                    if (description != null && string.IsNullOrWhiteSpace(description.Description) == false)
                        memberSchema["description"] = description.Description.Trim();
                    ApplyMemberConstraints(member, memberType, memberSchema);
                    properties[name] = memberSchema;
                    if (member.GetCustomAttribute<VmRequiredAttribute>(true) != null)
                        required.Add(name);
                }

                var schema = new Dictionary<string, object>
                {
                    { "type", "object" },
                    { "properties", properties },
                    { "additionalProperties", false },
                };
                if (required.Count > 0)
                    schema["required"] = required;
                return schema;
            }
            finally
            {
                activeTypes.Remove(type);
            }
        }

        private static Dictionary<string, object> ArraySchema(Type elementType, ISet<Type> activeTypes)
        {
            return new Dictionary<string, object>
            {
                { "type", "array" },
                { "items", CreateSchema(elementType, activeTypes) },
            };
        }

        private static Dictionary<string, object> TypeSchema(string type)
        {
            return new Dictionary<string, object> { { "type", type } };
        }

        private static void ApplyMemberConstraints(MemberInfo member, Type memberType,
            Dictionary<string, object> schema)
        {
            Type concreteType = Nullable.GetUnderlyingType(memberType) ?? memberType;
            VmRangeAttribute range = member.GetCustomAttribute<VmRangeAttribute>(true);
            if (range != null)
            {
                if (!IsNumeric(concreteType))
                    throw new InvalidOperationException(
                        $"VmRange requires a numeric member, but '{member.DeclaringType?.FullName}.{member.Name}' is '{memberType.FullName}'.");
                schema["minimum"] = range.Minimum;
                schema["maximum"] = range.Maximum;
            }

            VmMinLengthAttribute minLength =
                member.GetCustomAttribute<VmMinLengthAttribute>(true);
            if (minLength != null)
            {
                if (concreteType != typeof(string))
                    throw new InvalidOperationException(
                        $"VmMinLength requires a string member, but '{member.DeclaringType?.FullName}.{member.Name}' is '{memberType.FullName}'.");
                schema["minLength"] = minLength.Length;
            }

            VmMinItemsAttribute minItems =
                member.GetCustomAttribute<VmMinItemsAttribute>(true);
            if (minItems != null)
            {
                if (!memberType.IsArray && !TryGetListElementType(memberType, out _))
                    throw new InvalidOperationException(
                        $"VmMinItems requires an array or list member, but '{member.DeclaringType?.FullName}.{member.Name}' is '{memberType.FullName}'.");
                schema["minItems"] = minItems.Count;
            }

            VmDefaultSourceAttribute defaultSource =
                member.GetCustomAttribute<VmDefaultSourceAttribute>(true);
            if (defaultSource != null)
            {
                schema["x-unityMcpDefaultSource"] = defaultSource.Source;
                schema["x-unityMcpExplicitValueWins"] = defaultSource.ExplicitValueWins;
            }
        }

        private static object BindArray(object value, Type elementType)
        {
            IList source = RequireList(value);
            Array result = Array.CreateInstance(elementType, source.Count);
            for (int index = 0; index < source.Count; index++)
                result.SetValue(Bind(source[index], elementType), index);
            return result;
        }

        private static object BindList(object value, Type targetType, Type elementType)
        {
            IList source = RequireList(value);
            Type concreteType = targetType.IsInterface || targetType.IsAbstract
                ? typeof(List<>).MakeGenericType(elementType)
                : targetType;
            if (!(Activator.CreateInstance(concreteType) is IList result))
                throw new InvalidOperationException($"List contract '{targetType.FullName}' is not constructible.");
            foreach (object item in source)
                result.Add(Bind(item, elementType));
            return result;
        }

        private static object BindDictionary(object value, Type targetType, Type valueType)
        {
            IDictionary<string, object> source;
            if (value is IDictionary<string, object> stringDictionary)
                source = stringDictionary;
            else if (value is IDictionary dictionary)
                source = ToStringDictionary(dictionary);
            else
                throw new InvalidOperationException(
                    $"Expected a JSON object, received '{value.GetType().FullName}'.");
            Type concreteType = targetType.IsInterface || targetType.IsAbstract
                ? typeof(Dictionary<,>).MakeGenericType(typeof(string), valueType)
                : targetType;
            if (!(Activator.CreateInstance(concreteType) is IDictionary result))
                throw new InvalidOperationException($"Dictionary contract '{targetType.FullName}' is not constructible.");
            foreach (KeyValuePair<string, object> pair in source)
                result[pair.Key] = Bind(pair.Value, valueType);
            return result;
        }

        private static object BindObject(IDictionary<string, object> source, Type targetType)
        {
            object result = Activator.CreateInstance(targetType) ??
                            throw new InvalidOperationException(
                                $"JSON contract '{targetType.FullName}' requires a public parameterless constructor.");
            var membersByName = GetSerializableMembers(targetType)
                .ToDictionary(GetJsonName, StringComparer.Ordinal);
            foreach (KeyValuePair<string, object> pair in source)
            {
                if (!membersByName.TryGetValue(pair.Key, out MemberInfo member))
                    throw new InvalidOperationException(
                        $"JSON property '{pair.Key}' is not declared by '{targetType.FullName}'.");
                SetMemberValue(member, result, Bind(pair.Value, GetMemberType(member)));
            }
            return result;
        }

        private static object ToTransportValue(object value, ISet<object> visited)
        {
            if (value == null)
                return null;
            Type type = value.GetType();
            if (type == typeof(string) || type == typeof(bool) ||
                IsNumeric(type))
                return value;
            if (type == typeof(char))
                return value.ToString();
            if (type.IsEnum)
                return FormatEnum(value);
            if (value is Guid)
                return value.ToString();
            if (value is DateTime dateTime)
                return dateTime.ToString("O", CultureInfo.InvariantCulture);
            if (value is DateTimeOffset dateTimeOffset)
                return dateTimeOffset.ToString("O", CultureInfo.InvariantCulture);

            if (!type.IsValueType && !visited.Add(value))
                throw new InvalidOperationException($"Cyclic Automation result contract '{type.FullName}' is not supported.");
            try
            {
                if (value is IDictionary dictionary)
                {
                    var dictionaryResult = new Dictionary<string, object>(StringComparer.Ordinal);
                    foreach (DictionaryEntry pair in dictionary)
                    {
                        if (!(pair.Key is string key))
                        {
                            throw new InvalidOperationException(
                                $"Automation result dictionary '{type.FullName}' contains a non-string key.");
                        }
                        dictionaryResult[key] = ToTransportValue(pair.Value, visited);
                    }
                    return dictionaryResult;
                }
                if (value is IEnumerable enumerable)
                {
                    var listResult = new List<object>();
                    foreach (object item in enumerable)
                        listResult.Add(ToTransportValue(item, visited));
                    return listResult;
                }

                var result = new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (MemberInfo member in GetSerializableMembers(type))
                {
                    object memberValue = GetMemberValue(member, value);
                    if (memberValue == null &&
                        member.GetCustomAttribute<VmRequiredAttribute>(true) == null)
                    {
                        continue;
                    }
                    result[GetJsonName(member)] = ToTransportValue(memberValue, visited);
                }
                return result;
            }
            finally
            {
                if (!type.IsValueType)
                    visited.Remove(value);
            }
        }

        private static IEnumerable<MemberInfo> GetSerializableMembers(Type type)
        {
            IEnumerable<MemberInfo> fields = type
                .GetFields(BindingFlags.Instance | BindingFlags.Public)
                .Where(field => !field.IsInitOnly)
                .Cast<MemberInfo>();
            IEnumerable<MemberInfo> properties = type
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(property => property.CanRead && property.CanWrite &&
                                   property.GetIndexParameters().Length == 0)
                .Cast<MemberInfo>();
            return fields.Concat(properties)
                .OrderBy(GetJsonName, StringComparer.Ordinal);
        }

        private static string GetJsonName(MemberInfo member)
        {
            return member.GetCustomAttribute<VmJsonPropertyAttribute>(true)?.Name ?? member.Name;
        }

        private static Type GetMemberType(MemberInfo member)
        {
            return member is FieldInfo field ? field.FieldType : ((PropertyInfo)member).PropertyType;
        }

        private static object GetMemberValue(MemberInfo member, object instance)
        {
            return member is FieldInfo field
                ? field.GetValue(instance)
                : ((PropertyInfo)member).GetValue(instance);
        }

        private static void SetMemberValue(MemberInfo member, object instance, object value)
        {
            if (member is FieldInfo field)
                field.SetValue(instance, value);
            else
                ((PropertyInfo)member).SetValue(instance, value);
        }

        private static IList RequireList(object value)
        {
            if (value is IList list)
                return list;
            throw new InvalidOperationException($"Expected a JSON array, received '{value.GetType().FullName}'.");
        }

        private static Dictionary<string, object> ToStringDictionary(IDictionary source)
        {
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (DictionaryEntry pair in source)
                result[pair.Key.ToString()] = pair.Value;
            return result;
        }

        private static bool TryGetListElementType(Type type, out Type elementType)
        {
            Type listType = null;
            if (type.IsGenericType)
            {
                Type definition = type.GetGenericTypeDefinition();
                if (definition == typeof(List<>) || definition == typeof(IList<>) ||
                    definition == typeof(IReadOnlyList<>))
                {
                    listType = type;
                }
            }
            listType ??= type.GetInterfaces().FirstOrDefault(candidate => candidate.IsGenericType &&
                (candidate.GetGenericTypeDefinition() == typeof(IList<>) ||
                 candidate.GetGenericTypeDefinition() == typeof(IReadOnlyList<>)));
            elementType = listType?.GetGenericArguments()[0];
            return elementType != null && type != typeof(string);
        }

        private static bool TryGetDictionaryValueType(Type type, out Type valueType)
        {
            Type dictionaryType = null;
            if (type.IsGenericType)
            {
                Type definition = type.GetGenericTypeDefinition();
                if (definition == typeof(Dictionary<,>) || definition == typeof(IDictionary<,>))
                    dictionaryType = type;
            }
            dictionaryType ??= type.GetInterfaces().FirstOrDefault(candidate => candidate.IsGenericType &&
                candidate.GetGenericTypeDefinition() == typeof(IDictionary<,>));
            Type[] arguments = dictionaryType?.GetGenericArguments();
            valueType = arguments != null && arguments.Length == 2 && arguments[0] == typeof(string)
                ? arguments[1]
                : null;
            return valueType != null;
        }

        private static bool IsInteger(Type type)
        {
            return type == typeof(byte) || type == typeof(sbyte) || type == typeof(short) ||
                   type == typeof(ushort) || type == typeof(int) || type == typeof(uint) ||
                   type == typeof(long) || type == typeof(ulong);
        }

        private static bool IsNumeric(Type type)
        {
            return IsInteger(type) || type == typeof(float) || type == typeof(double) ||
                   type == typeof(decimal);
        }

        private static object ParseEnum(Type enumType, string value)
        {
            FieldInfo field = GetEnumFields(enumType).SingleOrDefault(candidate =>
                string.Equals(GetEnumJsonValue(candidate), value,
                    StringComparison.OrdinalIgnoreCase));
            if (field == null)
                throw new InvalidOperationException(
                    $"'{value}' is not a declared JSON value for enum '{enumType.FullName}'.");
            return field.GetValue(null);
        }

        private static string FormatEnum(object value)
        {
            Type enumType = value.GetType();
            string name = Enum.GetName(enumType, value) ??
                          throw new InvalidOperationException(
                              $"'{value}' is not a declared value of enum '{enumType.FullName}'.");
            FieldInfo field = enumType.GetField(name, BindingFlags.Public | BindingFlags.Static);
            return GetEnumJsonValue(field);
        }

        private static IEnumerable<FieldInfo> GetEnumFields(Type enumType)
        {
            return enumType.GetFields(BindingFlags.Public | BindingFlags.Static)
                .OrderBy(field => field.MetadataToken);
        }

        private static string GetEnumJsonValue(FieldInfo field)
        {
            return field.GetCustomAttribute<VmJsonEnumValueAttribute>(false)?.Value ??
                   field.Name;
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            internal static readonly ReferenceEqualityComparer Instance = new();

            public new bool Equals(object left, object right)
            {
                return ReferenceEquals(left, right);
            }

            public int GetHashCode(object value)
            {
                return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value);
            }
        }
    }
}
