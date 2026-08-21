using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace VMUnityAutomation.Editor
{
    internal sealed class VmAutomationWorkspaceJob
    {
        internal string JobId { get; set; }
        internal string JobAccessToken { get; set; }
        internal string JobType { get; set; }
        internal string Operation { get; set; }
        internal string OwnerAgentId { get; set; }
        internal string IdempotencyKey { get; set; }
        internal string RequestId { get; set; }
        internal string RequestFingerprint { get; set; }
        internal string Status { get; set; }
        internal string Phase { get; set; }
        internal string StatusMessage { get; set; }
        internal Dictionary<string, object> Request { get; set; } = new();
        internal Dictionary<string, object> Result { get; set; }
        internal Dictionary<string, object> Error { get; set; }
        internal DateTime CreatedAt { get; set; }
        internal DateTime UpdatedAt { get; set; }
        internal DateTime? StartedAt { get; set; }
        internal DateTime? CompletedAt { get; set; }
        internal bool ClientAdopted { get; set; }
        internal int AssetRefreshInvocationCount { get; set; }
        internal bool AssetRefreshReturned { get; set; }
        internal bool AssetRefreshDomainReloadObserved { get; set; }
        internal DateTime? AssetRefreshReturnedAt { get; set; }
        internal Dictionary<string, object> AssetRefreshResult { get; set; }
        internal bool CompilationRequested { get; set; }
        internal bool CompilationStarted { get; set; }
        internal bool CompilationFinished { get; set; }
        internal bool? CompilationSucceeded { get; set; }
        internal bool AssemblyReloadObserved { get; set; }
        internal DateTime? CompilationRequestedAt { get; set; }
        internal DateTime? CompilationStartedAt { get; set; }
        internal DateTime? CompilationFinishedAt { get; set; }
        internal int CompilerErrorCount { get; set; }
        internal int CompilerWarningCount { get; set; }
        internal List<Dictionary<string, object>> CompilerMessages { get; set; } = new();
        internal bool PackageRequestIssued { get; set; }
        internal bool PackageRequestCompleted { get; set; }
        internal int PackageRequestAttemptCount { get; set; }
        internal List<Dictionary<string, object>> PackageRequestFailures { get; set; } = new();
        internal bool PackageResolveInvoked { get; set; }
        internal bool PackageUpdatingObserved { get; set; }
        internal bool PackageRegistrationObserved { get; set; }
        internal DateTime? PackageRequestIssuedAt { get; set; }
        internal DateTime? PackageRequestCompletedAt { get; set; }
        internal string PackageName { get; set; }
        internal string RequestedPackageIdentifier { get; set; }
        internal string RequestedPackageRevision { get; set; }
        internal List<VmAutomationGitPackageExpectation> ExpectedPackages { get; set; } = new();
        internal Dictionary<string, object> PackageState { get; set; }
        internal Dictionary<string, object> TransactionState { get; set; }
        internal bool RecoveredAfterReload { get; set; }
        internal int DomainReloadCount { get; set; }

        internal bool IsTerminal =>
            Status == "succeeded" || Status == "failed" || Status == "canceled";

        internal bool HasRetainedRecoveryArtifacts
        {
            get
            {
                if (TransactionState == null ||
                    !TransactionState.TryGetValue("recoveryArtifactsRetained", out object value) ||
                    value == null)
                    return false;
                if (value is bool retained)
                    return retained;
                return bool.TryParse(value.ToString(), out bool parsed) && parsed;
            }
        }

        internal Dictionary<string, object> ToDictionary()
        {
            return new Dictionary<string, object>
            {
                { "jobId", JobId },
                { "jobAccessToken", JobAccessToken },
                { "jobType", JobType },
                { "operation", Operation },
                { "ownerAgentId", OwnerAgentId },
                { "idempotencyKey", IdempotencyKey ?? "" },
                { "requestId", RequestId ?? "" },
                { "requestFingerprint", RequestFingerprint ?? "" },
                { "status", Status },
                { "phase", Phase },
                { "statusMessage", StatusMessage ?? "" },
                { "request", Request },
                { "result", Result },
                { "error", Error },
                { "createdAt", CreatedAt.ToString("O") },
                { "updatedAt", UpdatedAt.ToString("O") },
                { "startedAt", FormatDate(StartedAt) },
                { "completedAt", FormatDate(CompletedAt) },
                { "clientAdopted", ClientAdopted },
                { "assetRefreshInvocationCount", AssetRefreshInvocationCount },
                { "assetRefreshReturned", AssetRefreshReturned },
                { "assetRefreshDomainReloadObserved", AssetRefreshDomainReloadObserved },
                { "assetRefreshReturnedAt", FormatDate(AssetRefreshReturnedAt) },
                { "assetRefreshResult", AssetRefreshResult },
                { "compilationRequested", CompilationRequested },
                { "compilationStarted", CompilationStarted },
                { "compilationFinished", CompilationFinished },
                { "compilationSucceeded", CompilationSucceeded },
                { "assemblyReloadObserved", AssemblyReloadObserved },
                { "compilationRequestedAt", FormatDate(CompilationRequestedAt) },
                { "compilationStartedAt", FormatDate(CompilationStartedAt) },
                { "compilationFinishedAt", FormatDate(CompilationFinishedAt) },
                { "compilerErrorCount", CompilerErrorCount },
                { "compilerWarningCount", CompilerWarningCount },
                { "compilerMessages", CompilerMessages.Cast<object>().ToList() },
                { "packageRequestIssued", PackageRequestIssued },
                { "packageRequestCompleted", PackageRequestCompleted },
                { "packageRequestAttemptCount", PackageRequestAttemptCount },
                { "packageRequestFailures", PackageRequestFailures.Cast<object>().ToList() },
                { "packageResolveInvoked", PackageResolveInvoked },
                { "packageUpdatingObserved", PackageUpdatingObserved },
                { "packageRegistrationObserved", PackageRegistrationObserved },
                { "packageRequestIssuedAt", FormatDate(PackageRequestIssuedAt) },
                { "packageRequestCompletedAt", FormatDate(PackageRequestCompletedAt) },
                { "packageName", PackageName ?? "" },
                { "requestedPackageIdentifier", RequestedPackageIdentifier ?? "" },
                { "requestedPackageRevision", RequestedPackageRevision ?? "" },
                { "expectedPackages", ExpectedPackages.Select(item =>
                    (object)item.ToDictionary()).ToList() },
                { "packageState", PackageState },
                { "transactionState", TransactionState },
                { "recoveredAfterReload", RecoveredAfterReload },
                { "domainReloadCount", DomainReloadCount },
            };
        }

        internal static VmAutomationWorkspaceJob FromDictionary(Dictionary<string, object> values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));

            var job = new VmAutomationWorkspaceJob
            {
                JobId = GetRequiredString(values, "jobId"),
                JobAccessToken = GetRequiredString(values, "jobAccessToken"),
                JobType = GetRequiredString(values, "jobType"),
                Operation = GetRequiredString(values, "operation"),
                OwnerAgentId = GetRequiredString(values, "ownerAgentId"),
                IdempotencyKey = GetString(values, "idempotencyKey"),
                RequestId = GetString(values, "requestId"),
                RequestFingerprint = GetString(values, "requestFingerprint"),
                Status = GetRequiredString(values, "status"),
                Phase = GetRequiredString(values, "phase"),
                StatusMessage = GetString(values, "statusMessage"),
                Request = GetDictionary(values, "request") ?? new Dictionary<string, object>(),
                Result = GetDictionary(values, "result"),
                Error = GetDictionary(values, "error"),
                CreatedAt = GetRequiredDate(values, "createdAt"),
                UpdatedAt = GetRequiredDate(values, "updatedAt"),
                StartedAt = GetNullableDate(values, "startedAt"),
                CompletedAt = GetNullableDate(values, "completedAt"),
                // Jobs written before the adoption barrier already followed the old
                // eager-execution contract. Preserve their ability to recover instead
                // of stranding an in-flight job after upgrading the package.
                ClientAdopted = !values.ContainsKey("clientAdopted") ||
                                GetBool(values, "clientAdopted"),
                AssetRefreshInvocationCount = GetInt(values, "assetRefreshInvocationCount"),
                AssetRefreshReturned = GetBool(values, "assetRefreshReturned"),
                AssetRefreshDomainReloadObserved =
                    GetBool(values, "assetRefreshDomainReloadObserved"),
                AssetRefreshReturnedAt = GetNullableDate(values, "assetRefreshReturnedAt"),
                AssetRefreshResult = GetDictionary(values, "assetRefreshResult"),
                CompilationRequested = GetBool(values, "compilationRequested"),
                CompilationStarted = GetBool(values, "compilationStarted"),
                CompilationFinished = GetBool(values, "compilationFinished"),
                CompilationSucceeded = GetNullableBool(values, "compilationSucceeded"),
                AssemblyReloadObserved = GetBool(values, "assemblyReloadObserved"),
                CompilationRequestedAt = GetNullableDate(values, "compilationRequestedAt"),
                CompilationStartedAt = GetNullableDate(values, "compilationStartedAt"),
                CompilationFinishedAt = GetNullableDate(values, "compilationFinishedAt"),
                CompilerErrorCount = GetInt(values, "compilerErrorCount"),
                CompilerWarningCount = GetInt(values, "compilerWarningCount"),
                CompilerMessages = GetDictionaryList(values, "compilerMessages"),
                PackageRequestIssued = GetBool(values, "packageRequestIssued"),
                PackageRequestCompleted = GetBool(values, "packageRequestCompleted"),
                PackageRequestAttemptCount = GetInt(values, "packageRequestAttemptCount"),
                PackageRequestFailures = GetDictionaryList(values, "packageRequestFailures"),
                PackageResolveInvoked = GetBool(values, "packageResolveInvoked"),
                PackageUpdatingObserved = GetBool(values, "packageUpdatingObserved"),
                PackageRegistrationObserved = GetBool(values, "packageRegistrationObserved"),
                PackageRequestIssuedAt = GetNullableDate(values, "packageRequestIssuedAt"),
                PackageRequestCompletedAt = GetNullableDate(values, "packageRequestCompletedAt"),
                PackageName = GetString(values, "packageName"),
                RequestedPackageIdentifier = GetString(values, "requestedPackageIdentifier"),
                RequestedPackageRevision = GetString(values, "requestedPackageRevision"),
                PackageState = GetDictionary(values, "packageState"),
                TransactionState = GetDictionary(values, "transactionState"),
                RecoveredAfterReload = GetBool(values, "recoveredAfterReload"),
                DomainReloadCount = GetInt(values, "domainReloadCount"),
            };

            job.ExpectedPackages = GetDictionaryList(values, "expectedPackages")
                .Select(VmAutomationGitPackageExpectation.FromDictionary).ToList();
            return job;
        }

        private static string FormatDate(DateTime? value)
        {
            return value.HasValue ? value.Value.ToString("O") : "";
        }

        private static string GetRequiredString(Dictionary<string, object> values, string key)
        {
            string result = GetString(values, key);
            if (string.IsNullOrWhiteSpace(result))
                throw new InvalidDataException($"Persisted workspace job is missing '{key}'.");
            return result;
        }

        private static string GetString(Dictionary<string, object> values, string key)
        {
            return values.TryGetValue(key, out object value) && value != null
                ? value.ToString()
                : "";
        }

        private static int GetInt(Dictionary<string, object> values, string key)
        {
            return values.TryGetValue(key, out object value) && value != null &&
                   int.TryParse(value.ToString(), NumberStyles.Integer,
                       CultureInfo.InvariantCulture, out int result)
                ? result
                : 0;
        }

        private static bool GetBool(Dictionary<string, object> values, string key)
        {
            if (!values.TryGetValue(key, out object value) || value == null)
                return false;
            return value is bool result
                ? result
                : bool.TryParse(value.ToString(), out result) && result;
        }

        private static bool? GetNullableBool(Dictionary<string, object> values, string key)
        {
            if (!values.TryGetValue(key, out object value) || value == null)
                return null;
            if (value is bool result)
                return result;
            return bool.TryParse(value.ToString(), out result)
                ? result
                : (bool?)null;
        }

        private static DateTime GetRequiredDate(Dictionary<string, object> values, string key)
        {
            DateTime? result = GetNullableDate(values, key);
            if (!result.HasValue)
                throw new InvalidDataException($"Persisted workspace job has invalid '{key}'.");
            return result.Value;
        }

        private static DateTime? GetNullableDate(Dictionary<string, object> values, string key)
        {
            return DateTime.TryParse(GetString(values, key), CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out DateTime result)
                ? result
                : (DateTime?)null;
        }

        private static Dictionary<string, object> GetDictionary(
            Dictionary<string, object> values, string key)
        {
            return values.TryGetValue(key, out object value)
                ? VmAutomationResponse.ToDictionary(value)
                : null;
        }

        private static List<Dictionary<string, object>> GetDictionaryList(
            Dictionary<string, object> values, string key)
        {
            if (!values.TryGetValue(key, out object value) || !(value is IList list))
                return new List<Dictionary<string, object>>();
            return list.Cast<object>().Select(VmAutomationResponse.ToDictionary)
                .Where(item => item != null).ToList();
        }
    }
}
