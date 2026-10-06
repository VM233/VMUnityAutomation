namespace VMUnityAutomation.Editor
{
    public sealed class VmComputeKernelSupportResult
    {
        [VmRequired, VmJsonProperty("assetPath")] public string AssetPath { get; set; }
        [VmRequired, VmJsonProperty("shaderName")] public string ShaderName { get; set; }
        [VmRequired, VmJsonProperty("kernelName")] public string KernelName { get; set; }
        [VmRequired, VmJsonProperty("kernelIndex")] public int KernelIndex { get; set; }
        [VmRequired, VmJsonProperty("graphicsDeviceType")] public string GraphicsDeviceType { get; set; }
        [VmRequired, VmJsonProperty("isSupported")] public bool IsSupported { get; set; }
        [VmJsonProperty("threadsX")] public uint? ThreadsX { get; set; }
        [VmJsonProperty("threadsY")] public uint? ThreadsY { get; set; }
        [VmJsonProperty("threadsZ")] public uint? ThreadsZ { get; set; }
    }
}
