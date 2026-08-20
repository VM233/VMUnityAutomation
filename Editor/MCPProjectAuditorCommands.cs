using System;
using System.Collections.Generic;

#if UNITY_6000_4_OR_NEWER
using System.Diagnostics;
using Unity.ProjectAuditor.Editor;
#endif

namespace VMUnityAutomation.Editor
{
    internal static class MCPProjectAuditorCommands
    {
#if UNITY_6000_4_OR_NEWER
        private static bool auditInProgress;
#endif

        internal static void Audit(Dictionary<string, object> arguments,
            Action<object> resolve, Action<object> progress)
        {
#if UNITY_6000_4_OR_NEWER
            if (!MCPCapabilityRegistry.IsCapabilityAvailable("project-auditor"))
            {
                resolve(MCPResponse.Error(
                    "Project Auditor requires Unity 6.4 or newer.",
                    "project_auditor_unavailable"));
                return;
            }

            arguments.TryGetValue("categories", out object categories);
            arguments.TryGetValue("descriptorIds", out object descriptorIds);
            arguments.TryGetValue("severities", out object severities);
            arguments.TryGetValue("offset", out object offset);
            arguments.TryGetValue("limit", out object limit);
            if (!MCPProjectAuditorQuery.TryCreate(categories, descriptorIds,
                    severities, offset, limit,
                    out MCPProjectAuditorQuery query,
                    out Dictionary<string, object> error))
            {
                resolve(error);
                return;
            }

            if (auditInProgress)
            {
                resolve(MCPResponse.Error(
                    "A Project Auditor analysis started by VM Unity Automation is already running.",
                    "project_auditor_busy", true));
                return;
            }

            var analysisParams = new AnalysisParams(true);
            if (query.Categories.Count > 0)
                analysisParams.Categories = query.CreateAnalysisCategories();

            var stopwatch = Stopwatch.StartNew();
            var auditor = new ProjectAuditor();
            analysisParams.OnCompleted = report =>
            {
                object result;
                try
                {
                    result = MCPProjectAuditorReportSerializer.Serialize(report,
                        query, analysisParams.Platform.ToString(),
                        stopwatch.ElapsedMilliseconds);
                }
                catch (Exception exception)
                {
                    result = MCPResponse.Error(
                        $"Project Auditor report serialization failed: {exception.Message}",
                        "project_auditor_result_failed");
                }

                auditInProgress = false;
                resolve(result);
            };

            auditInProgress = true;
            try
            {
                auditor.AuditAsync(analysisParams,
                    new ProjectAuditorProgress(progress));
            }
            catch
            {
                auditInProgress = false;
                throw;
            }
#else
            resolve(MCPResponse.Error(
                "Project Auditor is available in Unity 6.4 or newer.",
                "project_auditor_unavailable"));
#endif
        }

#if UNITY_6000_4_OR_NEWER
        private sealed class ProjectAuditorProgress :
            Unity.ProjectAuditor.Editor.IProgress
        {
            private readonly Action<object> publish;

            internal ProjectAuditorProgress(Action<object> publish)
            {
                this.publish = publish;
            }

            public bool IsCancelled => false;

#if UNITY_6000_5_OR_NEWER
            public void Cancel()
            {
            }

            public AsyncProgressState Start(string title, int total)
            {
                publish(new Dictionary<string, object>
                {
                    { "phase", "analyzing" },
                    { "title", title },
                    { "totalSteps", total },
                });
                return new AsyncProgressState();
            }

            public void Advance(AsyncProgressState state, string description)
            {
                // Module starts are the bounded public progress product. Per-asset
                // advances would persist one queue snapshot for every analyzed asset.
            }

            public void Clear(AsyncProgressState state)
            {
            }

            public AsyncProgressState StartRoot(string title, string description,
                int total)
            {
                publish(new Dictionary<string, object>
                {
                    { "phase", "analyzing" },
                    { "title", title },
                    { "description", description },
                    { "totalSteps", total },
                });
                return new AsyncProgressState();
            }

            public void AdvanceRoot(AsyncProgressState state)
            {
            }

            public void ClearRoot(AsyncProgressState state)
            {
            }
#else
            public void Start(string title, string description, int total)
            {
                publish(new Dictionary<string, object>
                {
                    { "phase", "analyzing" },
                    { "title", title },
                    { "description", description },
                    { "totalSteps", total },
                });
            }

            public void Advance(string description)
            {
                // Module starts are the bounded public progress product. Per-asset
                // advances would persist one queue snapshot for every analyzed asset.
            }

            public void Clear()
            {
            }
#endif
        }
#endif
    }
}
