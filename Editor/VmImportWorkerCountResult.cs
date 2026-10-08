namespace VMUnityAutomation.Editor
{
    public sealed class VmImportWorkerCountResult
    {
        [VmJsonProperty("previousDesiredWorkerCount")]
        public int PreviousDesiredWorkerCount { get; set; }

        [VmJsonProperty("desiredWorkerCount")]
        public int DesiredWorkerCount { get; set; }

        [VmJsonProperty("reconciliationRequested")]
        public bool ReconciliationRequested { get; set; }

        [VmJsonProperty("elapsedMilliseconds")]
        public double ElapsedMilliseconds { get; set; }
    }
}
