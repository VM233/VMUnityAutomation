namespace VMUnityAutomation.Runtime
{
    public sealed class VmUiAuditFinding
    {
        public string Rule { get; set; }
        public string Path { get; set; }
        public string ElementName { get; set; }
        public string Owner { get; set; }
        public string Message { get; set; }
        public double Measured { get; set; }
        public double Required { get; set; }
        public string Observed { get; set; }
        public string Expected { get; set; }
    }
}
