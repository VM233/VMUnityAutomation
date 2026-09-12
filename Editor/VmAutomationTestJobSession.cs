using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using TestJob = VMUnityAutomation.Editor.VmAutomationTestRunnerCommands.TestJob;
using TestJobStatus = VMUnityAutomation.Editor.VmAutomationTestRunnerCommands.TestJobStatus;
using TestResult = VMUnityAutomation.Editor.VmAutomationTestRunnerCommands.TestResult;

namespace VMUnityAutomation.Editor
{
    internal sealed class VmAutomationTestJobSession
    {
        private readonly string prefix;

        internal VmAutomationTestJobSession(string prefix) => this.prefix = prefix;

        internal void PublishMembership(IEnumerable<string> jobIds) =>
            SessionState.SetString(prefix + "/jobs", MiniJson.Serialize(jobIds.ToArray()));

        internal IReadOnlyList<string> ReadMembership()
        {
            string json = SessionState.GetString(prefix + "/jobs", "[]");
            return ((List<object>)MiniJson.Deserialize(json)).Select(value => (string)value).ToArray();
        }

        internal string CurrentJobId => SessionState.GetString(prefix + "/current", "");

        internal void PublishResult(string jobId, int index, TestResult result) =>
            SessionState.SetString(ResultKey(jobId, index), MiniJson.Serialize(new Dictionary<string, object>
            {
                { "fullName", result.FullName }, { "name", result.Name },
                { "status", result.Status }, { "duration", result.Duration },
                { "message", result.Message }, { "stackTrace", result.StackTrace }
            }));

        internal void ReplaceResults(string jobId, int previousCount, IReadOnlyList<TestResult> results)
        {
            for (int index = 0; index < results.Count; index++)
                PublishResult(jobId, index, results[index]);
            for (int index = results.Count; index < previousCount; index++)
                SessionState.EraseString(ResultKey(jobId, index));
        }

        internal void PublishJob(TestJob job, string currentJobId)
        {
            SessionState.SetString(JobKey(job.JobId), MiniJson.Serialize(new Dictionary<string, object>
            {
                { "jobId", job.JobId }, { "agentId", job.AgentId }, { "runGuid", job.RunGuid },
                { "cancelRequested", job.CancelRequested }, { "mode", job.Mode.ToString() },
                { "status", job.Status.ToString() }, { "startedAt", job.StartedAt.ToString("O") },
                { "completedAt", job.CompletedAt?.ToString("O") },
                { "lastUpdatedAt", job.LastUpdatedAt.ToString("O") },
                { "totalTests", job.TotalTests }, { "completedTests", job.CompletedTests },
                { "passedCount", job.PassedCount }, { "failedCount", job.FailedCount },
                { "skippedCount", job.SkippedCount }, { "totalDuration", job.TotalDuration },
                { "completionRecovered", job.CompletionRecovered },
                { "hasExplicitFilters", job.HasExplicitFilters },
                { "testNames", job.TestNames }, { "categories", job.Categories },
                { "assemblies", job.Assemblies },
                { "currentTestName", job.CurrentTestName },
                { "currentTestStartedAt", job.CurrentTestStartedAt?.ToString("O") },
                { "error", job.Error }, { "errorCode", job.ErrorCode }
            }));
            SessionState.SetString(prefix + "/current", currentJobId ?? "");
        }

        internal TestJob ReadJob(string jobId)
        {
            Dictionary<string, object> values = ReadRecord(JobKey(jobId));
            var job = new TestJob
            {
                JobId = (string)values["jobId"], AgentId = (string)values["agentId"],
                RunGuid = (string)values["runGuid"], CancelRequested = (bool)values["cancelRequested"],
                Mode = (TestMode)Enum.Parse(typeof(TestMode), (string)values["mode"]),
                Status = (TestJobStatus)Enum.Parse(typeof(TestJobStatus), (string)values["status"]),
                StartedAt = ParseDate((string)values["startedAt"]),
                CompletedAt = ParseOptionalDate(values["completedAt"]),
                LastUpdatedAt = ParseDate((string)values["lastUpdatedAt"]),
                TotalTests = Convert.ToInt32(values["totalTests"]),
                CompletedTests = Convert.ToInt32(values["completedTests"]),
                PassedCount = Convert.ToInt32(values["passedCount"]),
                FailedCount = Convert.ToInt32(values["failedCount"]),
                SkippedCount = Convert.ToInt32(values["skippedCount"]),
                TotalDuration = Convert.ToDouble(values["totalDuration"]),
                CompletionRecovered = (bool)values["completionRecovered"],
                HasExplicitFilters = (bool)values["hasExplicitFilters"],
                TestNames = ReadStrings(values["testNames"]), Categories = ReadStrings(values["categories"]),
                Assemblies = ReadStrings(values["assemblies"]),
                CurrentTestName = (string)values["currentTestName"],
                CurrentTestStartedAt = ParseOptionalDate(values["currentTestStartedAt"]),
                Error = (string)values["error"], ErrorCode = (string)values["errorCode"]
            };
            for (int index = 0; index < job.CompletedTests; index++)
            {
                Dictionary<string, object> result = ReadRecord(ResultKey(jobId, index));
                job.AllResults.Add(new TestResult((string)result["fullName"], (string)result["name"],
                    (string)result["status"], Convert.ToDouble(result["duration"]),
                    (string)result["message"], (string)result["stackTrace"]));
            }
            job.FailuresSoFar = job.AllResults.Where(result => result.IsFailure)
                .Take(VmAutomationTestRunnerCommands.MaxFailuresTracked).ToList();
            return job;
        }

        internal void RetireJob(string jobId, int resultCount)
        {
            SessionState.EraseString(JobKey(jobId));
            for (int index = 0; index < resultCount; index++)
                SessionState.EraseString(ResultKey(jobId, index));
        }

        internal void RetireIndex()
        {
            SessionState.EraseString(prefix + "/jobs");
            SessionState.EraseString(prefix + "/current");
        }

        private string JobKey(string jobId) => prefix + "/job/" + jobId;
        private string ResultKey(string jobId, int index) => JobKey(jobId) + "/result/" + index;

        private static Dictionary<string, object> ReadRecord(string key)
        {
            string json = SessionState.GetString(key, "");
            if (json.Length == 0)
                throw new InvalidOperationException("Test Runner session record is missing: " + key);
            return (Dictionary<string, object>)MiniJson.Deserialize(json);
        }

        private static DateTime ParseDate(string value) =>
            DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

        private static DateTime? ParseOptionalDate(object value) =>
            value == null ? (DateTime?)null : ParseDate((string)value);

        private static string[] ReadStrings(object value) =>
            value == null ? null : ((List<object>)value).Select(item => (string)item).ToArray();
    }
}
