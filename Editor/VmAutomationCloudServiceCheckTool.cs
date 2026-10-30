using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;

namespace VMUnityAutomation.Editor
{
    [VmProjectTool("unity/cloud-service-check", LongRunning = true,
        Description = "Check Unity's native service-token API once without publishing credentials. Uses Unity's own cache; availability does not prove a fresh network exchange or repair a historical failure. Returns bounded HTTP metadata on failure and waits for actual request retirement.",
        InputSchemaJson = "{\"type\":\"object\",\"additionalProperties\":false,\"properties\":{}}",
        OutputSchemaJson = ResultSchema,
        CleanupToolName = "unity/cloud-service-check-cleanup",
        SideEffects = VmProjectToolSideEffect.ReadsProjectState | VmProjectToolSideEffect.PerformsExternalIO | VmProjectToolSideEffect.ChangesRuntimeState,
        MutatesRuntime = true,
        ErrorCodes = new[] { "capability_unavailable", "cloud_check_identity_mismatch", "cloud_check_pending", "cloud_check_active" },
        Preconditions = new[] { "Stable Edit Mode; Unity 6 or newer." },
        CompletionEvidence = "Native API outcome and the admitted actual request-retirement task; never contains a token.")]
    public sealed class VmAutomationCloudServiceCheckTool : IVmProjectToolAsyncCleanup
    {
        internal const string FailureSchema = "{\"type\":\"object\",\"additionalProperties\":false,\"properties\":{\"exceptionType\":{\"type\":\"string\"},\"responseCode\":{\"type\":[\"integer\",\"null\"]},\"isHttpError\":{\"type\":[\"boolean\",\"null\"]},\"isNetworkError\":{\"type\":[\"boolean\",\"null\"]},\"method\":{\"type\":[\"string\",\"null\"]},\"target\":{\"type\":[\"string\",\"null\"]},\"timeout\":{\"type\":[\"integer\",\"null\"]}},\"required\":[\"exceptionType\",\"responseCode\",\"isHttpError\",\"isNetworkError\",\"method\",\"target\",\"timeout\"]}";
        internal const string ResultSchema = "{\"type\":\"object\",\"additionalProperties\":false,\"properties\":{\"protocol\":{\"type\":\"string\"},\"checkedAt\":{\"type\":\"string\"},\"outcome\":{\"type\":\"string\",\"enum\":[\"available\",\"empty\",\"failed\",\"cancelled\"]},\"tokenAvailable\":{\"type\":\"boolean\"},\"cacheSemantics\":{\"type\":\"string\"},\"failure\":{\"oneOf\":[{\"type\":\"null\"}," + FailureSchema + "]}},\"required\":[\"protocol\",\"checkedAt\",\"outcome\",\"tokenAvailable\",\"cacheSemantics\",\"failure\"]}";
        private static CloudServiceCheckWork active;

        static VmAutomationCloudServiceCheckTool()
        {
            AssemblyReloadEvents.beforeAssemblyReload += CancelActive;
            EditorApplication.quitting += CancelActive;
        }

        public object Execute(Dictionary<string, object> args)
            => throw new InvalidOperationException("Use the durable cloud-service-check Job.");

        public VmProjectToolJobStep ExecuteJobStep(Dictionary<string, object> args, Dictionary<string, object> state)
        {
#if UNITY_6000_0_OR_NEWER
            string job = VmProjectToolExecutionContext.JobId;
            if (state.Count == 0)
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                    throw new InvalidOperationException("Cloud service checks require stable Edit Mode.");
                if (active != null && !active.Retirement.IsCompleted)
                    throw new VmProjectToolException("cloud_check_active", "The previous native cloud request still owns resources.");
                active = new CloudServiceCheckWork(job, Guid.NewGuid().ToString("N"));
                VmProjectToolExecutionContext.PublishCleanupToken(active.Token, active.Retirement);
                active.Start(CloudProjectSettings.GetServiceTokenAsync);
                state = new Dictionary<string, object> { ["token"] = active.Token, ["polls"] = 0 };
            }
            RequireIdentity(job, (string)state["token"]);
            if (active.Source.IsCompleted)
                return VmProjectToolJobStep.Complete(active.Source.GetAwaiter().GetResult(), active.Token);
            int polls = Convert.ToInt32(state["polls"]) + 1;
            if (polls > 32)
                throw new VmProjectToolException("cloud_check_pending", "Native request retirement is still pending; no replacement request was started.");
            state["polls"] = polls;
            return VmProjectToolJobStep.Pending(state, statusMessage: "Awaiting the one native Unity service-token request.",
                delayMilliseconds: 500, cleanupToken: active.Token);
#else
            throw new VmProjectToolException("capability_unavailable", "Native cancellable service-token checks require Unity 6 or newer.");
#endif
        }

        public Task<VmProjectToolCleanupReceipt> CleanupJobAsync(string actualJobID, string actualCleanupToken)
        {
            RequireIdentity(actualJobID, actualCleanupToken);
            active.Cancel();
            return active.Retirement;
        }

        [VmProjectTool("unity/cloud-service-check-cleanup", Description = "Observe retirement of the exact native cloud check; does not create a request.",
            InputSchemaJson = "{\"type\":\"object\",\"additionalProperties\":false,\"properties\":{\"action\":{\"type\":\"string\",\"enum\":[\"cleanup\"]},\"jobId\":{\"type\":\"string\"},\"cleanupToken\":{\"type\":\"string\"}},\"required\":[\"action\",\"jobId\",\"cleanupToken\"]}",
            OutputSchemaJson = "{\"type\":\"object\",\"additionalProperties\":false,\"properties\":{\"resourcesReleased\":{\"type\":\"boolean\"}},\"required\":[\"resourcesReleased\"]}",
            ReadOnly = true, SideEffects = VmProjectToolSideEffect.ReadsProjectState,
            ErrorCodes = new[] { "cloud_check_identity_mismatch" })]
        public static object ObserveCleanup(Dictionary<string, object> args)
        {
            RequireIdentity((string)args["jobId"], (string)args["cleanupToken"]);
            return new Dictionary<string, object> { ["resourcesReleased"] = active.Retirement.IsCompleted };
        }

        private static void RequireIdentity(string job, string token)
        {
            if (active == null || active.JobID != job || active.Token != token)
                throw new VmProjectToolException("cloud_check_identity_mismatch", "The native cloud-check resource lifetime differs.");
        }

        private static void CancelActive() => active?.Cancel();
    }
}
