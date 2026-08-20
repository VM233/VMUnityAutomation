using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationCanonicalJson
    {
        internal static string ComputeSha256(object value)
        {
            string canonical = Serialize(value);
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(
                    Encoding.UTF8.GetBytes(canonical));
                return BitConverter.ToString(bytes).Replace("-", "")
                    .ToLowerInvariant();
            }
        }

        private static string Serialize(object value)
        {
            if (value == null)
                return "null";
            if (value is string text)
                return MiniJson.Serialize(text);
            if (value is bool boolean)
                return boolean ? "true" : "false";
            if (value is IDictionary dictionary)
            {
                var entries = new List<KeyValuePair<string, object>>();
                foreach (DictionaryEntry entry in dictionary)
                {
                    entries.Add(new KeyValuePair<string, object>(
                        entry.Key?.ToString() ?? "",
                        entry.Value));
                }

                return "{" + string.Join(",", entries
                    .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                    .Select(entry => MiniJson.Serialize(entry.Key) + ":" +
                                     Serialize(entry.Value))) + "}";
            }

            if (value is IEnumerable enumerable)
            {
                var values = new List<string>();
                foreach (object item in enumerable)
                    values.Add(Serialize(item));
                return "[" + string.Join(",", values) + "]";
            }

            if (value is IFormattable formattable)
                return formattable.ToString(null, CultureInfo.InvariantCulture);
            return MiniJson.Serialize(value.ToString());
        }
    }
}
