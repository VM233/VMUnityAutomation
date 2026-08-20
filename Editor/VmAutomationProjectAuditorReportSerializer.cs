#if UNITY_6000_4_OR_NEWER
using System.Collections.Generic;
using System.Linq;
using Unity.ProjectAuditor.Editor;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationProjectAuditorReportSerializer
    {
        internal static Dictionary<string, object> Serialize(Report report,
            VmAutomationProjectAuditorQuery query, string platform, long elapsedMs)
        {
            return SerializeSnapshot(report.GetAllIssues(), report.ReportVersion,
                report.IsValid(), report.NumTotalIssues, query, platform, elapsedMs);
        }

        internal static Dictionary<string, object> SerializeSnapshot(
            IEnumerable<ReportItem> issues, string reportVersion, bool isValid,
            int totalIssueCount, VmAutomationProjectAuditorQuery query, string platform,
            long elapsedMs)
        {
            List<ReportItem> matches = issues
                .Where(query.Matches)
                .OrderBy(issue => issue.RelativePath, System.StringComparer.Ordinal)
                .ThenBy(issue => issue.Line)
                .ThenBy(issue => issue.Id.ToString(), System.StringComparer.Ordinal)
                .ThenBy(issue => issue.Description, System.StringComparer.Ordinal)
                .ToList();

            List<Dictionary<string, object>> page = matches
                .Skip(query.Offset)
                .Take(query.Limit)
                .Select(SerializeIssue)
                .ToList();

            var result = new Dictionary<string, object>
            {
                { "reportVersion", reportVersion },
                { "isValid", isValid },
                { "platform", platform },
                { "elapsedMs", elapsedMs },
                { "totalIssues", totalIssueCount },
                { "matchedIssueCount", matches.Count },
                { "offset", query.Offset },
                { "limit", query.Limit },
                { "issues", page },
            };

            int nextOffset = query.Offset + page.Count;
            if (nextOffset < matches.Count)
                result["nextOffset"] = nextOffset;
            return result;
        }

        private static Dictionary<string, object> SerializeIssue(ReportItem issue)
        {
            var result = new Dictionary<string, object>
            {
                { "descriptorId", issue.Id.ToString() },
                { "category", issue.Category.ToString() },
                { "severity", issue.Severity.ToString() },
                { "logLevel", issue.LogLevel.ToString() },
                { "description", issue.Description },
                { "path", issue.RelativePath },
                { "line", issue.Line },
            };

            string[] customProperties = issue.CustomProperties;
            if (customProperties != null && customProperties.Length > 0)
                result["customProperties"] = customProperties.ToList();
            return result;
        }
    }
}
#endif
