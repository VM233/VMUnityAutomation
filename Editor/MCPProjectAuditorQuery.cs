#if UNITY_6000_4_OR_NEWER
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.ProjectAuditor.Editor;
using Unity.ProjectAuditor.Editor.Core;

namespace VMUnityAutomation.Editor
{
    internal sealed class MCPProjectAuditorQuery
    {
        internal const int DefaultLimit = 100;
        internal const int MaximumLimit = 500;

        private readonly HashSet<IssueCategory> categorySet;
        private readonly HashSet<string> descriptorIdSet;
        private readonly HashSet<Severity> severitySet;

        private MCPProjectAuditorQuery(IssueCategory[] categories,
            string[] descriptorIds, Severity[] severities, int offset, int limit)
        {
            Categories = categories;
            DescriptorIds = descriptorIds;
            Severities = severities;
            Offset = offset;
            Limit = limit;
            categorySet = new HashSet<IssueCategory>(categories);
            descriptorIdSet = new HashSet<string>(descriptorIds,
                StringComparer.Ordinal);
            severitySet = new HashSet<Severity>(severities);
        }

        internal IReadOnlyList<IssueCategory> Categories { get; }
        internal IReadOnlyList<string> DescriptorIds { get; }
        internal IReadOnlyList<Severity> Severities { get; }
        internal int Offset { get; }
        internal int Limit { get; }

        internal static bool TryCreate(object rawCategories,
            object rawDescriptorIds, object rawSeverities, object rawOffset,
            object rawLimit,
            out MCPProjectAuditorQuery query,
            out Dictionary<string, object> error)
        {
            query = null;
            error = null;

            if (!TryReadStringArray(rawCategories, "categories", out string[] categoryNames,
                    out error) ||
                !TryReadStringArray(rawDescriptorIds, "descriptorIds", out string[] descriptorIds,
                    out error) ||
                !TryReadStringArray(rawSeverities, "severities", out string[] severityNames,
                    out error) ||
                !TryReadInteger(rawOffset, "offset", 0, out int offset, out error) ||
                !TryReadInteger(rawLimit, "limit", DefaultLimit, out int limit, out error))
                return false;

            if (offset < 0)
            {
                error = InvalidArgument("offset", "must be zero or greater");
                return false;
            }

            if (limit < 1 || limit > MaximumLimit)
            {
                error = InvalidArgument("limit",
                    $"must be between 1 and {MaximumLimit}");
                return false;
            }

            if (!TryParseEnums(categoryNames, "categories",
                    out IssueCategory[] categories, out error) ||
                !TryParseEnums(severityNames, "severities",
                    out Severity[] severities, out error))
                return false;

            query = new MCPProjectAuditorQuery(categories, descriptorIds,
                severities, offset, limit);
            return true;
        }

        internal SerializableEnum<IssueCategory>[] CreateAnalysisCategories()
        {
            return Categories.Select(category =>
                new SerializableEnum<IssueCategory>(category)).ToArray();
        }

        internal bool Matches(ReportItem issue)
        {
            return (categorySet.Count == 0 || categorySet.Contains(issue.Category)) &&
                   (descriptorIdSet.Count == 0 ||
                    descriptorIdSet.Contains(issue.Id.ToString())) &&
                   (severitySet.Count == 0 || severitySet.Contains(issue.Severity));
        }

        private static bool TryReadStringArray(object rawValue, string name,
            out string[] values, out Dictionary<string, object> error)
        {
            error = null;
            if (rawValue == null)
            {
                values = Array.Empty<string>();
                return true;
            }

            if (!(rawValue is IList list))
            {
                values = null;
                error = InvalidArgument(name, "must be an array of strings");
                return false;
            }

            if (list.Count == 0)
            {
                values = null;
                error = InvalidArgument(name, "must contain at least one value when supplied");
                return false;
            }

            var result = new List<string>(list.Count);
            var unique = new HashSet<string>(StringComparer.Ordinal);
            foreach (object item in list)
            {
                if (!(item is string text) || string.IsNullOrWhiteSpace(text))
                {
                    values = null;
                    error = InvalidArgument(name,
                        "must contain only non-empty strings");
                    return false;
                }

                text = text.Trim();
                if (!unique.Add(text))
                {
                    values = null;
                    error = InvalidArgument(name, $"contains duplicate value '{text}'");
                    return false;
                }
                result.Add(text);
            }

            values = result.ToArray();
            return true;
        }

        private static bool TryReadInteger(object rawValue, string name,
            int defaultValue, out int value,
            out Dictionary<string, object> error)
        {
            error = null;
            if (rawValue == null)
            {
                value = defaultValue;
                return true;
            }

            if (rawValue is int intValue)
            {
                value = intValue;
                return true;
            }
            if (rawValue is long longValue && longValue >= int.MinValue &&
                longValue <= int.MaxValue)
            {
                value = (int)longValue;
                return true;
            }
            if (rawValue is double doubleValue &&
                doubleValue == Math.Truncate(doubleValue) &&
                doubleValue >= int.MinValue && doubleValue <= int.MaxValue)
            {
                value = (int)doubleValue;
                return true;
            }

            value = 0;
            error = InvalidArgument(name, "must be an integer");
            return false;
        }

        private static bool TryParseEnums<T>(IEnumerable<string> names,
            string argumentName, out T[] values,
            out Dictionary<string, object> error) where T : struct
        {
            var result = new List<T>();
            foreach (string name in names)
            {
                if (!Enum.TryParse(name, true, out T parsed) ||
                    !Enum.IsDefined(typeof(T), parsed))
                {
                    values = null;
                    error = InvalidArgument(argumentName,
                        $"contains unsupported value '{name}'");
                    return false;
                }
                result.Add(parsed);
            }

            values = result.ToArray();
            error = null;
            return true;
        }

        private static Dictionary<string, object> InvalidArgument(string name,
            string requirement)
        {
            return MCPResponse.Error(
                $"Project Auditor argument '{name}' {requirement}.",
                "invalid_arguments");
        }
    }
}
#endif
