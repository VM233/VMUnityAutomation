using System.ComponentModel;

namespace VMUnityAutomation.Editor
{
    public sealed class VmImportWorkerCountRequest
    {
        [VmRequired, VmRange(1, 128), VmJsonProperty("desiredWorkerCount")]
        [Description("Session-only desired import worker count. Unity can start missing workers and shut down idle surplus workers.")]
        public int DesiredWorkerCount { get; set; }
    }
}
