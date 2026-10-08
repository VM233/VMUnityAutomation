using System.Diagnostics;
using UnityEditor;

namespace VMUnityAutomation.Editor
{
    [VmProjectTool("asset/import-worker-count",
        Description = "Set the current Editor session's native desired import worker count and request immediate worker reconciliation. Unity starts missing workers or shuts down idle surplus workers. Return native desired-count readback and call duration; OS process retirement requires separate evidence. Does not persist settings, import assets or refresh scripts.",
        MutatesRuntime = true,
        SideEffects = VmProjectToolSideEffect.ReadsProjectState | VmProjectToolSideEffect.ChangesRuntimeState |
            VmProjectToolSideEffect.PerformsExternalIO,
        ErrorCodes = new[] { "import_worker_count_invalid", "import_workers_editor_not_idle", "import_worker_count_readback_mismatch" },
        Preconditions = new[] { "Main Editor", "Stable Edit Mode" },
        CompletionEvidence = "Unity's desired worker count equals the request after ForceToDesiredWorkerCount returns. This does not prove a specific worker's OS exit.")]
    public sealed class VmImportWorkerCountTool : IVmProjectTool<VmImportWorkerCountRequest, VmImportWorkerCountResult>
    {
        public VmImportWorkerCountResult Execute(VmImportWorkerCountRequest request)
        {
            if (request.DesiredWorkerCount < 1 || request.DesiredWorkerCount > 128)
                throw new VmProjectToolException("import_worker_count_invalid", "desiredWorkerCount must be in [1, 128].");
            if (AssetDatabase.IsAssetImportWorkerProcess() || EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new VmProjectToolException("import_workers_editor_not_idle", "Worker reconciliation requires stable Main Edit Mode.");

            long started = Stopwatch.GetTimestamp();
            int previous = AssetDatabase.DesiredWorkerCount;
            AssetDatabase.DesiredWorkerCount = request.DesiredWorkerCount;
            AssetDatabase.ForceToDesiredWorkerCount();
            int current = AssetDatabase.DesiredWorkerCount;
            if (current != request.DesiredWorkerCount)
                throw new VmProjectToolException("import_worker_count_readback_mismatch",
                    $"Native desired worker count is {current}; requested {request.DesiredWorkerCount}.");

            return new VmImportWorkerCountResult
            {
                PreviousDesiredWorkerCount = previous,
                DesiredWorkerCount = current,
                ReconciliationRequested = true,
                ElapsedMilliseconds = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency
            };
        }
    }
}
