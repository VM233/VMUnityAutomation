using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.ExceptionServices;

namespace VMUnityAutomation.Editor
{
    public sealed class VmProjectToolCleanupReceipt
    {
        internal const string Contract = "async-cleanup-receipt-v1";
        public string JobID { get; }
        public string CleanupToken { get; }
        public bool ResourcesReleased { get; }
        public object Result { get; }
        public object Failure { get; }
        internal Exception OriginalFailure { get; }

        public VmProjectToolCleanupReceipt(string jobID, string cleanupToken, bool resourcesReleased,
            object result, Exception failure = null)
        {
            if (string.IsNullOrWhiteSpace(jobID) || string.IsNullOrWhiteSpace(cleanupToken))
                throw new ArgumentException("An actual cleanup receipt requires its admitted job and token identity.");
            JobID = jobID;
            CleanupToken = cleanupToken;
            ResourcesReleased = resourcesReleased;
            Result = Freeze(VmJsonContract.ToTransportValue(result));
            OriginalFailure = failure;
            Failure = failure == null ? null : Freeze(FailureResponse(failure));
        }

        public void ThrowIfFailed()
        {
            if (OriginalFailure != null) ExceptionDispatchInfo.Capture(OriginalFailure).Throw();
        }

        internal Dictionary<string, object> ToTransport() => new()
        {
            ["JobID"] = JobID, ["CleanupToken"] = CleanupToken,
            ["ResourcesReleased"] = ResourcesReleased,
            ["Result"] = VmJsonContract.ToTransportValue(Result),
            ["Failure"] = VmJsonContract.ToTransportValue(Failure)
        };

        internal static Dictionary<string, object> FailureResponse(Exception failure)
        {
            if (failure is AggregateException aggregate)
            {
                var failures = new List<object>(aggregate.InnerExceptions.Count);
                foreach (var actual in aggregate.InnerExceptions) failures.Add(FailureResponse(actual));
                return VmAutomationResponse.Error(aggregate.Message, "job_cleanup_failed", false,
                    new Dictionary<string, object> { ["failures"] = failures });
            }
            if (failure is VmProjectToolException domain)
                return VmAutomationResponse.Error(domain.Message, domain.ErrorCode, domain.Retryable,
                    domain.Details == null ? null : new Dictionary<string, object>
                    { ["details"] = VmJsonContract.ToTransportValue(domain.Details) });
            return VmAutomationResponse.Error($"{failure.GetType().FullName}: {failure.Message}",
                "job_cleanup_failed");
        }

        private static object Freeze(object value)
        {
            if (value is IDictionary dictionary)
            {
                var product = new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (DictionaryEntry entry in dictionary) product.Add((string)entry.Key, Freeze(entry.Value));
                return new ReadOnlyDictionary<string, object>(product);
            }
            if (value is IEnumerable values && value is not string)
            {
                var product = new List<object>();
                foreach (object item in values) product.Add(Freeze(item));
                return product.AsReadOnly();
            }
            return value;
        }
    }
}
