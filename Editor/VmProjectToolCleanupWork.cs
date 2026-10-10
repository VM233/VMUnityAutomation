using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace VMUnityAutomation.Editor
{
    internal sealed class VmProjectToolCleanupWork
    {
        internal Dictionary<string, object> Job { get; }
        internal string JobID { get; }
        internal string Token { get; }
        internal Task<VmProjectToolCleanupReceipt> ActualTask { get; }
        internal string TerminalStatus { get; private set; }
        internal object TerminalResult { get; private set; }
        internal object TerminalError { get; private set; }
        internal Exception RequestFailure { get; private set; }

        internal VmProjectToolCleanupWork(Dictionary<string, object> job, string jobID, string token,
            Task<VmProjectToolCleanupReceipt> actualTask)
        {
            Job = job;
            JobID = jobID;
            Token = token;
            ActualTask = actualTask ?? throw new ArgumentNullException(nameof(actualTask));
        }

        internal void PublishTerminalIntent(string status, object result, object error)
        {
            if (TerminalStatus != null) throw new InvalidOperationException("This accepted job already published its source terminal product.");
            TerminalStatus = status;
            TerminalResult = result;
            TerminalError = error;
        }

        internal void RequireSameTask(Task<VmProjectToolCleanupReceipt> task)
        {
            if (!ReferenceEquals(task, ActualTask))
                throw new VmProjectToolException("project_tool_cleanup_task_mismatch",
                    $"Job '{JobID}' cleanup did not return its admitted actual scope task.");
        }

        internal void RetainRequestFailure(Exception failure) => RequestFailure = RequestFailure == null ? failure :
            new AggregateException("Actual cleanup requests failed.", RequestFailure, failure);

        internal VmProjectToolCleanupReceipt ReadTerminalReceipt()
        {
            if (!ActualTask.IsCompleted) throw new InvalidOperationException("The admitted cleanup source is still pending.");
            return ActualTask.GetAwaiter().GetResult();
        }

        internal void RequireReceiptIdentity(VmProjectToolCleanupReceipt receipt)
        {
            if (receipt == null || receipt.JobID != JobID || receipt.CleanupToken != Token)
                throw new VmProjectToolException("project_tool_cleanup_identity_mismatch",
                    $"Job '{JobID}' received a terminal cleanup product for another owner.", details:
                    new Dictionary<string, object>
                    {
                        ["jobID"] = JobID, ["cleanupToken"] = Token,
                        ["receiptJobID"] = receipt?.JobID, ["receiptCleanupToken"] = receipt?.CleanupToken,
                        ["receiptResourcesReleased"] = receipt?.ResourcesReleased
                    });
        }
    }
}
