using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace VMUnityAutomation.Editor
{
    internal sealed class VmCompilationDiagnosticsProduct
    {
        internal string Revision { get; }
        internal bool CaptureComplete { get; }
        internal string CaptureIssue { get; }
        internal IReadOnlyList<IReadOnlyDictionary<string, object>> Entries { get; }
        internal int ErrorCount { get; }
        internal int WarningCount { get; }
        internal int DeprecatedWarningCount { get; }

        internal VmCompilationDiagnosticsProduct(string revision, bool captureComplete,
            string captureIssue, IEnumerable<Dictionary<string, object>> entries)
        {
            Revision = revision;
            CaptureComplete = captureComplete;
            CaptureIssue = captureIssue;
            Entries = new ReadOnlyCollection<IReadOnlyDictionary<string, object>>(entries
                .Select(entry => (IReadOnlyDictionary<string, object>)
                    new ReadOnlyDictionary<string, object>(new Dictionary<string, object>(entry)))
                .ToList());
            foreach (IReadOnlyDictionary<string, object> entry in Entries)
            {
                if ((string)entry["severity"] == "error")
                    ErrorCount++;
                else
                {
                    WarningCount++;
                    if ((bool)entry["isDeprecated"])
                        DeprecatedWarningCount++;
                }
            }
        }

        internal Dictionary<string, object> BuildFailure()
        {
            return CaptureComplete ? null : VmAutomationResponse.Error(
                $"Latest compilation diagnostics are incomplete. {CaptureIssue}",
                "compilation_diagnostics_incomplete", false,
                new Dictionary<string, object> { { "snapshotRevision", Revision } });
        }
    }
}
