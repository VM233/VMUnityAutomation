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
        private TaskCompletionSource<VmProjectToolCleanupReceipt> retired;
        private Task retiring;
        internal bool Completed => worker.IsCompleted;
        internal VmUiTransitionSourceReport Result => worker.GetAwaiter().GetResult();

        internal void Begin(VmUiTransitionSourceRequest request)
        {
            lifetime = new CancellationTokenSource();
            Func<Task<VmUiTransitionSourceReport>> start;
            try { start = VmUiTransitionSourceReview.Prepare(request, lifetime.Token); }
            catch { lifetime.Dispose(); throw; }
            jobID = VmProjectToolExecutionContext.JobId; identity = VmObjectId.Get(this);
            cancellationRequested = VmProjectToolExecutionContext.CaptureCancellationCheck();
            retired = new TaskCompletionSource<VmProjectToolCleanupReceipt>();
            VmProjectToolExecutionContext.PublishCleanupToken(identity, retired.Task);
            AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
            try { worker = start(); }
            catch (Exception failure) { worker = Task.FromException<VmUiTransitionSourceReport>(failure); CloseCompleted(); throw; }
        }

        internal void ObserveCancellation()
        {
            if (cancellationRequested()) lifetime.Cancel();
        }

        internal void CloseCompleted()
        {
            if (!worker.IsCompleted) throw new InvalidOperationException("A live UI source worker cannot be destroyed.");
            Exception failure = null;
            try { worker.GetAwaiter().GetResult(); }
            catch (OperationCanceledException error) when (error.CancellationToken == lifetime.Token && lifetime.IsCancellationRequested) { }
            catch (Exception error) { failure = error; }
            AssemblyReloadEvents.beforeAssemblyReload -= BeforeReload;
            lifetime.Dispose();
            retired.SetResult(new VmProjectToolCleanupReceipt(jobID, identity, true,
                new VmUiTransitionSourceCleanupResult { Scope = identity, WorkerJoined = true, ResourcesReleased = true }, failure));
            DestroyImmediate(this);
        }

        internal Task<VmProjectToolCleanupReceipt> RetireAsync(string expectedJob)
        {
            if (expectedJob != jobID) throw new VmProjectToolException("ui_transition_source_owner", "The cleanup job does not own this native source scope.");
            return RequestRetirement();
        }

        internal Task<VmProjectToolCleanupReceipt> RequestRetirement()
        {
            lifetime.Cancel();
            retiring ??= RetireWorkerAsync();
            return retired.Task;
        }

        internal VmUiTransitionSourceCleanupResult ObserveRetirement() => new()
        { Scope = identity, WorkerJoined = worker.IsCompleted, ResourcesReleased = retired.Task.IsCompleted };

        private async Task RetireWorkerAsync()
        {
            lifetime.Cancel();
            try { await worker; }
            catch (OperationCanceledException error) when (error.CancellationToken == lifetime.Token && lifetime.IsCancellationRequested) { }
            catch (Exception) { /* CloseCompleted publishes the exact source failure. */ }
            CloseCompleted();
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
