using System;
using System.Collections.Generic;
using System.Reflection;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationBurstCompilation
    {
        internal const string Phase = "compiling-burst";
        internal const double TimeoutSeconds = 600;

        internal static bool Requested(Dictionary<string, object> request) =>
            request.TryGetValue("recompileBurst", out object value) && (bool)value;

        internal static Dictionary<string, object> AdmissionError(Dictionary<string, object> request)
        {
            if (!Requested(request)) return null;
#if UNITY_6000_6_OR_NEWER
            return Unity.Burst.BurstCompiler.IsEnabled ? null : VmAutomationResponse.Error(
                "Native Burst recompilation requires the integrated Burst compiler to be enabled.",
                "burst_compiler_disabled", false);
#else
            return VmAutomationResponse.Error(
                "Native Burst recompilation requires Unity 6000.6 or newer.",
                "capability_unavailable", false);
#endif
        }

        internal static Dictionary<string, object> Advance(VmAutomationWorkspaceJob job,
            Action persist, out bool completed)
        {
            completed = !Requested(job.Request);
            if (completed) return null;
            Dictionary<string, object> error = AdmissionError(job.Request);
            if (error != null) return error;
#if UNITY_6000_6_OR_NEWER
            if (!job.BurstCompilationStartedAt.HasValue)
            {
                job.BurstCompilationStartedAt = DateTime.UtcNow;
                job.BurstCompilationDomainRevision = job.DomainReloadCount;
                job.Phase = Phase;
                job.StatusMessage = "Recompiling through Unity's native Burst compiler.";
                persist();
                Native.Recompile();
                return null;
            }
            error = ObservationError(job, DateTime.UtcNow);
            if (error != null) return error;
            if (!Native.IsDone()) return null;
            job.BurstCompilationFinishedAt = DateTime.UtcNow;
            error = CompleteDiagnostics(job, UnityEngine.Application.consoleLogPath);
            if (error != null) return error;
            completed = true;
#endif
            return null;
        }

        internal static Dictionary<string, object> CompleteDiagnostics(VmAutomationWorkspaceJob job,
            string logPath)
        {
            if (job.BurstDiagnostics == null)
                return VmAutomationResponse.Error(
                    "The native diagnostic window was not recorded before workspace compilation.",
                    "burst_compilation_diagnostics_incomplete", false);
            job.BurstDiagnostics.Complete(logPath);
            return job.BurstDiagnostics.BuildFailure();
        }

        internal static Dictionary<string, object> ObservationError(VmAutomationWorkspaceJob job,
            DateTime now)
        {
            if (!job.BurstCompilationStartedAt.HasValue)
                throw new InvalidOperationException("A native Burst observation requires its published request.");
            if (job.BurstCompilationDomainRevision != job.DomainReloadCount)
                return VmAutomationResponse.Error(
                    "The assembly domain changed during native Burst recompilation; native adoption is unverified.",
                    "burst_compilation_interrupted", false);
            return now - job.BurstCompilationStartedAt.Value >= TimeSpan.FromSeconds(TimeoutSeconds)
                ? VmAutomationResponse.Error("Native Burst recompilation exceeded its 600-second deadline.",
                    "burst_compilation_timeout", false)
                : null;
        }

#if UNITY_6000_6_OR_NEWER
        static class Native
        {
            internal static readonly Action Recompile = Bind<Action>("TriggerRecompilation");
            internal static readonly Func<bool> IsDone = Bind<Func<bool>>("IsCurrentCompilationDone");

            static T Bind<T>(string name, params Type[] parameters) where T : Delegate
            {
                Type owner = typeof(Unity.Burst.BurstCompiler);
                MethodInfo method = owner.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static,
                    null, parameters, null) ?? throw new MissingMethodException(owner.FullName, name);
                return (T)method.CreateDelegate(typeof(T));
            }
        }
#endif
    }
}
