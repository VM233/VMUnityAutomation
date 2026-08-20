using System;
using System.Collections.Generic;
using System.Linq;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationUICommandArguments
    {
    internal static string GetString(Dictionary<string, object> args, string key)
    {
        return args != null && args.ContainsKey(key) && args[key] != null ? args[key].ToString() : "";
    }

    internal static bool GetBool(Dictionary<string, object> args, string key, bool defaultValue)
    {
        if (args == null || !args.ContainsKey(key) || args[key] == null)
            return defaultValue;

        if (args[key] is bool value)
            return value;

        return bool.TryParse(args[key].ToString(), out bool parsed) ? parsed : defaultValue;
    }

    internal static int GetInt(Dictionary<string, object> args, string key, int defaultValue)
    {
        if (args == null || !args.ContainsKey(key) || args[key] == null)
            return defaultValue;

        return int.TryParse(args[key].ToString(), out int parsed) ? parsed : defaultValue;
    }

    internal static bool TryGetObjectId(Dictionary<string, object> args, string key, out object id)
    {
        id = null;
        if (args == null || !args.ContainsKey(key) || args[key] == null)
            return false;

        string text = args[key].ToString();
        if (!IsObjectIdString(text))
            return false;

        id = args[key];
        return true;
    }

    internal static bool IsObjectIdString(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        int digitStart = value[0] == '-' ? 1 : 0;
        if (digitStart == value.Length)
            return false;

        bool hasNonZeroDigit = false;
        for (int index = digitStart; index < value.Length; index++)
        {
            char character = value[index];
            if (character < '0' || character > '9')
                return false;

            hasNonZeroDigit |= character != '0';
        }

        return hasNonZeroDigit;
    }

    internal static float GetFloat(Dictionary<string, object> args, string key, float defaultValue)
    {
        if (args == null || !args.ContainsKey(key) || args[key] == null)
            return defaultValue;

        return float.TryParse(args[key].ToString(), out float parsed) ? parsed : defaultValue;
    }

    internal static List<object> GetObjectList(Dictionary<string, object> args, string key)
    {
        if (args == null || args.TryGetValue(key, out object value) == false || value == null)
            return new List<object>();

        if (value is List<object> list)
            return list;

        return new List<object> { value };
    }

    internal static Dictionary<string, object> AsDictionary(object value)
    {
        return value as Dictionary<string, object> ?? new Dictionary<string, object>();
    }

    internal static List<string> GetStringList(Dictionary<string, object> args, string arrayKey, string singleKey)
    {
        var results = new List<string>();
        if (args == null)
            return results;

        if (args.TryGetValue(arrayKey, out object arrayValue) &&
            arrayValue is System.Collections.IEnumerable enumerable && arrayValue is string == false)
        {
            foreach (object item in enumerable)
            {
                if (item != null)
                    results.Add(item.ToString());
            }

            if (string.Equals(arrayKey, singleKey, StringComparison.Ordinal))
                return results;
        }

        string singleValue = GetString(args, singleKey);
        if (string.IsNullOrEmpty(singleValue) == false &&
            results.Contains(singleValue, StringComparer.OrdinalIgnoreCase) == false)
        {
            results.Add(singleValue);
        }

        return results;
    }


    }
}
