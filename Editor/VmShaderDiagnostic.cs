namespace VMUnityAutomation.Editor
{
    public sealed class VmShaderDiagnostic
    {
        [VmRequired, VmJsonProperty("severity")] public string Severity { get; set; }
        [VmRequired, VmJsonProperty("message")] public string Message { get; set; }
        [VmRequired, VmJsonProperty("file")] public string File { get; set; }
        [VmRequired, VmJsonProperty("line")] public int Line { get; set; }
        [VmRequired, VmJsonProperty("platform")] public string Platform { get; set; }
    }
}
