using System;
using System.Collections.Generic;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationAnimationCommandUtility
    {
    internal static List<string> GetStringList(Dictionary<string, object> args, string key)
    {
        var result = new List<string>();
        foreach (var value in GetObjectList(args, key))
        {
            if (value != null)
                result.Add(value.ToString());
        }

        return result;
    }

    internal static List<int> GetIntList(Dictionary<string, object> args, string key)
    {
        var result = new List<int>();
        foreach (var value in GetObjectList(args, key))
        {
            if (value != null && int.TryParse(value.ToString(), out int parsed))
                result.Add(parsed);
        }

        return result;
    }

    internal static List<object> GetObjectList(Dictionary<string, object> args, string key)
    {
        if (args == null || !args.TryGetValue(key, out object value) || value == null)
            return new List<object>();

        return value is List<object> values ? values : new List<object> { value };
    }


    }
}
