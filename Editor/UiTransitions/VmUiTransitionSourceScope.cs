using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    internal sealed class VmUiTransitionSourceScope : ScriptableObject
    {
        private CancellationTokenSource lifetime;
        private Task<VmUiTransitionSourceReport> worker;
        private Func<bool> cancellationRequested;
        private string jobID, identity;
        private Task<VmProjectToolCleanupReceipt> retirement;
        internal bool Completed => worker.IsCompleted;
        internal VmUiTransitionSourceReport Result => worker.GetAwaiter().GetResult();

        internal void Begin(VmUiTransitionSourceRequest request)
        {
            jobID = VmProjectToolExecutionContext.JobId; identity = VmObjectId.Get(this);
            cancellationRequested = VmProjectToolExecutionContext.CaptureCancellationCheck();
            lifetime = new CancellationTokenSource();
            AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
            try { worker = VmUiTransitionSourceReview.Start(request, lifetime.Token); }
            catch { AssemblyReloadEvents.beforeAssemblyReload -= BeforeReload; lifetime.Dispose(); throw; }
        }

        internal void ObserveCancellation()
        {
            if (cancellationRequested()) lifetime.Cancel();
        }

        internal void CloseCompleted()
        {
            if (!worker.IsCompleted) throw new InvalidOperationException("A live UI source worker cannot be destroyed.");
            AssemblyReloadEvents.beforeAssemblyReload -= BeforeReload;
            lifetime.Dispose();
            DestroyImmediate(this);
        }

        internal Task<VmProjectToolCleanupReceipt> RetireAsync(string expectedJob)
        {
            if (expectedJob != jobID) throw new VmProjectToolException("ui_transition_source_owner", "The cleanup job does not own this native source scope.");
            return retirement ??= RetireWorkerAsync();
        }

        internal Task<VmProjectToolCleanupReceipt> RequestRetirement() => retirement ??= RetireWorkerAsync();

        private async Task<VmProjectToolCleanupReceipt> RetireWorkerAsync()
        {
            lifetime.Cancel();
            Exception failure = null;
            try { await worker; }
            catch (OperationCanceledException error) when (error.CancellationToken == lifetime.Token && lifetime.IsCancellationRequested) { }
            catch (Exception error) { failure = error; }
            CloseCompleted();
            return new VmProjectToolCleanupReceipt(jobID, identity, true,
                new VmUiTransitionSourceCleanupResult { Scope = identity, WorkerJoined = true, ResourcesReleased = true }, failure);
        }

        private void BeforeReload()
        {
            lifetime.Cancel();
            try { worker.GetAwaiter().GetResult(); }
            catch (OperationCanceledException error) when (error.CancellationToken == lifetime.Token && lifetime.IsCancellationRequested) { }
            finally { CloseCompleted(); }
        }
    }
}
