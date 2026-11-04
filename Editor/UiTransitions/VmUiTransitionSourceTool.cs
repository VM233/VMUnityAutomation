using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    [VmProjectTool("uitoolkit/transition-source-review", LongRunning = true,
        CleanupToolName = "uitoolkit/transition-source-review-cleanup", MutatesRuntime = true,
        SideEffects = VmProjectToolSideEffect.ReadsProjectState | VmProjectToolSideEffect.CreatesTemporaryObjects,
        Description = "Build a bounded Roslyn inventory of actual UI visibility/content/native navigation sites and resolved calls in their actual native Unity compilations. Sibling context sources do not widen declared UI graph coverage. DeclaredFixture is an explicit standalone test mode, never a native fallback. Affected-path review includes adjacent callers and cannot set full Passed. This is source coverage, not runtime or visual acceptance. One cancellable worker admits1024 selected files/16MiB/250000 nodes/12000 edges, with at most8 native contexts/4096 context files/64MiB/2000000 context nodes and bounded native metadata images.",
        ErrorCodes = new[] { "persistent_job_required", "ui_transition_source_arguments", "ui_transition_source_limit", "ui_transition_source_capability", "ui_transition_source_owner" },
        CompletionEvidence = "Untruncated source digest, exact roots/exclusions/selected paths, resolved producer/caller edges, every discovered mutation site and registration gaps. RuntimeAccepted is false; project host linkage and actual frame/input evidence are independent required layers.")]
    public sealed class VmUiTransitionSourceTool : IVmProjectTool<VmUiTransitionSourceRequest, VmUiTransitionSourceReport>, IVmProjectToolAsyncCleanup
    {
        public VmUiTransitionSourceReport Execute(VmUiTransitionSourceRequest request) =>
            throw new VmProjectToolException("persistent_job_required", "UI source analysis requires its durable worker scope.");
        object IVmProjectTool.Execute(Dictionary<string, object> args) => Execute((VmUiTransitionSourceRequest)VmJsonContract.Bind(args, typeof(VmUiTransitionSourceRequest)));

        public VmProjectToolJobStep ExecuteJobStep(Dictionary<string, object> args, Dictionary<string, object> state)
        {
            VmUiTransitionSourceScope scope;
            if (state.Count == 0)
            {
                scope = ScriptableObject.CreateInstance<VmUiTransitionSourceScope>(); scope.hideFlags = HideFlags.HideAndDontSave;
                try { scope.Begin((VmUiTransitionSourceRequest)VmJsonContract.Bind(args, typeof(VmUiTransitionSourceRequest))); }
                catch { UnityEngine.Object.DestroyImmediate(scope); throw; }
            }
            else scope = Require((string)state["scope"]);
            string identity = VmObjectId.Get(scope);
            scope.ObserveCancellation();
            if (!scope.Completed) return VmProjectToolJobStep.Pending(new Dictionary<string, object> { ["scope"] = identity },
                statusMessage: "Analyzing actual UI source and semantic call relationships.", cleanupToken: identity);
            try { return VmProjectToolJobStep.Complete(VmJsonContract.ToTransportValue(scope.Result), identity); }
            finally { scope.CloseCompleted(); }
        }

        public Task<VmProjectToolCleanupReceipt> CleanupJobAsync(string job, string token) => Require(token).RetireAsync(job);

        private static VmUiTransitionSourceScope Require(string identity) => VmObjectId.ToObject(identity) as VmUiTransitionSourceScope ??
            throw new VmProjectToolException("ui_transition_source_owner", "The actual UI source worker scope no longer exists.");

        [VmProjectTool("uitoolkit/transition-source-review-cleanup", MutatesRuntime = true,
            SideEffects = VmProjectToolSideEffect.ChangesRuntimeState,
            Description = "Cancel the exact UI source worker. Canonical asynchronous job cleanup joins the worker and publishes actual retirement.")]
        public static VmUiTransitionSourceCleanupResult Cleanup(VmUiTransitionSourceCleanupRequest request)
        {
            if (request.Action != "cleanup") throw new ArgumentException("The UI source cleanup action must be cleanup.");
            var scope = Require(request.CleanupToken);
            _ = scope.RequestRetirement();
            return scope.ObserveRetirement();
        }
    }
}
