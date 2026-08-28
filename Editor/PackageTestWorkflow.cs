using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace VMUnityAutomation.Editor
{
    internal sealed class PackageTestWorkflow
    {
        public string WorkflowId;
        public string State;
        public string PackageName;
        public string Mode;
        public string[] Assemblies;
        public string[] PackageTestAssemblies;
        public string[] TestNames;
        public string[] Categories;
        public string[] GroupNames;
        public string ManifestPath;
        public string OriginalManifestBase64;
        public bool OriginalManifestHadUtf8Bom;
        private ManifestPublicationState manifestPublication =
            ManifestPublicationState.Original;
        private ManifestResolveTarget manifestResolve =
            ManifestResolveTarget.None;
        private bool manifestResolveIssued;
        private bool manifestResolveActivityObserved;
        private bool manifestResolveAssemblyReloadObserved;
        private DateTime manifestResolveStartedAt;

        public ManifestPublicationState ManifestPublication => manifestPublication;
        public ManifestResolveTarget ManifestResolve => manifestResolve;
        public bool ManifestResolveIssued => manifestResolveIssued;
        public bool ManifestResolveActivityObserved =>
            manifestResolveActivityObserved;
        public bool ManifestResolveAssemblyReloadObserved =>
            manifestResolveAssemblyReloadObserved;
        public DateTime ManifestResolveStartedAt => manifestResolveStartedAt;
        public string TestJobId;
        public bool TestSucceeded;
        public bool CancelRequested;
        public Dictionary<string, object> TestResult;
        public string Error;
        public DateTime StartedAt;
        public DateTime UpdatedAt;
        public string OwnerAgentId;

        public bool IsTerminal => State == "succeeded" || State == "failed" || State == "canceled";
        public bool NeedsManifestRestore =>
            ManifestPublication == ManifestPublicationState.Modified ||
            ManifestPublication == ManifestPublicationState.Restoring;

        public void BeginManifestModification()
        {
            if (ManifestPublication == ManifestPublicationState.Modified)
                return;
            TransitionManifestPublication(ManifestPublicationState.Original,
                ManifestPublicationState.Modified, "begin manifest testables publication");
        }

        public void BeginManifestRestore()
        {
            if (ManifestPublication == ManifestPublicationState.Restoring)
                return;
            TransitionManifestPublication(ManifestPublicationState.Modified,
                ManifestPublicationState.Restoring, "begin manifest restoration");
        }

        public void MarkManifestRestored()
        {
            TransitionManifestPublication(ManifestPublicationState.Restoring,
                ManifestPublicationState.Restored, "complete manifest restoration");
        }

        public void MarkManifestRestoreFailed()
        {
            TransitionManifestPublication(ManifestPublicationState.Restoring,
                ManifestPublicationState.RestoreFailed, "fail manifest restoration");
        }

        public void BeginManifestResolve(ManifestResolveTarget target)
        {
            if (target == ManifestResolveTarget.None)
                throw new ArgumentOutOfRangeException(nameof(target));
            if (ManifestResolve == target)
                return;
            if (ManifestResolve != ManifestResolveTarget.None)
                throw BuildManifestResolveTransitionException(
                    "begin Package Manager adoption", target, ManifestResolve);
            manifestResolve = target;
            manifestResolveIssued = false;
            manifestResolveActivityObserved = false;
            manifestResolveAssemblyReloadObserved = false;
            manifestResolveStartedAt = DateTime.UtcNow;
        }

        public void MarkManifestResolveIssued()
        {
            if (ManifestResolve == ManifestResolveTarget.None)
                throw new InvalidOperationException(
                    $"Package-test workflow '{WorkflowId}' cannot issue Package Manager adoption without an active manifest resolve target.");
            if (ManifestResolveIssued)
                throw new InvalidOperationException(
                    $"Package-test workflow '{WorkflowId}' already issued Package Manager adoption for '{ManifestResolve}'.");
            manifestResolveIssued = true;
        }

        public void MarkManifestResolveActivityObserved()
        {
            if (ManifestResolve == ManifestResolveTarget.None)
                throw new InvalidOperationException(
                    $"Package-test workflow '{WorkflowId}' cannot observe Package Manager activity without an active manifest resolve target.");
            manifestResolveActivityObserved = true;
        }

        public void MarkManifestResolveAssemblyReloadObserved()
        {
            if (ManifestResolve == ManifestResolveTarget.None)
                throw new InvalidOperationException(
                    $"Package-test workflow '{WorkflowId}' cannot observe an assembly reload without an active manifest resolve target.");
            if (!ManifestResolveIssued)
                throw new InvalidOperationException(
                    $"Package-test workflow '{WorkflowId}' cannot attribute an assembly reload before Package Manager adoption was issued.");
            manifestResolveActivityObserved = true;
            manifestResolveAssemblyReloadObserved = true;
        }

        public void CompleteManifestResolve(ManifestResolveTarget target)
        {
            if (ManifestResolve != target)
                throw BuildManifestResolveTransitionException(
                    "complete Package Manager adoption", target, ManifestResolve);
            manifestResolve = ManifestResolveTarget.None;
            manifestResolveIssued = false;
            manifestResolveActivityObserved = false;
            manifestResolveAssemblyReloadObserved = false;
            manifestResolveStartedAt = default;
        }

        public void AbandonModifiedManifestResolveForRestore()
        {
            RequireManifestPublication(ManifestPublicationState.Restoring,
                "abandon modified manifest adoption for restoration");
            CompleteManifestResolve(ManifestResolveTarget.Modified);
        }

        public bool HasManifestResolveTimedOut(DateTime utcNow,
            TimeSpan timeout)
        {
            if (ManifestResolve == ManifestResolveTarget.None)
                throw new InvalidOperationException(
                    $"Package-test workflow '{WorkflowId}' cannot evaluate a Package Manager adoption deadline without an active manifest resolve target.");
            if (timeout <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(timeout));
            if (ManifestResolveStartedAt == default)
                throw new InvalidDataException(
                    $"Package-test workflow '{WorkflowId}' has no persisted Package Manager adoption start time.");
            return utcNow - ManifestResolveStartedAt >= timeout;
        }

        public bool HasManifestResolveActivityTimedOut(DateTime utcNow,
            TimeSpan timeout)
        {
            if (ManifestResolve == ManifestResolveTarget.None)
                throw new InvalidOperationException(
                    $"Package-test workflow '{WorkflowId}' cannot evaluate a Package Manager activity deadline without an active manifest resolve target.");
            if (timeout <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(timeout));
            if (!ManifestResolveIssued || ManifestResolveActivityObserved)
                return false;
            if (ManifestResolveStartedAt == default)
                throw new InvalidDataException(
                    $"Package-test workflow '{WorkflowId}' has no persisted Package Manager adoption start time.");
            return utcNow - ManifestResolveStartedAt >= timeout;
        }

        public void RequireManifestResolve(ManifestResolveTarget expected,
            string operation)
        {
            if (ManifestResolve != expected)
                throw BuildManifestResolveTransitionException(operation, expected,
                    ManifestResolve);
        }

        public void RequireManifestPublication(ManifestPublicationState expected,
            string operation)
        {
            if (ManifestPublication != expected)
                throw BuildManifestTransitionException(operation, expected,
                    ManifestPublication);
        }

        private void TransitionManifestPublication(ManifestPublicationState expected,
            ManifestPublicationState next, string operation)
        {
            if (ManifestPublication != expected)
                throw BuildManifestTransitionException(operation, expected,
                    ManifestPublication);
            manifestPublication = next;
        }

        private InvalidOperationException BuildManifestTransitionException(string operation,
            ManifestPublicationState expected, ManifestPublicationState actual)
        {
            return new InvalidOperationException(
                $"Package-test workflow '{WorkflowId}' cannot {operation}: " +
                $"manifest publication state is {actual}; expected {expected}.");
        }

        private InvalidOperationException BuildManifestResolveTransitionException(
            string operation, ManifestResolveTarget expected,
            ManifestResolveTarget actual)
        {
            return new InvalidOperationException(
                $"Package-test workflow '{WorkflowId}' cannot {operation}: " +
                $"manifest resolve target is {actual}; expected {expected}.");
        }

        public Dictionary<string, object> ToDictionary()
        {
            return new Dictionary<string, object>
            {
                { "workflowId", WorkflowId },
                { "state", State },
                { "packageName", PackageName },
                { "mode", Mode },
                { "assemblies", ToObjectList(Assemblies) },
                { "packageTestAssemblies", ToObjectList(PackageTestAssemblies) },
                { "testNames", ToObjectList(TestNames) },
                { "categories", ToObjectList(Categories) },
                { "groupNames", ToObjectList(GroupNames) },
                { "manifestPath", ManifestPath },
                { "originalManifestBase64", OriginalManifestBase64 },
                { "originalManifestHadUtf8Bom", OriginalManifestHadUtf8Bom },
                { "manifestPublicationState", ManifestPublication.ToString() },
                { "manifestResolveTarget", ManifestResolve.ToString() },
                { "manifestResolveIssued", ManifestResolveIssued },
                { "manifestResolveActivityObserved",
                    ManifestResolveActivityObserved },
                { "manifestResolveAssemblyReloadObserved",
                    ManifestResolveAssemblyReloadObserved },
                { "manifestResolveStartedAt", ManifestResolve == ManifestResolveTarget.None
                    ? ""
                    : ManifestResolveStartedAt.ToString("O") },
                { "testJobId", TestJobId ?? "" },
                { "testSucceeded", TestSucceeded },
                { "cancelRequested", CancelRequested },
                { "testResult", TestResult },
                { "error", Error ?? "" },
                { "startedAt", StartedAt.ToString("O") },
                { "updatedAt", UpdatedAt.ToString("O") },
                { "ownerAgentId", OwnerAgentId ?? "anonymous" },
            };
        }

        public static PackageTestWorkflow FromDictionary(Dictionary<string, object> values)
        {
            ManifestPublicationState manifestPublication =
                ParseManifestPublicationState(values);
            ManifestResolveTarget manifestResolve =
                ParseManifestResolveTarget(values);
            var workflow = new PackageTestWorkflow
            {
                WorkflowId = GetValue(values, "workflowId"),
                State = GetValue(values, "state"),
                PackageName = GetValue(values, "packageName"),
                Mode = GetValue(values, "mode", "EditMode"),
                Assemblies = GetArray(values, "assemblies"),
                PackageTestAssemblies = GetArray(values, "packageTestAssemblies"),
                TestNames = GetArray(values, "testNames"),
                Categories = GetArray(values, "categories"),
                GroupNames = GetArray(values, "groupNames"),
                ManifestPath = GetValue(values, "manifestPath"),
                OriginalManifestBase64 = GetValue(values, "originalManifestBase64"),
                OriginalManifestHadUtf8Bom = GetBoolean(values, "originalManifestHadUtf8Bom"),
                TestJobId = GetValue(values, "testJobId"),
                TestSucceeded = GetBoolean(values, "testSucceeded"),
                CancelRequested = GetBoolean(values, "cancelRequested"),
                TestResult = values.TryGetValue("testResult", out var result)
                    ? result as Dictionary<string, object>
                    : null,
                Error = GetValue(values, "error"),
                StartedAt = GetDateTime(values, "startedAt"),
                UpdatedAt = GetDateTime(values, "updatedAt"),
                OwnerAgentId = GetValue(values, "ownerAgentId", "anonymous"),
            };
            workflow.manifestPublication = manifestPublication;
            workflow.manifestResolve = manifestResolve;
            workflow.manifestResolveIssued = GetManifestResolveIssued(
                values, manifestResolve);
            workflow.manifestResolveActivityObserved =
                GetManifestResolveActivityObserved(values);
            workflow.manifestResolveAssemblyReloadObserved =
                GetBoolean(values, "manifestResolveAssemblyReloadObserved");
            workflow.manifestResolveStartedAt = manifestResolve == ManifestResolveTarget.None
                ? default
                : GetDateTime(values, "manifestResolveStartedAt", workflow.UpdatedAt);
            if (workflow.manifestResolve == ManifestResolveTarget.None &&
                workflow.manifestResolveIssued)
                throw new InvalidDataException(
                    "Package-test workflow cannot persist a resolve invocation without an active manifest target.");
            if (workflow.manifestResolve == ManifestResolveTarget.None &&
                workflow.manifestResolveActivityObserved)
                throw new InvalidDataException(
                    "Package-test workflow cannot persist resolve activity without an active manifest target.");
            if (workflow.manifestResolve == ManifestResolveTarget.None &&
                workflow.manifestResolveAssemblyReloadObserved)
                throw new InvalidDataException(
                    "Package-test workflow cannot persist an assembly reload without an active manifest target.");
            if (workflow.manifestResolveAssemblyReloadObserved &&
                !workflow.manifestResolveIssued)
                throw new InvalidDataException(
                    "Package-test workflow cannot persist an assembly reload before its resolve invocation.");
            return workflow;
        }

        private static bool GetManifestResolveIssued(
            Dictionary<string, object> values, ManifestResolveTarget target)
        {
            if (target == ManifestResolveTarget.None)
                return GetBoolean(values, "manifestResolveIssued");
            if (values.ContainsKey("manifestResolveIssued"))
                return GetBoolean(values, "manifestResolveIssued");

            // The previous durable format persisted its target before calling Client.Resolve
            // and then recorded busy evidence. Treat any active legacy target as already
            // issued so a reload cannot overlap a second void Resolve operation.
            return true;
        }

        private static bool GetManifestResolveActivityObserved(
            Dictionary<string, object> values)
        {
            if (values.ContainsKey("manifestResolveActivityObserved"))
                return GetBoolean(values, "manifestResolveActivityObserved");
            if (values.ContainsKey("manifestResolveBusyObserved"))
                return GetBoolean(values, "manifestResolveBusyObserved");
            return false;
        }

        private static ManifestPublicationState ParseManifestPublicationState(
            Dictionary<string, object> values)
        {
            string value = GetValue(values, "manifestPublicationState");
            foreach (ManifestPublicationState state in
                     Enum.GetValues(typeof(ManifestPublicationState)))
            {
                if (string.Equals(value, state.ToString(), StringComparison.Ordinal))
                    return state;
            }

            throw new InvalidDataException(
                $"Unknown package-test manifest publication state '{value}'.");
        }

        private static ManifestResolveTarget ParseManifestResolveTarget(
            Dictionary<string, object> values)
        {
            string value = GetValue(values, "manifestResolveTarget",
                ManifestResolveTarget.None.ToString());
            foreach (ManifestResolveTarget target in
                     Enum.GetValues(typeof(ManifestResolveTarget)))
            {
                if (string.Equals(value, target.ToString(),
                        StringComparison.Ordinal))
                    return target;
            }

            throw new InvalidDataException(
                $"Unknown package-test manifest resolve target '{value}'.");
        }

        private static List<object> ToObjectList(IEnumerable<string> values)
        {
            return values?.Cast<object>().ToList() ?? new List<object>();
        }

        private static string GetValue(Dictionary<string, object> values, string key,
            string defaultValue = "")
        {
            return values.TryGetValue(key, out var value) && value != null ? value.ToString() : defaultValue;
        }

        private static bool GetBoolean(Dictionary<string, object> values, string key)
        {
            return values.TryGetValue(key, out var value) && value != null && Convert.ToBoolean(value);
        }

        private static string[] GetArray(Dictionary<string, object> values, string key)
        {
            return values.TryGetValue(key, out var value) && value is List<object> list
                ? list.Select(item => item?.ToString()).Where(item => !string.IsNullOrEmpty(item)).ToArray()
                : Array.Empty<string>();
        }

        private static DateTime GetDateTime(Dictionary<string, object> values, string key)
        {
            return TryGetUtcDateTime(values, key, out DateTime result)
                ? result
                : DateTime.UtcNow;
        }

        private static DateTime GetDateTime(Dictionary<string, object> values, string key,
            DateTime fallback)
        {
            return TryGetUtcDateTime(values, key, out DateTime result)
                ? result
                : fallback;
        }

        private static bool TryGetUtcDateTime(Dictionary<string, object> values, string key,
            out DateTime result)
        {
            if (!DateTime.TryParse(GetValue(values, key), CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out result))
                return false;
            result = result.ToUniversalTime();
            return true;
        }
    }
}
