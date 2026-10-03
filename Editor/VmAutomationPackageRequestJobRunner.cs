using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationPackageRequestJobRunner
    {
        internal const string JobType = "package-request";
        internal const string AwaitingRequestPhase = "awaiting-package-request";

        internal static object Start(string operation, Dictionary<string, object> arguments)
        {
            string inputName = operation == "packages/add" ? "identifier" :
                operation == "packages/remove" ? "name" : "query";
            if (!arguments.TryGetValue(inputName, out object value) ||
                string.IsNullOrWhiteSpace(value?.ToString()))
                return VmAutomationResponse.Error(inputName + " is required.", "invalid_arguments");
            if (operation == "packages/search")
            {
                if (!arguments.ContainsKey("offset")) arguments.Add("offset", 0);
                if (!arguments.ContainsKey("limit")) arguments.Add("limit", 50);
            }
            return VmAutomationWorkspaceJobRunner.StartPackageRequest(operation, arguments);
        }

        internal static void Issue(VmAutomationWorkspaceJob job)
        {
            VmAutomationPackageRequestState state = VmAutomationPackageRequestState.instance;
            if (job.PackageRequestIssued)
                throw new InvalidOperationException(
                    $"Package request '{job.JobId}' attempted overlapping or repeated issuance.");
            job.PackageRequestIssued = true;
            job.PackageRequestIssuedAt = DateTime.UtcNow;
            job.Phase = AwaitingRequestPhase;
            job.StatusMessage = "The native Package Manager request was issued once.";
            VmAutomationWorkspaceJobRunner.Persist(job);
            state.Issue(job);
        }

        internal static void Observe(VmAutomationWorkspaceJob job)
        {
            Request activeRequest = VmAutomationPackageRequestState.instance.GetRequest(job);
            if (!activeRequest.IsCompleted)
                return;
            if (activeRequest.Status == StatusCode.Failure)
            {
                Error nativeError = activeRequest.Error;
                VmAutomationWorkspaceJobRunner.Fail(job, VmAutomationResponse.Error(
                    nativeError.message, FailureCode(job.Operation), false,
                    new Dictionary<string, object>
                    {
                        { "nativeErrorCode", nativeError.errorCode.ToString() },
                        { "requestIssuedAt", job.PackageRequestIssuedAt.Value.ToString("O") },
                    }));
                return;
            }

            Dictionary<string, object> completion;
            if (activeRequest is SearchRequest search)
            {
                completion = BuildSearchResult(search.Result, job.Request);
                job.PackageRequestCompleted = true;
                job.PackageRequestCompletedAt = DateTime.UtcNow;
                job.Result = completion;
                job.Status = "succeeded";
                job.Phase = "succeeded";
                job.StatusMessage = "The native registry query completed.";
                job.CompletedAt = DateTime.UtcNow;
                VmAutomationWorkspaceJobRunner.Persist(job);
                Retire(job);
                return;
            }

            if (activeRequest is AddRequest add)
            {
                PackageInfo package = add.Result;
                job.PackageName = package.name;
                completion = BuildAddCompletion(package);
            }
            else
            {
                job.PackageName = (string)job.Request["name"];
                completion = new Dictionary<string, object> { { "removed", job.PackageName } };
            }
            job.PackageRequestCompleted = true;
            job.PackageRequestCompletedAt = DateTime.UtcNow;
            job.TransactionState = new Dictionary<string, object>
            {
                { "nativeCompletion", completion },
                { "declaration", VmAutomationPackageManagerCommands.CapturePackageDeclarationState(job.PackageName) },
            };
            job.Phase = VmAutomationWorkspaceJobRunner.RefreshingAssetsPhase;
            job.StatusMessage = "Native package completion persisted; refreshing assets before compilation.";
            VmAutomationWorkspaceJobRunner.Persist(job);
            Retire(job);
        }

        internal static Dictionary<string, object> BuildAddCompletion(PackageInfo package)
        {
            return new Dictionary<string, object>
            {
                { "name", package.name }, { "displayName", package.displayName },
                { "version", package.version }, { "packageId", package.packageId },
                { "source", package.source.ToString() },
            };
        }

        internal static Dictionary<string, object> BuildSearchResult(PackageInfo[] packages,
            Dictionary<string, object> request)
        {
            int offset = Convert.ToInt32(request["offset"], CultureInfo.InvariantCulture);
            int limit = Convert.ToInt32(request["limit"], CultureInfo.InvariantCulture);
            var page = new List<object>();
            int count = offset >= packages.Length ? 0 : Math.Min(limit, packages.Length - offset);
            for (int index = 0; index < count; index++)
            {
                PackageInfo package = packages[offset + index];
                page.Add(new Dictionary<string, object>
                {
                    { "name", package.name }, { "displayName", package.displayName },
                    { "version", package.version }, { "description", package.description ?? "" },
                });
            }
            bool hasMore = (long)offset + count < packages.Length;
            return new Dictionary<string, object>
            {
                { "query", request["query"] }, { "total", packages.Length },
                { "offset", offset }, { "limit", limit }, { "hasMore", hasMore },
                { "nextOffset", hasMore ? (object)(offset + count) : null }, { "results", page },
            };
        }

        internal static bool MatchesNativeCompletion(string operation,
            Dictionary<string, object> completion, Dictionary<string, object> state)
        {
            if (operation == "packages/remove")
                return (string)state["manifestDependency"] == "";
            return (string)state["manifestDependency"] != "" &&
                   (string)state["lockVersion"] != "" &&
                   Equals(state["resolvedVersion"], completion["version"]) &&
                   Equals(state["resolvedPackageId"], completion["packageId"]) &&
                   Equals(state["resolvedSource"], completion["source"]);
        }

        internal static bool VerifyMutation(VmAutomationWorkspaceJob job)
        {
            Dictionary<string, object> completion =
                (Dictionary<string, object>)job.TransactionState["nativeCompletion"];
            Dictionary<string, object> actual =
                VmAutomationPackageManagerCommands.CapturePackageResolutionState(job.PackageName);
            job.PackageState = actual;
            return MatchesDeclaration((Dictionary<string, object>)job.TransactionState["declaration"], actual) &&
                MatchesNativeCompletion(job.Operation, completion, actual);
        }

        internal static bool MatchesDeclaration(Dictionary<string, object> expected, Dictionary<string, object> actual)
        {
            foreach (KeyValuePair<string, object> field in expected)
                if (!actual.TryGetValue(field.Key, out object value) || !Equals(value, field.Value)) return false;
            return true;
        }

        internal static Dictionary<string, object> RecoverInterruptedRequest(VmAutomationWorkspaceJob job)
        {
            return VmAutomationResponse.Error(
                "The Editor domain reloaded before native package completion was persisted. " +
                "Its original native operation is unavailable after the process restart. " +
                "The operation will not be reissued and its outcome is uncertain.",
                "package_request_outcome_uncertain_after_reload", false,
                new Dictionary<string, object>
                {
                    { "jobId", job.JobId }, { "operation", job.Operation },
                    { "request", job.Request },
                    { "requestIssuedAt", job.PackageRequestIssuedAt.Value.ToString("O") },
                });
        }

        internal static void Retire(VmAutomationWorkspaceJob job)
        {
            VmAutomationPackageRequestState.instance.Retire(job);
        }

        internal static bool OwnsOriginalRequest(VmAutomationWorkspaceJob job)
        {
            return VmAutomationPackageRequestState.instance.JobId == job.JobId;
        }

        private static string FailureCode(string operation)
        {
            return operation == "packages/add" ? "package_add_failed" :
                operation == "packages/remove" ? "package_remove_failed" : "package_search_failed";
        }
    }
}
