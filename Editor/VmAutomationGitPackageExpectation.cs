using System;
using System.Collections.Generic;

namespace VMUnityAutomation.Editor
{
    internal sealed class VmAutomationGitPackageExpectation
    {
        internal VmAutomationGitPackageExpectation(string name, string identifier, string revision)
        {
            Name = name;
            Identifier = identifier;
            Revision = revision;
        }

        internal string Name { get; }
        internal string Identifier { get; }
        internal string Revision { get; }

        internal Dictionary<string, object> ToDictionary()
        {
            return new Dictionary<string, object>
            {
                { "name", Name },
                { "identifier", Identifier },
                { "revision", Revision },
            };
        }

        internal static VmAutomationGitPackageExpectation FromDictionary(
            Dictionary<string, object> values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));

            string name = GetString(values, "name");
            string identifier = GetString(values, "identifier");
            string revision = GetString(values, "revision");
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(identifier) ||
                string.IsNullOrWhiteSpace(revision))
            {
                throw new ArgumentException(
                    "A persisted Git package expectation requires name, identifier, and revision.");
            }

            return new VmAutomationGitPackageExpectation(name, identifier, revision);
        }

        private static string GetString(Dictionary<string, object> values, string key)
        {
            return values.TryGetValue(key, out object value) && value != null
                ? value.ToString()
                : "";
        }
    }
}
